using Xunit;

namespace Yuu.Redis;

/// <summary>
/// 共用測試 DB <see cref="TestRedis.Database"/> 的測試類別放同一個 collection,不並行(案例會清空該 DB)。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class TestRedisCollection
{
    public const string Name = "Yuu.Redis real Redis";
}
