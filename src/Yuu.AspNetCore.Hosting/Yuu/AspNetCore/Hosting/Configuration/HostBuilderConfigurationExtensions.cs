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

            var fileName = string.IsNullOrWhiteSpace(envName) ? "appsettings.json" : $"appsettings.{envName}.json";

            builder.AddJsonFile(
                path: fileName,
                optional: optional,
                reloadOnChange: reloadOnChange);
        });
    }
}
