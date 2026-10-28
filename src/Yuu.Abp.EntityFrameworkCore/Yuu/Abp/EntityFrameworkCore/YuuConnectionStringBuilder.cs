using System;
using System.Globalization;
using System.Linq;

namespace Yuu.Abp.EntityFrameworkCore;

/// <summary>
/// 依資料庫提供者將純值設定組成連線字串。
/// </summary>
public static class YuuConnectionStringBuilder
{
    /// <summary>
    /// 建立指定資料庫的連線字串；Password 可為空，AdditionalOptions 原樣追加，由呼叫端負責格式。
    /// </summary>
    /// <exception cref="InvalidOperationException">Provider 無效，或缺少必要欄位；訊息包含連線 key 與欄位。</exception>
    /// <exception cref="NotSupportedException">指定的資料庫提供者尚未實作。</exception>
    public static string Build(string key, YuuDatabaseConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Provider switch
        {
            YuuDatabaseProvider.PostgreSql => BuildPostgreSql(key, options, options.Port ?? 5432),
            YuuDatabaseProvider.SqlServer => BuildSqlServer(key, options, options.Port ?? 1433),
            YuuDatabaseProvider.MySql => BuildMySql(key, options, options.Port ?? 3306),
            _ => throw new InvalidOperationException($"資料庫連線 {key} 的 Provider 缺少或無效。")
        };
    }

    private static string BuildPostgreSql(string key, YuuDatabaseConnectionOptions options, int port)
    {
        ValidateRequired(options.Host, key, nameof(options.Host));
        ValidateRequired(options.Username, key, nameof(options.Username));
        ValidateRequired(options.Database, key, nameof(options.Database));

        return $"Host={Quote(options.Host)};Port={port.ToString(CultureInfo.InvariantCulture)};" +
            $"Database={Quote(options.Database)};Username={Quote(options.Username)};Password={Quote(options.Password)};" +
            $"Include Error Detail={(options.IncludeErrorDetail ? "true" : "false")};{options.AdditionalOptions}";
    }

    private static string BuildSqlServer(string key, YuuDatabaseConnectionOptions options, int port)
    {
        throw new NotSupportedException($"資料庫連線 {key} 的 Provider {YuuDatabaseProvider.SqlServer} 尚未實作。");
    }

    private static string BuildMySql(string key, YuuDatabaseConnectionOptions options, int port)
    {
        throw new NotSupportedException($"資料庫連線 {key} 的 Provider {YuuDatabaseProvider.MySql} 尚未實作。");
    }

    private static void ValidateRequired(string value, string key, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"PostgreSQL 連線 {key} 缺少設定：{field}。");
        }
    }

    private static string Quote(string value)
    {
        // Npgsql 使用 ADO.NET 連線字串語法：雙引號內的雙引號以兩個雙引號跳脫。
        if (value.Any(c => char.IsWhiteSpace(c) || c is ';' or '=' or '\'' or '"'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
