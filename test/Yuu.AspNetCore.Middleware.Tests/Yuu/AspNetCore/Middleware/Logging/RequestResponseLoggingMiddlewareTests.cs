using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using Yuu.AspNetCore.Middleware.Logging;
using Yuu.AspNetCore.Middleware.Logging.Options;
using Yuu.Environment.Provider;

namespace Yuu.AspNetCore.Middleware.Logging.Tests;

/// <summary>
/// <see cref="RequestResponseLoggingMiddleware"/> 降噪改版的行為測試:
/// 等級矩陣/慢請求升級、body 雙重閘門(設定 × local 環境,fail-safe)、
/// 摘要模式不做 response 緩衝、SSE 完全跳過、複合敏感鍵遮蔽、截斷。
/// 測試用的假密碼/假金鑰皆為 dummy 值,斷言失敗輸出不含真 secret。
/// </summary>
public class RequestResponseLoggingMiddlewareTests
{
    /// <summary>
    /// 攔截輸出的假 logger:收集 (等級, 訊息) 供斷言;IsEnabled 恆真讓等級判定交由斷言驗證。
    /// </summary>
    private sealed class CapturingLogger : ILogger<RequestResponseLoggingMiddleware>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private static RequestResponseLoggingMiddleware CreateMiddleware(
        RequestDelegate next,
        CapturingLogger logger,
        RequestResponseLoggingOptions? options = null,
        bool isLocal = false)
    {
        var environmentProvider = Substitute.For<IEnvironmentProvider>();
        environmentProvider.IsLocal().Returns(isLocal);
        environmentProvider.EnvName.Returns(isLocal ? "local-dev" : "develop");

        return new RequestResponseLoggingMiddleware(
            next,
            logger,
            environmentProvider,
            Microsoft.Extensions.Options.Options.Create(options ?? new RequestResponseLoggingOptions()));
    }

    private static DefaultHttpContext CreateContext(
        string path = "/api/v1/test",
        string? requestBody = null,
        string? accept = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (requestBody is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(requestBody);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            context.Request.ContentType = "application/json";
        }

        if (accept is not null)
        {
            context.Request.Headers.Accept = accept;
        }

        return context;
    }

    /// <summary>
    /// 等級矩陣:2xx/3xx=Debug、一般 4xx=Information、408/429=Warning、5xx=Error。
    /// </summary>
    [Theory]
    [InlineData(200, LogLevel.Debug)]
    [InlineData(302, LogLevel.Debug)]
    [InlineData(400, LogLevel.Information)]
    [InlineData(404, LogLevel.Information)]
    [InlineData(409, LogLevel.Information)]
    [InlineData(408, LogLevel.Warning)]
    [InlineData(429, LogLevel.Warning)]
    [InlineData(500, LogLevel.Error)]
    [InlineData(503, LogLevel.Error)]
    public async Task Summary_level_matrix(int statusCode, LogLevel expected)
    {
        var logger = new CapturingLogger();
        var middleware = CreateMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            },
            logger);

        await middleware.InvokeAsync(CreateContext());

        logger.Entries.ShouldHaveSingleItem();
        logger.Entries[0].Level.ShouldBe(expected);
        logger.Entries[0].Message.ShouldContain($"responded {statusCode}");
    }

    /// <summary>
    /// 慢請求升 Warning(門檻設 1ms + 人為延遲保證觸發);5xx 不因慢而降級,仍為 Error。
    /// </summary>
    [Theory]
    [InlineData(200, LogLevel.Warning)]
    [InlineData(500, LogLevel.Error)]
    public async Task Slow_request_escalates_to_warning_but_5xx_stays_error(int statusCode, LogLevel expected)
    {
        var logger = new CapturingLogger();
        var middleware = CreateMiddleware(
            async ctx =>
            {
                await Task.Delay(30);
                ctx.Response.StatusCode = statusCode;
            },
            logger,
            new RequestResponseLoggingOptions { SlowRequestThresholdMs = 1 });

        await middleware.InvokeAsync(CreateContext());

        logger.Entries.ShouldHaveSingleItem();
        logger.Entries[0].Level.ShouldBe(expected);
    }

    /// <summary>
    /// 摘要模式(預設):不緩衝 response(管線內看到的 Body 與原始串流同一顆)、訊息不含 body 內容。
    /// </summary>
    [Fact]
    public async Task Summary_mode_does_not_buffer_response_and_omits_bodies()
    {
        var logger = new CapturingLogger();
        Stream? bodySeenByHandler = null;
        var middleware = CreateMiddleware(
            ctx =>
            {
                bodySeenByHandler = ctx.Response.Body;
                return Task.CompletedTask;
            },
            logger,
            isLocal: false);

        var context = CreateContext(requestBody: """{"name":"value-marker"}""");
        var originalBody = context.Response.Body;

        await middleware.InvokeAsync(context);

        bodySeenByHandler.ShouldBeSameAs(originalBody);
        logger.Entries.ShouldHaveSingleItem();
        logger.Entries[0].Message.ShouldNotContain("value-marker");
    }

    /// <summary>
    /// 非 local 環境即使 LogBodies=true 也強制走摘要模式(fail-safe:部署誤設不得洩 body)。
    /// </summary>
    [Fact]
    public async Task Non_local_forces_summary_even_when_log_bodies_enabled()
    {
        var logger = new CapturingLogger();
        Stream? bodySeenByHandler = null;
        var middleware = CreateMiddleware(
            ctx =>
            {
                bodySeenByHandler = ctx.Response.Body;
                return Task.CompletedTask;
            },
            logger,
            new RequestResponseLoggingOptions { LogBodies = true },
            isLocal: false);

        var context = CreateContext(requestBody: """{"name":"value-marker"}""");
        var originalBody = context.Response.Body;

        await middleware.InvokeAsync(context);

        bodySeenByHandler.ShouldBeSameAs(originalBody);
        logger.Entries.ShouldHaveSingleItem();
        logger.Entries[0].Message.ShouldNotContain("value-marker");
    }

    /// <summary>
    /// local + LogBodies=true:記 body(框線格式),response 有做緩衝且內容原樣回寫給呼叫端。
    /// </summary>
    [Fact]
    public async Task Local_with_log_bodies_logs_bodies_and_restores_response()
    {
        var logger = new CapturingLogger();
        Stream? bodySeenByHandler = null;
        var middleware = CreateMiddleware(
            async ctx =>
            {
                bodySeenByHandler = ctx.Response.Body;
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("""{"result":"response-marker"}""");
            },
            logger,
            new RequestResponseLoggingOptions { LogBodies = true },
            isLocal: true);

        var context = CreateContext(requestBody: """{"name":"value-marker"}""");
        var originalBody = context.Response.Body;

        await middleware.InvokeAsync(context);

        // body 模式必須緩衝(管線內是替身串流),結束後原樣回寫到原始串流。
        bodySeenByHandler.ShouldNotBeSameAs(originalBody);
        originalBody.Position = 0;
        (await new StreamReader(originalBody).ReadToEndAsync()).ShouldContain("response-marker");

        logger.Entries.ShouldHaveSingleItem();
        logger.Entries[0].Message.ShouldContain("value-marker");
        logger.Entries[0].Message.ShouldContain("response-marker");
    }

    /// <summary>
    /// SSE(Accept 或 /events 路徑):完全不記錄、不緩衝——緩衝會破壞即時推送。
    /// </summary>
    [Theory]
    [InlineData("/api/v1/vm-provision/jobs/x/events", null)]
    [InlineData("/api/v1/anything", "text/event-stream")]
    public async Task Sse_requests_skip_logging_and_buffering(string path, string? accept)
    {
        var logger = new CapturingLogger();
        Stream? bodySeenByHandler = null;
        var middleware = CreateMiddleware(
            ctx =>
            {
                bodySeenByHandler = ctx.Response.Body;
                return Task.CompletedTask;
            },
            logger,
            new RequestResponseLoggingOptions { LogBodies = true },
            isLocal: true);

        var context = CreateContext(path: path, accept: accept);
        var originalBody = context.Response.Body;

        await middleware.InvokeAsync(context);

        bodySeenByHandler.ShouldBeSameAs(originalBody);
        logger.Entries.ShouldBeEmpty();
    }

    /// <summary>
    /// health/swagger 與 ABP framework API 等基礎設施路徑不記錄:
    /// 前者是高頻探針,後者是前端 bootstrap 固定請求,都不是業務操作訊號。
    /// </summary>
    [Theory]
    [InlineData("/health/ready")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/api/abp/application-configuration")]
    [InlineData("/API/ABP/application-localization")]
    [InlineData("/")]
    public async Task Infrastructure_paths_are_skipped(string path)
    {
        var logger = new CapturingLogger();
        var middleware = CreateMiddleware(ctx => Task.CompletedTask, logger);

        await middleware.InvokeAsync(CreateContext(path: path));

        logger.Entries.ShouldBeEmpty();
    }

    /// <summary>
    /// ABP 排除規則使用 path segment 比對,不得誤傷名稱剛好以 <c>/api/abp</c> 起頭的自訂 API。
    /// </summary>
    [Fact]
    public async Task Abp_like_custom_path_is_not_skipped()
    {
        var logger = new CapturingLogger();
        var middleware = CreateMiddleware(ctx => Task.CompletedTask, logger);

        await middleware.InvokeAsync(CreateContext(path: "/api/abp-custom/resource"));

        logger.Entries.ShouldHaveSingleItem();
    }

    /// <summary>
    /// 複合/camelCase 敏感鍵遮蔽:rootPassword/adminPassword/currentPassword/accessSecret/
    /// accessToken/refreshToken/authorization/apiKey/cardNumber 的值一律變 ***
    /// (舊版只匹配裸鍵名,這批全數漏遮——鎖住回歸)。dummy 值,非真 secret。
    /// </summary>
    [Fact]
    public async Task Compound_sensitive_keys_are_masked_in_body_log()
    {
        var logger = new CapturingLogger();
        const string requestBody =
            """
            {"password":"dummy-1","rootPassword":"dummy-2","adminPassword":"dummy-3",
             "currentPassword":"dummy-4","accessSecret":"dummy-5","accessToken":"dummy-6",
             "refreshToken":"dummy-7","authorization":"dummy-8","apiKey":"dummy-9",
             "api_key":"dummy-10","creditCard":"dummy-11","cardNumber":"dummy-12","name":"keep-me"}
            """;
        var middleware = CreateMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            logger,
            new RequestResponseLoggingOptions { LogBodies = true },
            isLocal: true);

        await middleware.InvokeAsync(CreateContext(requestBody: requestBody));

        logger.Entries.ShouldHaveSingleItem();
        var message = logger.Entries[0].Message;
        for (var i = 1; i <= 12; i++)
        {
            message.ShouldNotContain($"dummy-{i}");
        }

        message.ShouldContain("***");
        message.ShouldContain("keep-me"); // 非敏感欄位不誤傷。
    }

    /// <summary>
    /// form-urlencoded 的複合敏感鍵同樣遮蔽。
    /// </summary>
    [Fact]
    public async Task Form_urlencoded_sensitive_keys_are_masked()
    {
        var logger = new CapturingLogger();
        var middleware = CreateMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            logger,
            new RequestResponseLoggingOptions { LogBodies = true },
            isLocal: true);

        var context = CreateContext();
        var bytes = Encoding.UTF8.GetBytes("username=alice&rootPassword=dummy-form&client_secret=dummy-cs");
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Request.ContentType = "application/x-www-form-urlencoded";

        await middleware.InvokeAsync(context);

        var message = logger.Entries.ShouldHaveSingleItem().Message;
        message.ShouldNotContain("dummy-form");
        message.ShouldNotContain("dummy-cs");
        message.ShouldContain("alice"); // 非敏感欄位不誤傷。
    }

    /// <summary>
    /// 摘要模式的 query fail-safe:只列 key 名集合,任何 query 值(不論敏感與否)都不進 log
    /// ——OAuth code / reset token / signature 常走 query,denylist 遮值必有漏網。
    /// </summary>
    [Fact]
    public async Task Summary_logs_query_keys_but_never_values()
    {
        var logger = new CapturingLogger();
        var middleware = CreateMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            logger);

        var context = CreateContext();
        context.Request.QueryString = new QueryString("?token=dummy-qs-1&plain=hello-value");

        await middleware.InvokeAsync(context);

        var message = logger.Entries.ShouldHaveSingleItem().Message;
        message.ShouldContain("token,plain"); // key 集合保留可觀測性。
        message.ShouldNotContain("dummy-qs-1");
        message.ShouldNotContain("hello-value"); // 非敏感值同樣不輸出(keys only)。
    }

    /// <summary>
    /// body 模式(local)顯示 query 原文,但敏感 key(token/code/signature/…)的值要遮;
    /// 非敏感 key 的值保留除錯價值。
    /// </summary>
    [Fact]
    public async Task Body_mode_masks_sensitive_query_values()
    {
        var logger = new CapturingLogger();
        var middleware = CreateMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            logger,
            new RequestResponseLoggingOptions { LogBodies = true },
            isLocal: true);

        var context = CreateContext();
        context.Request.QueryString = new QueryString("?resetToken=dummy-qs-2&code=dummy-qs-3&page=3");

        await middleware.InvokeAsync(context);

        var message = logger.Entries.ShouldHaveSingleItem().Message;
        message.ShouldNotContain("dummy-qs-2");
        message.ShouldNotContain("dummy-qs-3");
        message.ShouldContain("resetToken=***");
        message.ShouldContain("page=3"); // 非敏感值不誤傷。
    }

    /// <summary>
    /// MaxBodyLogLength 設 0 或負值:請求照常完成、log 照出,只是 body 內容不輸出
    /// (設定錯誤不得在請求中途炸 range 例外)。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Zero_or_negative_max_body_length_omits_body_without_breaking_request(int maxBodyLogLength)
    {
        var logger = new CapturingLogger();
        var middleware = CreateMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            logger,
            new RequestResponseLoggingOptions { LogBodies = true, MaxBodyLogLength = maxBodyLogLength },
            isLocal: true);

        var context = CreateContext(requestBody: """{"name":"value-marker"}""");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(200);
        var message = logger.Entries.ShouldHaveSingleItem().Message;
        message.ShouldNotContain("value-marker");
        message.ShouldContain("body omitted");
    }

    /// <summary>
    /// 超過 MaxBodyLogLength 的 body 被截斷並標註總長。
    /// </summary>
    [Fact]
    public async Task Overlong_body_is_truncated()
    {
        var logger = new CapturingLogger();
        var middleware = CreateMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            logger,
            new RequestResponseLoggingOptions { LogBodies = true, MaxBodyLogLength = 16 },
            isLocal: true);

        await middleware.InvokeAsync(CreateContext(requestBody: new string('x', 200)));

        var message = logger.Entries.ShouldHaveSingleItem().Message;
        message.ShouldContain("(truncated, total length: 200)");
        message.ShouldNotContain(new string('x', 32));
    }

    /// <summary>
    /// 摘要模式下管線拋例外:補一筆 500 Error 摘要(保留耗時軌跡)後原樣往外拋,不吞例外。
    /// </summary>
    [Fact]
    public async Task Summary_mode_logs_500_and_rethrows_on_exception()
    {
        var logger = new CapturingLogger();
        var middleware = CreateMiddleware(
            _ => throw new InvalidOperationException("boom"),
            logger);

        await Should.ThrowAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(CreateContext()));

        logger.Entries.ShouldHaveSingleItem();
        logger.Entries[0].Level.ShouldBe(LogLevel.Error);
        logger.Entries[0].Message.ShouldContain("responded 500");
    }
}
