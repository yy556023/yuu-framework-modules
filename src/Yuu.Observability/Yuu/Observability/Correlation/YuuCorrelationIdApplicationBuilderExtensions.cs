using Microsoft.AspNetCore.Builder;

namespace Yuu.Observability.Correlation;

public static class YuuCorrelationIdApplicationBuilderExtensions
{
    /// <summary>
    /// 掛上統一規格的 correlation id。
    ///
    /// <para>
    /// 位置要**盡量靠前**（<c>UseRouting()</c> 之前）：middleware 是巢狀執行的，
    /// 排在它之前的元件所寫的日誌不會帶上這個屬性。
    /// </para>
    ///
    /// <para>
    /// ABP 應用不需要再呼叫 <c>app.UseCorrelationId()</c>——ABP 的
    /// <c>ICorrelationIdProvider</c> 讀的是同一個標頭，而這裡已經保證標頭一定存在。
    /// 兩個都掛不會壞，但等於做兩次同樣的事。
    /// </para>
    /// </summary>
    public static IApplicationBuilder UseYuuCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<YuuCorrelationIdMiddleware>();
    }
}
