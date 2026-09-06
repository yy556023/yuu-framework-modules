using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Yuu.AspNetCore.Hosting.Configuration;

public static class HostBuilderConfigurationExtensions
{
    public static IHostBuilder AddAppSettingsEnvsJson(
        this IHostBuilder hostBuilder,
        string? envName = null,
        // 預設不允許缺檔：環境名打錯（例如把 develop 打成 Develop）如果放行，
        // 會靜默地只吃 appsettings.json 就上線，那比啟動失敗危險得多。
        bool optional = false,
        bool reloadOnChange = true)
    {
        return hostBuilder.ConfigureAppConfiguration((context, builder) =>
        {
            envName ??= System.Environment.GetEnvironmentVariable(EnvironmentVariables.AspNetCoreEnvironment);

            // 沒有環境變數就是 local，而 local 的設定就寫在 appsettings.json 本身，
            // 那份已由主機的預設來源載入，這裡不必再疊任何東西
            // （原本這種情況會再加一次 appsettings.json，等於多做一次檔案監看）。
            if (string.IsNullOrWhiteSpace(envName))
            {
                return;
            }

            // 疊在 appsettings.json 之上、只覆寫同名的 key，所以環境檔只要寫「與 base 不同」的部分。
            builder.AddJsonFile(
                path: $"appsettings.{envName}.json",
                optional: optional,
                reloadOnChange: reloadOnChange);
        });
    }
}
