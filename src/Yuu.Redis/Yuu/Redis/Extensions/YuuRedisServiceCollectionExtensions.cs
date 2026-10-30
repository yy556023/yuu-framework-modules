using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Yuu.Redis.Extensions;

public static class YuuRedisServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 singleton <see cref="IConnectionMultiplexer"/>(第一次取用時才連線)。
    /// <para>
    /// ABP <c>AbpCachingStackExchangeRedisModule</c> 只註冊 <c>IDistributedCache</c>,不曝露底層 multiplexer;
    /// 直接用 Redis 資料結構(hash、交易)的元件由這裡取得連線。
    /// 連線字串讀 <see cref="YuuRedisConsts.ConfigurationKey"/>,未設定時用 <see cref="YuuRedisConsts.DefaultConfiguration"/>;
    /// 連線選項見 <see cref="YuuRedisConfigurationOptions.Create"/>。
    /// </para>
    /// </summary>
    public static IServiceCollection AddYuuRedisConnection(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConfiguration = configuration[YuuRedisConsts.ConfigurationKey] ?? YuuRedisConsts.DefaultConfiguration;
        var options = YuuRedisConfigurationOptions.Create(redisConfiguration);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));

        return services;
    }
}
