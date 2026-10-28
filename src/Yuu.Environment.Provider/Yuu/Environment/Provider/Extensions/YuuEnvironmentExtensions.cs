using System;
using Microsoft.Extensions.Hosting;

namespace Yuu.Environment.Provider.Extensions;

/// <summary>
/// Extension methods for <see cref="IHostEnvironment"/>.
/// </summary>
public static class YuuEnvironmentExtensions
{
    /// <summary>
    /// 環境名稱包含 local（不分大小寫）才算 local。
    /// 決定:空環境名不算 local,對齊 K2m,2026-10-29 使用者拍板
    ///
    /// <para>
    /// 提供字串多載，是因為有些情境拿不到 <see cref="IHostEnvironment"/>：
    /// Serilog 的 bootstrap logger 建立在 DI 容器之前，只能直接讀環境變數。
    /// 字串與主機環境多載共用規則；Hosting 遇到空環境名時仍不疊加環境檔，並不代表它屬於 local。
    /// </para>
    /// </summary>
    public static bool IsLocal(string? environmentName)
    {
        return !string.IsNullOrWhiteSpace(environmentName) && environmentName.Contains("local", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLocal(this IHostEnvironment hostEnvironment)
    {
        return IsLocal(hostEnvironment.EnvironmentName);
    }

    public static bool IsDev(this IHostEnvironment hostEnvironment)
    {
        return hostEnvironment.EnvironmentName.Contains("dev", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStag(this IHostEnvironment hostEnvironment)
    {
        return hostEnvironment.EnvironmentName.Contains("stag", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProd(this IHostEnvironment hostEnvironment)
    {
        return hostEnvironment.EnvironmentName.Contains("prod", StringComparison.OrdinalIgnoreCase);
    }
}
