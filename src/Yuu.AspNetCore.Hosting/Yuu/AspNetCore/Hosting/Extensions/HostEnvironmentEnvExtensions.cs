using Microsoft.Extensions.Hosting;

namespace Yuu.AspNetCore.Hosting.Extensions;

/// <summary>
/// Extension methods for <see cref="IHostEnvironment"/>.
/// </summary>
public static class HostEnvironmentEnvExtensions
{
    public static bool IsLocal(this IHostEnvironment hostEnvironment)
    {
        return hostEnvironment.EnvironmentName.Contains("local");
    }

    public static bool IsDev(this IHostEnvironment hostEnvironment)
    {
        return hostEnvironment.EnvironmentName.Contains("dev");
    }

    public static bool IsStag(this IHostEnvironment hostEnvironment)
    {
        return hostEnvironment.EnvironmentName.Contains("stag");
    }

    public static bool IsProd(this IHostEnvironment hostEnvironment)
    {
        return hostEnvironment.EnvironmentName.Contains("prod");
    }
}
