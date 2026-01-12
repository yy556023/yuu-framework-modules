using Microsoft.AspNetCore.Builder;
using Yuu.AspNetCore.Middleware.Logging;

namespace Yuu.AspNetCore.Extensions;

public static class RequestResponseLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestResponseLoggingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestResponseLoggingMiddleware>();
    }
}
