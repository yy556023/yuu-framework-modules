namespace Yuu.Observability.Options;

/// <summary>
/// correlation id 的設定（組態區段 <c>Observability:CorrelationId</c>）。
/// </summary>
public class YuuCorrelationIdOptions
{
    public const string SectionName = "Observability:CorrelationId";

    /// <summary>
    /// 標頭名稱。預設 <c>X-Correlation-Id</c>，與 ABP 的預設值一致——
    /// 改成別的名字會讓 ABP 的 <c>ICorrelationIdProvider</c> 讀不到，稽核記錄就會空白。
    /// 真要改，兩邊要一起改（ABP 那側是 <c>AbpCorrelationIdOptions.HttpHeaderName</c>）。
    /// </summary>
    public string HeaderName { get; set; } = "X-Correlation-Id";
}
