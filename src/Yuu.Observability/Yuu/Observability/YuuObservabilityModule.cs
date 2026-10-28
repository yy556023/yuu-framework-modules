using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Yuu.Observability.Correlation;
using Yuu.Observability.Extensions;
using Yuu.Observability.Options;

namespace Yuu.Observability;

/// <summary>
/// 可觀測性模組。
///
/// <para>
/// 用法：Host 模組的 <c>[DependsOn]</c> 加上這個型別，再於管線最前面呼叫
/// <c>app.UseYuuCorrelationId()</c>。日誌那一半要寫在 <c>Program.cs</c>
/// （<c>EnrichWithTraceContext()</c>、<c>WriteToEnvironmentConsole()</c> 與
/// <c>WriteToOtlpLogs()</c>）——Serilog 的 logger 建在 ABP 容器之前，模組介入不了，
/// 所以 OTLP 位址要自己從 <c>IConfiguration</c> 讀 <c>Observability:OtlpEndpoint</c>。
/// </para>
///
/// <para>
/// **與 ABP 的 correlation id 不會打架**：<see cref="YuuCorrelationIdMiddleware"/>
/// 把值寫進請求標頭，ABP 的 <c>ICorrelationIdProvider</c> 讀的就是同一個標頭，
/// 所以兩邊得到同一個字串。ABP 的 Serilog enricher 照常運作、稽核記錄照常寫入，
/// 只是那個值現在等於 TraceId——這正是「統一規格」要的效果，不需要關掉 ABP 的任何東西。
/// </para>
/// </summary>
public class YuuObservabilityModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        context.Services.AddYuuObservability(configuration);

        context.Services.Configure<YuuCorrelationIdOptions>(
            configuration.GetSection(YuuCorrelationIdOptions.SectionName));

        // IMiddleware 實作要自己註冊到容器，ABP 的慣例註冊不會撿它。
        context.Services.AddTransient<YuuCorrelationIdMiddleware>();
    }
}
