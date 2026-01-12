using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Yuu.AspNetCore.Middleware.Logging;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private const int MaxBodyLogLength = 10000; // 避免過大的 body

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseLoggingMiddleware> logger,
        IHostEnvironment environment
    )
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await LogRequestAndResponseAsync(context, stopwatch);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.Log(
                    LogLevel.Error,
                    ex,
                    "記錄請求/回應時發生錯誤 - TraceId: {TraceId}",
                    context.TraceIdentifier);
            }

            await _next(context);
        }
    }

    private static bool ShouldSkipLogging(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();
        return pathValue == "/" ||
               pathValue?.Contains("swagger") == true ||
               pathValue?.Contains("health") == true ||
               pathValue?.Contains("metrics") == true ||
               pathValue?.Contains("favicon") == true;
    }

    private async Task LogRequestAndResponseAsync(HttpContext context, Stopwatch stopwatch)
    {
        var request = context.Request;
        var traceId = context.TraceIdentifier;

        // 記錄請求
        var requestBody = await ReadRequestBodyAsync(request);
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
                request,
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
        string? requestBody,
        int statusCode,
        string responseBody,
        long elapsedMs,
        string clientIp
    )
    {
        var requestPath = $"{request.Scheme}://{request.Host}{request.Path}";
        var sanitizedRequestBody = SanitizeSensitiveData(requestBody);
        var sanitizedResponseBody = SanitizeSensitiveData(responseBody);

        // 截斷過長的 body
        var truncatedRequestBody = TruncateBody(sanitizedRequestBody);
        var truncatedResponseBody = TruncateBody(sanitizedResponseBody);

        var logLevel = statusCode >= 400 ? LogLevel.Warning : LogLevel.Information;

        if (!_logger.IsEnabled(logLevel))
        {
            return;
        }

        // TODO: 待引用
        if (_environment.IsDevelopment())
        {
            // 本地 Seq: 使用格式化輸出,易於閱讀
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
                request.QueryString.HasValue ? request.QueryString.Value : "(empty)",
                statusCode,
                elapsedMs,
                request.ContentType ?? "(none)",
                string.IsNullOrWhiteSpace(truncatedRequestBody) ? "(empty)" : truncatedRequestBody,
                string.IsNullOrWhiteSpace(truncatedResponseBody) ? "(empty)" : truncatedResponseBody
            );
        }
        else
        {
            // 雲端環境 (ELK / GCP): 使用結構化日誌,便於查詢和分析
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = traceId,
                ["TransactionType"] = "HTTP",
                ["Environment"] = _environment.EnvironmentName
            }))
            {
                _logger.Log(logLevel,
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms | " +
                    "ClientIp: {ClientIp} | QueryString: {QueryString} | " +
                    "RequestContentType: {RequestContentType} | " +
                    "RequestBody: {RequestBody} | ResponseBody: {ResponseBody}",
                    request.Method,
                    requestPath,
                    statusCode,
                    elapsedMs,
                    clientIp,
                    request.QueryString.Value ?? string.Empty,
                    request.ContentType ?? string.Empty,
                    truncatedRequestBody ?? string.Empty,
                    truncatedResponseBody ?? string.Empty
                );
            }
        }
    }

    private static string? SanitizeSensitiveData(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        // 遮蔽敏感欄位
        var patterns = new Dictionary<string, string>
        {
            // JSON 格式
            [@"(""password""\s*:\s*"")[^""]*("")"] = "$1***$2",
            [@"(""token""\s*:\s*"")[^""]*("")"] = "$1***$2",
            [@"(""authorization""\s*:\s*"")[^""]*("")"] = "$1***$2",
            [@"(""secret""\s*:\s*"")[^""]*("")"] = "$1***$2",
            [@"(""apiKey""\s*:\s*"")[^""]*("")"] = "$1***$2",
            [@"(""api_key""\s*:\s*"")[^""]*("")"] = "$1***$2",
            [@"(""accessToken""\s*:\s*"")[^""]*("")"] = "$1***$2",
            [@"(""refreshToken""\s*:\s*"")[^""]*("")"] = "$1***$2",
            [@"(""creditCard""\s*:\s*"")[^""]*("")"] = "$1***$2",
            [@"(""cardNumber""\s*:\s*"")[^""]*("")"] = "$1***$2",

            // Form Data 格式
            [@"(password=)[^&\s]*"] = "$1***",
            [@"(token=)[^&\s]*"] = "$1***",
            [@"(api_key=)[^&\s]*"] = "$1***",
        };

        var result = input;
        foreach (var (pattern, replacement) in patterns)
        {
            result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);
        }

        return result;
    }

    private static string? TruncateBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        if (body.Length <= MaxBodyLogLength)
        {
            return body;
        }

        return $"{body[..MaxBodyLogLength]}... (truncated, total length: {body.Length})";
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
}
