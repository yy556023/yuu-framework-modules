using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Yuu.Observability.Serilog;

/// <summary>
/// 把目前 <see cref="Activity"/> 的 TraceId／SpanId 寫進每一筆日誌。
///
/// <para>
/// 這是「日誌」與「追蹤」之間唯一的橋：有了它，Grafana 上看到一條慢請求的 trace，
/// 才能用同一個 TraceId 直接跳到那次請求的所有日誌。
/// 沒有它，兩邊就是兩份無法對照的資料。
/// </para>
///
/// <para>
/// TraceId 由 W3C <c>traceparent</c> 標頭跨服務傳遞，所以 Yuu.Sample 打到
/// Yuu.AuthServer 拿 token 的那一段，兩邊的日誌會帶同一個 TraceId。
/// </para>
/// </summary>
public class TraceContextEnricher : ILogEventEnricher
{
    public const string TraceIdPropertyName = "TraceId";
    public const string SpanIdPropertyName = "SpanId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;

        // 背景工作與啟動階段沒有 Activity，這時候不塞空值——
        // 塞了只會讓每筆日誌多兩個永遠是空的欄位。
        if (activity == null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty(TraceIdPropertyName, activity.TraceId.ToString()));

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty(SpanIdPropertyName, activity.SpanId.ToString()));
    }
}
