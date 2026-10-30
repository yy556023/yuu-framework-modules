using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using StackExchange.Redis;
using Volo.Abp;
using Xunit;
using Yuu.Redis.Extensions;

namespace Yuu.Redis;

/// <summary>
/// <see cref="YuuRedisServiceCollectionExtensions.AddYuuRedisConnection"/> / <see cref="YuuRedisModule"/> 的連線註冊,接本機真 Redis(<see cref="TestRedis"/>)。
/// 寫入只在測試 DB <see cref="TestRedis.Database"/>,每個案例結束清空該 DB。
/// </summary>
[Collection(TestRedisCollection.Name)]
public class YuuRedisConnectionTests
{
    private const string Key = "Yuu:Redis:Tests:Connection";

    [Fact]
    public void Create_should_keep_host_starting_when_redis_is_down()
    {
        var options = YuuRedisConfigurationOptions.Create("redis.example:6380,defaultDatabase=3");

        options.AbortOnConnectFail.ShouldBeFalse();
        options.BacklogPolicy.ShouldBe(BacklogPolicy.FailFast);
        options.EndPoints.Count.ShouldBe(1);
        options.EndPoints[0].ToString().ShouldBe("Unspecified/redis.example:6380");
        options.DefaultDatabase.ShouldBe(3);
    }

    [Fact]
    public async Task AddYuuRedisConnection_should_connect_to_configured_redis_async()
    {
        using var verifier = TestRedis.ConnectVerifier();
        TestRedis.FlushTestDatabase(verifier);
        try
        {
            await using var provider = BuildProvider(TestRedis.ConnectionString);
            var connectionMultiplexer = provider.GetRequiredService<IConnectionMultiplexer>();

            // 同一個 singleton,寫入落在連線字串指定的 DB。
            provider.GetRequiredService<IConnectionMultiplexer>().ShouldBeSameAs(connectionMultiplexer);
            await connectionMultiplexer.GetDatabase().StringSetAsync(Key, "written");

            ((string?)await verifier.GetDatabase(TestRedis.Database).StringGetAsync(Key)).ShouldBe("written");
        }
        finally
        {
            TestRedis.FlushTestDatabase(verifier);
        }
    }

    [Fact]
    public async Task AddYuuRedisConnection_should_not_throw_and_fail_fast_when_redis_is_down_async()
    {
        await using var provider = BuildProvider(TestRedis.UnavailableEndPoint());

        // AbortOnConnectFail = false:連不到也能取得 multiplexer(Host 照常啟動)。
        var connectionMultiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
        connectionMultiplexer.IsConnected.ShouldBeFalse();

        // FailFast:離線期間命令立即失敗,不排隊等重連。
        await Should.ThrowAsync<RedisConnectionException>(
            async () => await connectionMultiplexer.GetDatabase().StringSetAsync(Key, "never"));
    }

    [Fact]
    public async Task AddYuuRedisConnection_should_default_to_localhost_when_not_configured_async()
    {
        await using var provider = BuildProvider(null);

        // 只檢查連線目標,不對預設 DB(db 0,開發資料)下任何命令。
        var connectionMultiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
        ConfigurationOptions.Parse(connectionMultiplexer.Configuration).EndPoints[0].ToString()
            .ShouldBe("Unspecified/localhost:6379");
    }

    [Fact]
    public async Task Module_should_register_connection_from_configuration_async()
    {
        using var verifier = TestRedis.ConnectVerifier();
        TestRedis.FlushTestDatabase(verifier);
        try
        {
            using var application = await AbpApplicationFactory.CreateAsync<YuuRedisModule>(options =>
            {
                options.Services.ReplaceConfiguration(TestRedis.CreateConfiguration(TestRedis.ConnectionString));
            });
            await application.InitializeAsync();

            var connectionMultiplexer = application.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            await connectionMultiplexer.GetDatabase().StringSetAsync(Key, "module");

            ((string?)await verifier.GetDatabase(TestRedis.Database).StringGetAsync(Key)).ShouldBe("module");

            await application.ShutdownAsync();
        }
        finally
        {
            TestRedis.FlushTestDatabase(verifier);
        }
    }

    private static ServiceProvider BuildProvider(string? redisConfiguration)
    {
        var services = new ServiceCollection();
        services.AddYuuRedisConnection(TestRedis.CreateConfiguration(redisConfiguration));
        return services.BuildServiceProvider();
    }
}
