using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serilog.Context;
using Yuu.Observability.Options;

namespace Yuu.Observability.Correlation;

/// <summary>
/// 框架無關的 correlation id：讀標頭、沒有就用 TraceId、推進 Serilog 的 LogContext。
///
/// <para>
/// **為什麼值取 TraceId 而不是另外產生 GUID**：那樣日誌、追蹤、稽核就會是三個互不相干的識別碼，
/// 從 Grafana 上一條慢 trace 想找對應的稽核記錄時得靠人腦拼。取同一個值之後，
/// 三邊用同一個字串就查得到，而且 TraceId 本來就會由 W3C <c>traceparent</c> 跨服務傳遞。
/// </para>
///
/// <para>
/// **與 ABP 的關係**：這個 middleware 會把值寫回 <c>Request.Headers</c>，所以 ABP 的
/// <c>ICorrelationIdProvider</c> 讀到的是同一個值，<c>AbpAuditLogs.CorrelationId</c> 照常運作。
/// ABP 自己的 Serilog enricher 照常運作也無妨——它推的是同一個值，
/// 不需要關掉任何東西。
/// </para>
///
/// <para>
/// 必須排在管線很前面（<c>UseRouting</c> 之前），否則前段的日誌不會帶上這個屬性。
/// </para>
/// </summary>
public class YuuCorrelationIdMiddleware : IMiddleware
{
    public const string LogPropertyName = "CorrelationId";

    private readonly YuuCorrelationIdOptions _options;

    public YuuCorrelationIdMiddleware(IOptions<YuuCorrelationIdOptions> options)
    {
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = ResolveCorrelationId(context);

        // 寫回請求標頭：下游元件（含 ABP 的 ICorrelationIdProvider）讀的是這裡。
        context.Request.Headers[_options.HeaderName] = correlationId;

        // 回應也帶上，呼叫端才有辦法拿著這個值來回報問題。
        context.Response.Headers[_options.HeaderName] = correlationId;

        using (LogContext.PushProperty(LogPropertyName, correlationId))
        {
            await next(context);
        }
    }

    private string ResolveCorrelationId(HttpContext context)
    {
        var fromHeader = context.Request.Headers[_options.HeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(fromHeader))
        {
            return fromHeader;
        }

        // Activity 由 ASP.NET Core 的 hosting 層在 middleware 之前建立，所以這裡拿得到。
        // 真的沒有（例如關掉了追蹤）才退回 GUID。
        var traceId = Activity.Current?.TraceId.ToString();

        return string.IsNullOrWhiteSpace(traceId) || traceId == default(ActivityTraceId).ToString()
            ? Guid.NewGuid().ToString("N")
            : traceId!;
    }
}
