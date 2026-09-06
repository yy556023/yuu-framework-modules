using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yuu.AspNetCore.Middleware.Logging.Options;
using Yuu.Environment.Provider;

namespace Yuu.AspNetCore.Middleware.Logging;

/// <summary>
/// HTTP 請求/回應記錄 middleware(降噪改版 2026-07-16)。
/// <para>
/// 兩種模式:<b>摘要模式</b>(預設)只記單行結構化摘要(Method/Path/StatusCode/ElapsedMs/TraceId/ClientIp),
/// 不讀 body、不做 response 緩衝——判定提前在任何緩衝/regex 處理之前,production 不繳無謂的記憶體與延遲;
/// <b>body 模式</b>需 <see cref="RequestResponseLoggingOptions.LogBodies"/>=true <b>且</b>環境為 local
/// (雙重閘門 fail-safe:develop/production 誤設 true 也不輸出 body),輸出人讀框線格式含遮蔽後的 body。
/// </para>
/// <para>
/// 等級規則:5xx=Error;408/429=Warning;達慢請求門檻=Warning(5xx 仍為 Error);其餘 4xx=Information;
/// 正常 2xx/3xx=Debug(production root 為 Information → 正常流量天然靜音,稽核責任由 ABP audit log 承擔)。
/// </para>
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly IEnvironmentProvider _environment;
    private readonly RequestResponseLoggingOptions _options;

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    /// <summary>
    /// JSON 敏感鍵遮蔽:鍵名「包含」敏感字根即命中(涵蓋 rootPassword/adminPassword/accessSecret/
    /// accessToken 這類 camelCase 複合鍵——舊版只匹配裸鍵名,複合鍵全數漏遮,實測踩到)。
    /// 值的部分容許跳脫字元(\")避免遮蔽被提前截斷。
    /// </summary>
    private static readonly Regex _jsonSensitiveKeyRegex = new(
        @"""(?<key>[^""]*(?:password|secret|token|authorization|api[_-]?key|credit[_-]?card|card[_-]?number)[^""]*)""(?<sep>\s*:\s*)""(?:[^""\\]|\\.)*""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// form-urlencoded 敏感鍵遮蔽(同上,鍵名包含敏感字根即命中)。
    /// </summary>
    private static readonly Regex _formSensitiveKeyRegex = new(
        @"(?<key>[\w.\-]*(?:password|secret|token|authorization|api[_-]?key|credit[_-]?card|card[_-]?number)[\w.\-]*)=[^&\s]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// query string 敏感鍵遮蔽(body 模式顯示 query 用):字根比 body 更廣——query 常見的
    /// OAuth code / reset token / signed URL signature / 各式 key 都收,寧可過遮不可漏遮
    /// (如 keyword 會被誤遮,除錯價值損失可接受;摘要模式則連值都不輸出,只列 key 名)。
    /// </summary>
    private static readonly Regex _querySensitiveKeyRegex = new(
        @"(?<key>[^&=?\s]*(?:password|secret|token|authorization|signature|code|key)[^&=?\s]*)=[^&\s]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseLoggingMiddleware> logger,
        IEnvironmentProvider environment,
        IOptions<RequestResponseLoggingOptions> options
    )
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkipLogging(context.Request.Path) || IsEventStreamRequest(context.Request))
        {
            await _next(context);
            return;
        }

        // body 記錄的雙重閘門:設定開 + 環境必須是 local(fail-safe:develop/production 誤設
        // true 也不輸出)。判定必須在任何緩衝/讀流之前——摘要模式完全不動 request/response stream。
        if (_options.LogBodies && _environment.IsLocal())
        {
            await LogWithBodiesAsync(context);
        }
        else
        {
            await LogSummaryOnlyAsync(context);
        }
    }

    private static bool ShouldSkipLogging(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();
        return path.StartsWithSegments("/api/abp", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/Abp", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase) ||
               pathValue == "/" ||
               pathValue?.Contains("swagger") == true ||
               pathValue?.Contains("health") == true ||
               pathValue?.Contains("metrics") == true ||
               pathValue?.Contains("favicon") == true;
    }

    /// <summary>
    /// 偵測 SSE 端點,跳過記錄與 body buffering(會破壞即時推送並導致 header sealed 問題)。
    /// 兩段偵測:(1) Accept header(browser EventSource 規範保證帶);
    /// (2) Path 結尾 <c>/events</c> fallback,避免 CORS preflight / proxy 把 header 弄丟時 miss。
    /// </summary>
    private static bool IsEventStreamRequest(HttpRequest request)
    {
        if (request.Headers.Accept.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = request.Path.Value;
        return !string.IsNullOrEmpty(path)
            && path.EndsWith("/events", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 摘要模式:不碰 request/response stream,只量測耗時並記單行摘要。
    /// 未被內層攔截的例外照常往外拋(交給外層例外處理);拋出前補一筆 500 摘要保留耗時軌跡。
    /// </summary>
    private async Task LogSummaryOnlyAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        catch (Exception)
        {
            stopwatch.Stop();
            LogSummary(context, StatusCodes.Status500InternalServerError, stopwatch.ElapsedMilliseconds);
            throw;
        }

        stopwatch.Stop();
        LogSummary(context, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// 單行結構化摘要:欄位固定 Method/Path/StatusCode/ElapsedMs/TraceId/ClientIp/QueryKeys
    /// (<c>TraceId</c> 欄位名維持穩定——後續 OTel/traceparent 收口沿用,別改名)。
    /// query 只列 <b>key 名集合</b>不列值(fail-safe:OAuth code / reset token / signature 這類
    /// secret 常走 query,denylist 遮值必有漏網;key 集合已足夠看出「打了什麼樣的請求」)。
    /// </summary>
    private void LogSummary(HttpContext context, int statusCode, long elapsedMs)
    {
        var logLevel = ComputeLogLevel(statusCode, elapsedMs);
        if (!_logger.IsEnabled(logLevel))
        {
            return;
        }

        var request = context.Request;
        _logger.Log(logLevel,
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms | TraceId: {TraceId} | ClientIp: {ClientIp} | QueryKeys: {QueryKeys}",
            request.Method,
            $"{request.Scheme}://{request.Host}{request.Path}",
            statusCode,
            elapsedMs,
            context.TraceIdentifier,
            GetClientIpAddress(context),
            GetQueryKeys(request));
    }

    /// <summary>
    /// 取 query 的 key 名集合(不含值)。
    /// </summary>
    private static string GetQueryKeys(HttpRequest request)
    {
        return request.Query.Count == 0
            ? string.Empty
            : string.Join(",", request.Query.Keys);
    }

    /// <summary>
    /// 等級矩陣(降噪核心):5xx 一律 Error(慢也不降);408/429 屬壓力訊號記 Warning;
    /// 達慢請求門檻升 Warning;其餘 4xx 屬正常業務拒絕記 Information;2xx/3xx 記 Debug。
    /// </summary>
    private LogLevel ComputeLogLevel(int statusCode, long elapsedMs)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogLevel.Error;
        }

        if (statusCode is StatusCodes.Status408RequestTimeout or StatusCodes.Status429TooManyRequests)
        {
            return LogLevel.Warning;
        }

        if (_options.SlowRequestThresholdMs > 0 && elapsedMs >= _options.SlowRequestThresholdMs)
        {
            return LogLevel.Warning;
        }

        if (statusCode >= StatusCodes.Status400BadRequest)
        {
            return LogLevel.Information;
        }

        return LogLevel.Debug;
    }

    /// <summary>
    /// body 模式(僅 local):維持原有「讀 request body + 緩衝 response body」流程與人讀框線輸出。
    /// </summary>
    private async Task LogWithBodiesAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await LogRequestAndResponseAsync(context, stopwatch);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            // response 還沒開始：log 後讓 ASP.NET 走預設錯誤處理（不要再呼 _next，
            // 會造成重複執行 controller）。
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.Log(
                    LogLevel.Error,
                    ex,
                    "記錄請求/回應時發生錯誤 - TraceId: {TraceId}",
                    context.TraceIdentifier);
            }

            throw;
        }
        catch (Exception ex)
        {
            // response 已 started → 不能再改 header 也不能再呼 _next，只能記錄。
            // 重打 _next 會觸發 "Headers are read-only, response has already started"。
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.Log(
                    LogLevel.Error,
                    ex,
                    "記錄請求/回應時發生錯誤（response 已開始，無法回頭）- TraceId: {TraceId}",
                    context.TraceIdentifier);
            }
        }
    }

    private async Task LogRequestAndResponseAsync(HttpContext context, Stopwatch stopwatch)
    {
        var traceId = context.TraceIdentifier;

        // 記錄請求
        var requestBody = await ReadRequestBodyAsync(context.Request);
        var clientIp = GetClientIpAddress(context);

        // 記錄回應
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();

        try
        {
            context.Response.Body = responseBodyStream;

            await _next(context);

            stopwatch.Stop();

            var responseBody = await ReadResponseBodyAsync(context.Response);

            // 統一的結構化日誌
            LogRequestResponse(
                traceId,
                context.Request,
                context.Response,
                requestBody,
                context.Response.StatusCode,
                responseBody,
                stopwatch.ElapsedMilliseconds,
                clientIp);
        }
        finally
        {
            await responseBodyStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }

    private void LogRequestResponse(
        string traceId,
        HttpRequest request,
        HttpResponse response,
        string? requestBody,
        int statusCode,
        string responseBody,
        long elapsedMs,
        string clientIp
    )
    {
        var logLevel = ComputeLogLevel(statusCode, elapsedMs);
        if (!_logger.IsEnabled(logLevel))
        {
            return;
        }

        var requestPath = $"{request.Scheme}://{request.Host}{request.Path}";
        var sanitizedRequestBody = SanitizeSensitiveData(requestBody);
        var sanitizedResponseBody = SanitizeSensitiveData(responseBody);

        var formattedRequestBody = IsJsonContentType(request.ContentType)
              ? TryFormatJson(sanitizedRequestBody)
              : sanitizedRequestBody;

        var formattedResponseBody = IsJsonContentType(response.ContentType)
            ? TryFormatJson(sanitizedResponseBody)
            : sanitizedResponseBody;

        // 截斷過長的 body
        var truncatedRequestBody = TruncateBody(formattedRequestBody);
        var truncatedResponseBody = TruncateBody(formattedResponseBody);

        // body 模式只在 local 成立(InvokeAsync 已閘),固定人讀框線格式;
        // 非 local 的結構化輸出一律走摘要模式,原「雲端結構化含 body」分支已成死路徑、移除。
        _logger.Log(logLevel,
            """

            ╔══════════════════════════════════════════════════════════════
            ║ 🔄 HTTP Transaction
            ╠══════════════════════════════════════════════════════════════
            ║ Trace ID      : {TraceId}
            ║ Client IP     : {ClientIp}
            ║ Method        : {Method}
            ║ Path          : {Path}
            ║ Query String  : {QueryString}
            ║ Status Code   : {StatusCode}
            ║ Duration      : {ElapsedMs} ms
            ╠══════════════════════════════════════════════════════════════
            ║ 📨 Request
            ╠══════════════════════════════════════════════════════════════
            ║ Content-Type  : {RequestContentType}
            ║ Body          : {RequestBody}
            ╠══════════════════════════════════════════════════════════════
            ║ 📤 Response
            ╠══════════════════════════════════════════════════════════════
            ║ Body          : {ResponseBody}
            ╚══════════════════════════════════════════════════════════════
            """,
            traceId,
            clientIp,
            request.Method,
            requestPath,
            // body 模式(僅 local)顯示 query 原文有除錯價值,但敏感 key 的值一樣要遮
            // (reset token / OAuth code 貼進 log 就是外洩,local 也不例外)。
            request.QueryString.HasValue ? SanitizeQueryString(request.QueryString.Value) : "(empty)",
            statusCode,
            elapsedMs,
            request.ContentType ?? "(none)",
            string.IsNullOrWhiteSpace(truncatedRequestBody) ? "(empty)" : truncatedRequestBody,
            string.IsNullOrWhiteSpace(truncatedResponseBody) ? "(empty)" : truncatedResponseBody
        );
    }

    private static string? SanitizeSensitiveData(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var result = _jsonSensitiveKeyRegex.Replace(
            input,
            static m => $"\"{m.Groups["key"].Value}\"{m.Groups["sep"].Value}\"***\"");
        result = _formSensitiveKeyRegex.Replace(
            result,
            static m => $"{m.Groups["key"].Value}=***");

        return result;
    }

    /// <summary>
    /// query string 遮蔽:敏感 key 的值換成 ***(key 名保留供除錯)。
    /// </summary>
    private static string? SanitizeQueryString(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return queryString;
        }

        return _querySensitiveKeyRegex.Replace(
            queryString,
            static m => $"{m.Groups["key"].Value}=***");
    }

    private string? TruncateBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        // 設定值防禦:<= 0 視為「不輸出 body 內容」(負值 clamp,不得讓 range 運算在請求中途炸掉——
        // logging 設定錯誤不可以弄壞請求本身)。
        if (_options.MaxBodyLogLength <= 0)
        {
            return $"(body omitted: MaxBodyLogLength <= 0, total length: {body.Length})";
        }

        if (body.Length <= _options.MaxBodyLogLength)
        {
            return body;
        }

        return $"{body[.._options.MaxBodyLogLength]}... (truncated, total length: {body.Length})";
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // 嘗試從各種 Header 取得真實 IP
        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? context.Request.Headers["X-Real-IP"].FirstOrDefault()
                 ?? context.Connection.RemoteIpAddress?.ToString();

        // 如果是 X-Forwarded-For,取第一個 IP
        if (!string.IsNullOrEmpty(ip) && ip.Contains(','))
        {
            ip = ip.Split(',')[0].Trim();
        }

        return ip ?? "Unknown";
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
        {
            return null;
        }

        // 只記錄特定 Content-Type
        var contentType = request.ContentType?.ToLowerInvariant();
        if (!IsLoggableContentType(contentType))
        {
            return $"[{contentType}] (binary content not logged)";
        }

        request.EnableBuffering();

        try
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(
                request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            return body;
        }
        catch (Exception)
        {
            request.Body.Position = 0;
            throw;
        }
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(response.Body, leaveOpen: true);
        var text = await reader.ReadToEndAsync();

        response.Body.Seek(0, SeekOrigin.Begin);

        return text;
    }

    private static bool IsLoggableContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        var loggableTypes = new[]
        {
            "application/json",
            "application/xml",
            "text/xml",
            "text/plain",
            "application/x-www-form-urlencoded"
        };

        return loggableTypes.Any(contentType.Contains);
    }

    private string? TryFormatJson(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        try
        {
            using var doc = JsonDocument.Parse(input);
            var text = System.Environment.NewLine + PrefixEachLine(JsonSerializer.Serialize(doc, _jsonSerializerOptions));

            return text;
        }
        catch
        {
            return input; // 不是 JSON 就原樣輸出
        }
    }

    private string PrefixEachLine(string text, string prefix = "║")
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lines = text.Split(System.Environment.NewLine);
        return string.Join(System.Environment.NewLine, lines.Select(line => $"{prefix}{line}"));
    }

    private static bool IsJsonContentType(string? contentType)
    {
        return contentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }
}
