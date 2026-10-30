using StackExchange.Redis;

namespace Yuu.Redis;

/// <summary>
/// 建立 <see cref="IConnectionMultiplexer"/> 用的 <see cref="ConfigurationOptions"/>。
/// </summary>
public static class YuuRedisConfigurationOptions
{
    /// <summary>
    /// 解析連線字串,並套用「Redis 停機時 Host 仍須啟動」的設定:
    /// <list type="bullet">
    /// <item><description><c>AbortOnConnectFail = false</c>:啟動時連不到不丟例外,背景持續重連。</description></item>
    /// <item><description><c>BacklogPolicy.FailFast</c>:離線期間的命令立即失敗,不排隊等重連;由呼叫端決定降級(記警告、回錯誤碼、下次重試)。</description></item>
    /// </list>
    /// 2026-10-30 自 exvs2ib-server-game / -match 兩個 Host 的 <c>ConfigureRedisConnection</c> 收斂,行為不變。
    /// </summary>
    public static ConfigurationOptions Create(string configuration)
    {
        var options = ConfigurationOptions.Parse(configuration);
        options.AbortOnConnectFail = false;
        options.BacklogPolicy = BacklogPolicy.FailFast;
        return options;
    }
}
