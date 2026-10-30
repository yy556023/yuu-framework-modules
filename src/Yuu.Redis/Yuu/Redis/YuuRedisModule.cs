using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Yuu.Redis.Extensions;

namespace Yuu.Redis;

/// <summary>
/// Redis 連線模組:Host 模組的 <c>[DependsOn]</c> 加上這個型別,就會依 <see cref="YuuRedisConsts.ConfigurationKey"/>
/// 註冊 singleton <c>IConnectionMultiplexer</c>(<see cref="YuuRedisServiceCollectionExtensions.AddYuuRedisConnection"/>)。
/// health check 由 Host 在自己的 <c>AddHealthChecks()</c> 呼叫 <see cref="YuuRedisHealthChecksBuilderExtensions.AddYuuRedis"/>,
/// tag 與其他依賴(PostgreSQL 等)的組合留給 Host 決定。不含任何業務 key。
/// </summary>
public class YuuRedisModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddYuuRedisConnection(context.Services.GetConfiguration());
    }
}
