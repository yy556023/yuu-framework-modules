using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yuu.AspNetCore.Middleware.Logging.Options;
using Yuu.AspNetCore.Middleware.Logging;

namespace Yuu.AspNetCore.Extensions;

public static class RequestResponseLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestResponseLoggingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestResponseLoggingMiddleware>();
    }

    /// <summary>
    /// 綁定 <see cref="RequestResponseLoggingOptions"/>(configuration 區段 <c>HttpLogging</c>)。
    /// Host 未呼叫時 middleware 仍以安全預設運作(不記 body),此註冊只是讓設定檔可調參。
    /// </summary>
    public static IServiceCollection AddRequestResponseLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RequestResponseLoggingOptions>(
            configuration.GetSection(RequestResponseLoggingOptions.SectionName));

        return services;
    }
}
