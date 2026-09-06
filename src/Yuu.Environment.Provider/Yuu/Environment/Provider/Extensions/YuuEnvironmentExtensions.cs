using Microsoft.Extensions.Hosting;

namespace Yuu.Environment.Provider.Extensions;

/// <summary>
/// Extension methods for <see cref="IHostEnvironment"/>.
/// </summary>
public static class YuuEnvironmentExtensions
{
    public static bool IsLocal(this IHostEnvironment hostEnvironment)
    {
        return string.IsNullOrEmpty(hostEnvironment.EnvironmentName);
    }

    public static bool IsDev(this IHostEnvironment hostEnvironment)
    {
        return hostEnvironment.EnvironmentName.Contains("dev") || hostEnvironment.EnvironmentName.Contains("develop");
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
