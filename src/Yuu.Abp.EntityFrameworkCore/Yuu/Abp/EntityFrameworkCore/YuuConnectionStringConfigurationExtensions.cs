using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Yuu.Abp.EntityFrameworkCore;

/// <summary>
/// 把 <c>Databases</c> 區段的純值設定組成 <c>ConnectionStrings:{key}</c> 的 IConfiguration／IHostBuilder 擴充；須在 secrets json 之後呼叫，顯式字串優先。
/// </summary>
public static class YuuConnectionStringConfigurationExtensions
{
    /// <summary>
    /// 將 Databases 下各連線物件轉成同名的 ConnectionStrings，保留既有顯式設定。
    /// 同一連線的非敏感欄位與帳號密碼可分別放在 base 與 secrets json，由設定來源疊合。
    /// 必須在 appsettings.secrets.json 與其他設定來源加入後呼叫，才能取得疊加後的帳號、密碼與資料庫設定。
    /// 產生的 in-memory 設定是呼叫當下的快照，不隨來源重新載入而更新；section 不存在時不新增來源。
    /// </summary>
    public static IConfigurationBuilder AddYuuConnectionStrings(this IConfigurationBuilder builder, string sectionName = "Databases")
    {
        var configuration = builder.Build();
        var section = configuration.GetSection(sectionName);
        if (!section.Exists())
        {
            return builder;
        }

        var connectionStrings = new Dictionary<string, string?>();

        foreach (var connection in section.GetChildren())
        {
            var key = $"ConnectionStrings:{connection.Key}";
            if (configuration[key] is not null)
            {
                continue;
            }

            var providerValue = connection[nameof(YuuDatabaseConnectionOptions.Provider)];
            if (!Enum.TryParse<YuuDatabaseProvider>(providerValue, true, out var provider) ||
                !Enum.IsDefined(provider) || providerValue!.Contains(','))
            {
                throw new InvalidOperationException($"資料庫連線 {connection.Key} 的 Provider 缺少或無法解析。");
            }

            var options = new YuuDatabaseConnectionOptions();
            connection.Bind(options);
            connectionStrings[key] = YuuConnectionStringBuilder.Build(connection.Key, options);
        }

        return builder.AddInMemoryCollection(connectionStrings);
    }

    /// <summary>
    /// 在主機設定階段將 Databases 設定轉成 ConnectionStrings。
    /// 必須在加入 appsettings.secrets.json 的 ConfigureAppConfiguration 之後呼叫，才能讀到疊加後的敏感設定。
    /// 既有顯式連線字串優先；產生的設定是快照，不隨來源重新載入而更新。
    /// </summary>
    public static IHostBuilder AddYuuConnectionStrings(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureAppConfiguration((context, builder) =>
        {
            builder.AddYuuConnectionStrings();
        });
    }
}
