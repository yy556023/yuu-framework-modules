using System;

namespace Yuu.Observability.Options;

/// <summary>
/// 可觀測性設定（組態區段 <c>Observability</c>）。
///
/// <para>
/// 預設值刻意是「不送任何東西出去」：沒有設定 <see cref="OtlpEndpoint"/> 時
/// 只會建立 ActivitySource 與 Meter，不掛 exporter。這讓本機開發不需要先跑一個
/// collector 才能啟動，而部署環境只要給一個位址就會開始送。
/// </para>
/// </summary>
public class YuuObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// 服務名稱，會成為 trace／metric 上的 <c>service.name</c>。
    /// 不給就用進入點組件的名稱——那通常就是 Host 專案名，足以區分兩台服務。
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// 服務版本，對應 <c>service.version</c>。建議由 CI 帶入映像 tag，
    /// 這樣 Grafana 上就能把一段異常對應到某一次部署。
    /// </summary>
    public string? ServiceVersion { get; set; }

    /// <summary>
    /// OTLP 收集端位址（例如 <c>http://opentelemetry-collector.observability.svc.cluster.local:4317</c>）。
    /// **留空就完全不匯出**，見類別註解。
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// 追蹤取樣比例，0～1。預設 1（全採）。
    ///
    /// <para>
    /// 正式環境要調低時請注意：取樣是**整條 trace** 的決定，不是每個 span 各自決定，
    /// 所以調成 0.1 之後看到的是「完整的一成請求」而不是「每條請求的一成片段」。
    /// </para>
    /// </summary>
    public double TraceSampleRatio { get; set; } = 1.0;

    public bool TracingEnabled { get; set; } = true;

    public bool MetricsEnabled { get; set; } = true;

    /// <summary>
    /// 額外要監聽的 ActivitySource 名稱，用於自訂的 <see cref="System.Diagnostics.ActivitySource"/>。
    /// </summary>
    public string[] AdditionalSources { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 不要追蹤的請求路徑前綴。預設排除探針與 metrics 端點——
    /// 它們每幾秒就被打一次，全採會讓 trace 後端幾乎只剩雜訊。
    /// </summary>
    public string[] ExcludedPaths { get; set; } =
    [
        "/healthz",
        "/health",
        "/metrics",
    ];
}
