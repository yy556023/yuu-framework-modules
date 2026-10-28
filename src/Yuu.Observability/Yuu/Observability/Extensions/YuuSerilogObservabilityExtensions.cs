using System;
using System.Collections.Generic;
using System.Reflection;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;
using Yuu.Environment.Provider.Extensions;
using Yuu.Observability.Serilog;

namespace Yuu.Observability.Extensions;

public static class YuuSerilogObservabilityExtensions
{
    /// <summary>
    /// 把 TraceId／SpanId 補進每一筆日誌。
    ///
    /// <para>
    /// **只補這兩個。** CorrelationId 由 <c>YuuCorrelationIdMiddleware</c> 推進 <c>LogContext</c>，
    /// TenantId／UserId／ClientId 在 ABP 應用裡由 <c>AbpSerilogMiddleware</c> 負責，
    /// 兩者都需要 Serilog 設定裡有 <c>Enrich.FromLogContext()</c>。
    /// </para>
    ///
    /// <para>
    /// 不需要 DI，bootstrap logger 階段就能用——讀的是 <c>Activity.Current</c>，
    /// 那是 .NET 執行階段維護的 AsyncLocal。
    /// </para>
    /// </summary>
    public static LoggerConfiguration EnrichWithTraceContext(this LoggerConfiguration configuration)
    {
        return configuration.Enrich.With(new TraceContextEnricher());
    }

    /// <summary>
    /// Console：**永遠是人讀格式**，只有等級隨環境調整。
    ///
    /// <para>
    /// 這裡刻意不輸出 JSON。Console 的讀者是人——本機是開發者盯著終端機，
    /// 叢集上是 <c>kubectl logs</c> 或 Pod 畫面。把 JSON 印在這裡兩邊都難讀，
    /// 而且沒有換到任何查詢能力：真正要查詢的是後端，那條路走
    /// <see cref="WriteToOtlpLogs"/>。
    /// </para>
    ///
    /// <para>
    /// 等級只是**預設值**：local 開到 Debug（開發時要看細節），其他環境 Information
    /// （Debug 在正式流量下會把有用的訊息淹掉，也是實質成本）；空環境名也使用 Information。
    /// 呼叫端給了 <paramref name="minimumLevel"/> 就一律以它為準，不再看環境——
    /// 「正式環境臨時要開 Debug 追一個問題」這種需求不該逼人改框架。
    /// </para>
    /// </summary>
    /// <param name="minimumLevel">
    /// 明確指定最低等級。給了就蓋掉環境推斷；不給（<c>null</c>）才走 local→Debug／其餘→Information。
    /// </param>
    /// <param name="environmentName">明確指定環境；不給就讀 <c>ASPNETCORE_ENVIRONMENT</c>。</param>
    public static LoggerConfiguration WriteToEnvironmentConsole(
        this LoggerConfiguration configuration,
        LogEventLevel? minimumLevel = null,
        string? environmentName = null)
    {
        var level = minimumLevel ?? ResolveDefaultLevel(environmentName);

        // Async 包起來：Console 寫入是同步 IO，高流量時會擋住請求執行緒。
        return configuration.WriteTo.Async(sink => sink.Console(restrictedToMinimumLevel: level));
    }

    /// <summary>
    /// 沒有明確指定時的等級推斷：local 要細節，其他環境求訊噪比。
    /// </summary>
    private static LogEventLevel ResolveDefaultLevel(string? environmentName)
    {
        var envName = environmentName
                      ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return YuuEnvironmentExtensions.IsLocal(envName)
            ? LogEventLevel.Debug
            : LogEventLevel.Information;
    }

    /// <summary>
    /// 結構化日誌走 OTLP 送到收集端，那才是能查詢的地方。
    ///
    /// <para>
    /// 走 OTLP 而不是「印 JSON 到 stdout 讓 collector 撿」，差別在於
    /// trace 關聯是**協定層面**保證的：這個 sink 會直接帶上目前 Activity 的
    /// trace_id／span_id，後端不必再從文字裡剖析。日誌與追蹤在 Grafana 上
    /// 是同一組識別碼，可以互跳。
    /// </para>
    ///
    /// <para>
    /// <paramref name="otlpEndpoint"/> 留空就完全不掛這個 sink——
    /// 本機開發不需要先跑一個 collector 才能啟動。
    /// </para>
    /// </summary>
    /// <param name="minimumLevel">
    /// 明確指定最低等級，語意同 <see cref="WriteToEnvironmentConsole"/>。
    /// 這條路徑的成本比 Console 高（網路流量與後端儲存），要壓等級通常先壓這裡。
    /// </param>
    public static LoggerConfiguration WriteToOtlpLogs(
        this LoggerConfiguration configuration,
        string? otlpEndpoint,
        string? serviceName = null,
        string? serviceVersion = null,
        LogEventLevel? minimumLevel = null,
        string? environmentName = null)
    {
        if (string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            return configuration;
        }

        var envName = environmentName
                      ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        var level = minimumLevel ?? ResolveDefaultLevel(environmentName);

        var attributes = new Dictionary<string, object>
        {
            // 這幾個必須與 AddYuuObservability 送出的 resource 逐字一致，
            // 否則 Grafana 上 logs 與 traces 會被當成兩個不同的服務，互跳就斷了。
            ["service.name"] = ResolveServiceName(serviceName),

            ["service.instance.id"] = System.Environment.MachineName,
        };

        // 未指定環境就不送此屬性，避免把未知環境誤標為 local。
        if (!string.IsNullOrWhiteSpace(envName))
        {
            attributes["deployment.environment"] = envName;
        }

        // 版本沒給就**不送這個屬性**。送一個空字串不是「未知」，
        // 而是「這個服務的版本是空字串」，後端照樣會據此分組。
        if (!string.IsNullOrWhiteSpace(serviceVersion))
        {
            attributes["service.version"] = serviceVersion;
        }

        return configuration.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = otlpEndpoint;
            options.Protocol = OtlpProtocol.Grpc;
            options.RestrictedToMinimumLevel = level;
            options.ResourceAttributes = attributes;
        });
    }

    /// <summary>
    /// 服務名稱的推斷鏈，**必須與 <c>AddYuuObservability</c> 完全相同**：
    /// 明確指定 → 進入點組件名 → <c>unknown-service</c>。
    ///
    /// <para>
    /// 兩邊若不一致，「沒設定 ServiceName」這個最常見的情境就會讓 traces 掛在組件名下、
    /// logs 掛在 unknown-service 下，變成後端看起來有兩個服務。
    /// </para>
    /// </summary>
    private static string ResolveServiceName(string? serviceName)
    {
        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            return serviceName;
        }

        return Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown-service";
    }
}
