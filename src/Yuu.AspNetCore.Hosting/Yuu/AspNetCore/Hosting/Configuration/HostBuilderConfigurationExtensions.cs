using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Yuu.AspNetCore.Hosting.Configuration;

public static class HostBuilderConfigurationExtensions
{
    public static IHostBuilder AddAppSettingsEnvsJson(
        this IHostBuilder hostBuilder,
        string? envName = null,
        bool optional = true,
        bool reloadOnChange = true)
    {
        return hostBuilder.ConfigureAppConfiguration((context, builder) =>
        {
            envName ??= context.HostingEnvironment.EnvironmentName;

            builder.AddJsonFile(
                path: $"appsettings.{envName}.json",
                optional: optional,
                reloadOnChange: reloadOnChange);
        });
    }
}
