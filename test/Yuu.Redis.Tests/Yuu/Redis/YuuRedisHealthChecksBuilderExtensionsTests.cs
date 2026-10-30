using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using Yuu.Redis.Extensions;

namespace Yuu.Redis;

/// <summary>
/// <see cref="YuuRedisHealthChecksBuilderExtensions.AddYuuRedis"/>:接本機真 Redis 與真實不可用端點,health check 只 PING、不寫資料。
/// </summary>
[Collection(TestRedisCollection.Name)]
public class YuuRedisHealthChecksBuilderExtensionsTests
{
    [Fact]
    public async Task AddYuuRedis_should_report_healthy_with_ready_tag_when_redis_is_up_async()
    {
        await using var provider = BuildProvider(TestRedis.ConnectionString, ["ready"]);

        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        report.Entries.Keys.ShouldBe([YuuRedisConsts.HealthCheckName]);
        var entry = report.Entries[YuuRedisConsts.HealthCheckName];
        entry.Status.ShouldBe(HealthStatus.Healthy);
        entry.Tags.ShouldBe(["ready"]);
    }

    [Fact]
    public async Task AddYuuRedis_should_report_unhealthy_when_redis_is_down_async()
    {
        await using var provider = BuildProvider(TestRedis.UnavailableEndPoint(), ["ready"]);

        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        report.Entries[YuuRedisConsts.HealthCheckName].Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AddYuuRedis_should_not_register_when_not_configured_async(string? redisConfiguration)
    {
        await using var provider = BuildProvider(redisConfiguration, ["ready"]);

        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ShouldBeEmpty();
        (await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync()).Entries.Count.ShouldBe(0);
    }

    [Fact]
    public async Task AddYuuRedis_should_use_given_name_async()
    {
        await using var provider = BuildProvider(TestRedis.ConnectionString, null, "cache");

        var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Single();
        registration.Name.ShouldBe("cache");
        registration.Tags.ShouldBeEmpty();
    }

    private static ServiceProvider BuildProvider(string? redisConfiguration, string[]? tags, string name = YuuRedisConsts.HealthCheckName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks().AddYuuRedis(TestRedis.CreateConfiguration(redisConfiguration), name, tags);
        return services.BuildServiceProvider();
    }
}
