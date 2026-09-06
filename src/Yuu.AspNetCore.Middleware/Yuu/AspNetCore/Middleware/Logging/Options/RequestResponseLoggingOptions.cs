namespace Yuu.AspNetCore.Middleware.Logging.Options;

/// <summary>
/// HTTP 請求/回應記錄的行為設定(configuration 區段 <see cref="SectionName"/>)。
/// 預設值必須是「安全側」:不記 body、截斷上限保守——記 body 是本地除錯專用行為,
/// 靠 <see cref="LogBodies"/> 與環境雙重閘門把關(見 middleware)。
/// </summary>
public sealed class RequestResponseLoggingOptions
{
    /// <summary>
    /// configuration 綁定區段名。
    /// </summary>
    public const string SectionName = "HttpLogging";

    /// <summary>
    /// 是否記錄 request/response body。
    /// 即使設 true,middleware 仍要求環境為 local 才會真的輸出(fail-safe:
    /// develop/production 誤設 true 也不得洩出 body)。
    /// </summary>
    public bool LogBodies { get; set; } = false;

    /// <summary>
    /// body 記錄的截斷上限(字元數);超過只留前段並標註總長。
    /// 設 0 或負值 = 不輸出 body 內容(只標註總長)——設定錯誤只影響 log 內容,不得弄壞請求。
    /// </summary>
    public int MaxBodyLogLength { get; set; } = 4096;

    /// <summary>
    /// 慢請求門檻(毫秒):elapsed 達門檻的請求以 Warning 記錄(5xx 仍為 Error)。
    /// 設 0 或負值 = 停用慢請求判定。
    /// </summary>
    public long SlowRequestThresholdMs { get; set; } = 5000;
}
