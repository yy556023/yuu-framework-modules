using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Yuu.Redis.Extensions;

public static class YuuRedisHealthChecksBuilderExtensions
{
    /// <summary>
    /// 註冊 Redis health check(<c>AspNetCore.HealthChecks.Redis</c>,自建連線 PING);
    /// 只有 <see cref="YuuRedisConsts.ConfigurationKey"/> 有值時才註冊,未設定就不查 Redis(不套連線註冊的預設值)。
    /// <paramref name="tags"/> 由 Host 決定(例 readiness 用 <c>["ready"]</c>)。
    /// </summary>
    public static IHealthChecksBuilder AddYuuRedis(
        this IHealthChecksBuilder builder,
        IConfiguration configuration,
        string name = YuuRedisConsts.HealthCheckName,
        IEnumerable<string>? tags = null)
    {
        var redisConfiguration = configuration[YuuRedisConsts.ConfigurationKey];
        if (!string.IsNullOrWhiteSpace(redisConfiguration))
        {
            builder.AddRedis(redisConfiguration, name: name, tags: tags);
        }

        return builder;
    }
}
