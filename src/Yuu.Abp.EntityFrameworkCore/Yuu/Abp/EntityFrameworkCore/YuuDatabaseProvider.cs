namespace Yuu.Abp.EntityFrameworkCore;

/// <summary>
/// 資料庫提供者。
/// </summary>
public enum YuuDatabaseProvider : byte
{
    /// <summary>
    /// PostgreSQL（Npgsql），目前唯一完整實作的 provider。
    /// </summary>
    PostgreSql = 1,

    /// <summary>
    /// Microsoft SQL Server；連線字串組裝尚未實作，呼叫會丟 NotSupportedException。
    /// </summary>
    SqlServer = 2,

    /// <summary>
    /// MySQL／MariaDB；連線字串組裝尚未實作，呼叫會丟 NotSupportedException。
    /// </summary>
    MySql = 3
}
