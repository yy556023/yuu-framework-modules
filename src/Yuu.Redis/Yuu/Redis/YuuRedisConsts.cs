namespace Yuu.Redis;

/// <summary>
/// Redis 連線的設定鍵與預設值。
/// </summary>
public static class YuuRedisConsts
{
    /// <summary>
    /// StackExchange.Redis 連線字串的設定鍵;與 ABP <c>AbpCachingStackExchangeRedisModule</c> 讀的是同一個鍵,
    /// 分散式快取與 <c>IConnectionMultiplexer</c> 連同一台 Redis。
    /// </summary>
    public const string ConfigurationKey = "Redis:Configuration";

    /// <summary>
    /// 未設定 <see cref="ConfigurationKey"/> 時的連線字串(只用於連線註冊;health check 未設定就不註冊)。
    /// </summary>
    public const string DefaultConfiguration = "localhost:6379";

    /// <summary>
    /// health check 預設名稱。
    /// </summary>
    public const string HealthCheckName = "redis";
}
