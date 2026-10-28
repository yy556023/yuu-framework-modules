using Microsoft.Extensions.Hosting;

namespace Yuu.AspNetCore.Hosting.Configuration;

/// <summary>
/// 提供主機建置器的環境設定擴充方法。
/// </summary>
public static class HostBuilderConfigurationExtensions
{
    /// <summary>
    /// 疊加環境設定與環境機密設定；local 環境不新增任何來源。
    /// </summary>
    /// <param name="hostBuilder">
    /// 已載入基礎設定來源的建置器。
    /// </param>
    /// <param name="envName">
    /// 環境名稱；未指定時讀取 ASPNETCORE_ENVIRONMENT。
    /// </param>
    /// <param name="optional">
    /// 是否允許環境設定檔不存在；預設不允許，避免環境名稱打錯時靜默使用基礎設定。
    /// 環境機密設定檔一律允許不存在。
    /// </param>
    /// <param name="reloadOnChange">
    /// 是否在環境設定檔或環境機密設定檔變更時重新載入。
    /// </param>
    /// <returns>
    /// 原建置器。
    /// </returns>
    public static IHostBuilder AddAppSettingsEnvsJson(
        this IHostBuilder hostBuilder,
        string? envName = null,
        bool optional = false,
        bool reloadOnChange = true)
    {
        return hostBuilder.ConfigureAppConfiguration((context, builder) =>
        {
            builder.AddAppSettingsEnvsJson(envName, optional, reloadOnChange);
        });
    }
}
