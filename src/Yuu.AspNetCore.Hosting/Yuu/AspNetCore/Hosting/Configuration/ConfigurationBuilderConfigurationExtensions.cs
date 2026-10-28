using System;
using Microsoft.Extensions.Configuration;
using Yuu.AspNetCore.Hosting.Environments;

namespace Yuu.AspNetCore.Hosting.Configuration;

/// <summary>
/// 提供設定建置器的環境設定擴充方法。
/// </summary>
public static class ConfigurationBuilderConfigurationExtensions
{
    /// <summary>
    /// 疊加環境設定與環境機密設定；local 環境不新增任何來源。
    /// </summary>
    /// <param name="builder">
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
    /// <remarks>
    /// 呼叫端先載入 base appsettings.json、appsettings.secrets.json 與環境變數，
    /// 本方法再載入 env json 與 env secrets，完整順序為 base -> secrets -> 環境變數 -> env json -> env secrets。
    /// env secrets 最後疊加，因此相同 key 以它為準。
    /// 本機執行 develop 等非 local 環境時，可將該環境帳密放入被 Git 忽略的 appsettings.{envName}.secrets.json，
    /// base appsettings.secrets.json 只放 local 帳密。
    /// 叢集由 Secret 掛載 appsettings.secrets.json，不會有環境別機密檔，因此 env secrets 一律為 optional。
    /// 環境名稱空白或包含 local（不區分大小寫）時提前返回，不疊加任何檔案。
    /// </remarks>
    public static IConfigurationBuilder AddAppSettingsEnvsJson(
        this IConfigurationBuilder builder,
        string? envName = null,
        bool optional = false,
        bool reloadOnChange = false)
    {
        envName ??= System.Environment.GetEnvironmentVariable(EnvironmentVariables.AspNetCoreEnvironment);

        // 空環境名沒有可載入的環境檔；local 則沿用基礎設定。兩者都不疊加，但空環境名不算 local。
        if (string.IsNullOrWhiteSpace(envName) || envName.Contains(YuuEnvironmentConsts.Local.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return builder;
        }

        builder.AddJsonFile(
            path: $"appsettings.{envName}.json",
            optional: optional,
            reloadOnChange: reloadOnChange);

        return builder.AddJsonFile(
            path: $"appsettings.{envName}.secrets.json",
            optional: true,
            reloadOnChange: reloadOnChange);
    }
}
