using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Yuu.Observability.Options;

namespace Yuu.Observability.Extensions;

public static class YuuObservabilityExtensions
{
    /// <summary>
    /// 掛上 OpenTelemetry 的追蹤與指標。
    ///
    /// <para>
    /// 日誌**不走這裡**：本生態系用 Serilog，結構化日誌由
    /// <c>WriteToOtlpLogs()</c> 直接以 OTLP 送到同一個收集端，Console 則永遠留給人讀。
    /// 硬要在這裡再開 OTel 的 logging provider，會變成同一筆訊息送兩次。
    /// </para>
    /// </summary>
    public static IServiceCollection AddYuuObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(YuuObservabilityOptions.SectionName);
        services.Configure<YuuObservabilityOptions>(section);

        var options = section.Get<YuuObservabilityOptions>() ?? new YuuObservabilityOptions();
        var serviceName = options.ServiceName
                          ?? Assembly.GetEntryAssembly()?.GetName().Name
                          ?? "unknown-service";

        var builder = services.AddOpenTelemetry().ConfigureResource(resource => resource
            .AddService(
                serviceName: serviceName,
                serviceVersion: options.ServiceVersion,
                // 同一個服務的多個副本要能分辨，否則指標會被混在一起。
                // 完整限定：本組件也引用了 Yuu.Environment.*，裸寫 Environment 會被解析成那個命名空間。
                serviceInstanceId: System.Environment.MachineName));

        if (options.TracingEnabled)
        {
            builder.WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio))
                    .AddAspNetCoreInstrumentation(instrumentation =>
                    {
                        // 探針與 metrics 端點每幾秒被打一次，採進來只會淹沒真正的請求。
                        instrumentation.Filter = context =>
                            !options.ExcludedPaths.Any(path =>
                                context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));

                        instrumentation.RecordException = true;
                    })
                    // 下游呼叫也要進來，跨服務的 trace 才不會斷在邊界上。
                    // W3C traceparent 由 HttpClient instrumentation 自動帶上，不需要自己塞 header。
                    .AddHttpClientInstrumentation(instrumentation =>
                    {
                        instrumentation.RecordException = true;
                    })
                    // 資料庫用 Npgsql 自己的 ActivitySource：
                    // OpenTelemetry.Instrumentation.EntityFrameworkCore 目前沒有穩定版，
                    // 而 Npgsql 這條路徑同樣看得到 SQL 執行時間，且是官方維護。
                    .AddNpgsql();

                foreach (var source in options.AdditionalSources)
                {
                    tracing.AddSource(source);
                }

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(options.OtlpEndpoint!));
                }
            });
        }

        if (options.MetricsEnabled)
        {
            builder.WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    // GC、執行緒池、例外次數——.NET 服務最早出問題的地方通常在這裡，
                    // 而不是在業務程式碼裡。
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(options.OtlpEndpoint!));
                }
            });
        }

        return services;
    }
}
