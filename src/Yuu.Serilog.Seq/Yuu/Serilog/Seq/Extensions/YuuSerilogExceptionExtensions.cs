using Serilog;
using Yuu.Serilog.Seq.Exceptions;

namespace Yuu.Serilog.Seq.Extensions;

/// <summary>
/// Serilog 例外事件瘦身設定（OPS-LOG-EVENT-SIZE-01，2026-10-19；自 clouds-admin-api 上移，Admin／User Host 共用）。
/// <para>
/// 三層處理，缺一層都可能讓 Seq 再次收到「Event JSON representation exceeds the byte size limit」占位事件：
/// <list type="number">
///   <item>根治：樂觀併發衝突本身（CloudImageVariant、OpenIddictToken 等）由各服務另案處理，本設定不改業務行為。</item>
///   <item>來源瘦身（本類別）：EF <c>DbUpdateException</c> 改用 <see cref="DbUpdateExceptionSummaryDestructurer"/>，
///     只留實體型別、狀態與主鍵；其餘結構化屬性的字串與集合加上長度上限，避免 <c>Exception.Data</c> 或
///     大型參數把單一事件撐爆。</item>
///   <item>最後防線：Yuu.Serilog.Seq 的 <c>UseSeqAsync</c> 預設把 Seq sink 事件上限放寬到 1 MB。</item>
/// </list>
/// </para>
/// </summary>
public static class YuuSerilogExceptionExtensions
{
    /// <summary>
    /// 結構化屬性中單一字串的最大長度（字元）預設值。人讀訊息、堆疊（<c>@x</c>）不受此限；
    /// 被截斷的通常是 <c>Exception.Data</c> 或誤傳整份 payload 的參數，8K 字元已足以判讀。
    /// </summary>
    public const int DefaultMaxStringLength = 8 * 1024;

    /// <summary>
    /// 結構化屬性中單一集合／字典的最大元素數預設值。
    /// </summary>
    public const int DefaultMaxCollectionCount = 64;

    /// <summary>
    /// 例外詳情解構深度預設值；Serilog.Exceptions 預設 10 層會沿導覽屬性一路展開，
    /// 5 層已涵蓋例外與兩層 InnerException 的共通欄位。
    /// </summary>
    public const int DefaultMaxDepth = 5;

    /// <summary>
    /// 套用例外詳情 enricher（含 EF 例外摘要解構，衍生型別亦同）與結構化屬性的字串／集合長度上限。
    /// 取代直接呼叫 <c>Enrich.WithExceptionDetails()</c>，兩者不要同時掛，否則 ExceptionDetail 會被寫兩次。
    /// </summary>
    public static LoggerConfiguration UseYuuExceptionDetails(
        this LoggerConfiguration configuration,
        int maxStringLength = DefaultMaxStringLength,
        int maxCollectionCount = DefaultMaxCollectionCount,
        int maxDepth = DefaultMaxDepth)
    {
        // 不用 Enrich.WithExceptionDetails(options)：它的解構器只精確比對型別，DbUpdateException 子類別會漏掉，
        // 改用 YuuExceptionDetailsEnricher 在執行期補登衍生型別。
        return configuration
            .Destructure.ToMaximumStringLength(maxStringLength)
            .Destructure.ToMaximumCollectionCount(maxCollectionCount)
            .Enrich.With(new YuuExceptionDetailsEnricher(maxDepth));
    }
}
