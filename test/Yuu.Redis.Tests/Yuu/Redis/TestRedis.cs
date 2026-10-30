using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Yuu.Redis;

/// <summary>
/// Yuu.Redis 測試的真 Redis 連線(不用替身):連本機 Redis(預設 <see cref="DefaultEndPoint"/>,可用環境變數 <see cref="EndPointVariable"/> 改 host:port),
/// 一律使用專用 DB index <see cref="Database"/>(2026-10-30 選定時 <c>INFO keyspace</c> 無此 DB;
/// db 0 為開發資料、12 為 game ScalingVerification、13 / 14 為 match / game 測試、15 為 match ScalingVerification)。
/// 連不到 Redis 時測試直接失敗、不會略過。
/// </summary>
public static class TestRedis
{
    /// <summary>
    /// 覆寫 Redis host:port 的環境變數(例 <c>localhost:6379</c>)。
    /// </summary>
    public const string EndPointVariable = "YUU_TEST_REDIS";

    public const string DefaultEndPoint = "localhost:6379";

    /// <summary>
    /// Yuu.Redis 測試專用的 Redis DB index(不可為 0)。
    /// </summary>
    public const int Database = 11;

    /// <summary>
    /// 受測程式讀的連線字串:測試端點 + <c>defaultDatabase=</c><see cref="Database"/>。
    /// </summary>
    public static string ConnectionString
    {
        get
        {
            var endPoint = Environment.GetEnvironmentVariable(EndPointVariable);
            return $"{(string.IsNullOrWhiteSpace(endPoint) ? DefaultEndPoint : endPoint)},defaultDatabase={Database}";
        }
    }

    /// <summary>
    /// 只含 <c>Redis:Configuration</c> 的設定;<paramref name="redisConfiguration"/> 為 null 表示不設定該鍵。
    /// </summary>
    public static IConfiguration CreateConfiguration(string? redisConfiguration)
    {
        Dictionary<string, string?> values = [];
        if (redisConfiguration != null)
        {
            values[YuuRedisConsts.ConfigurationKey] = redisConfiguration;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    /// <summary>
    /// 另開一條檢查用連線(可 FLUSHDB);連不到直接丟例外。
    /// </summary>
    public static IConnectionMultiplexer ConnectVerifier()
    {
        var options = ConfigurationOptions.Parse(ConnectionString);
        options.DefaultDatabase = Database;
        // FLUSHDB 需要 admin 命令;只在檢查用連線開啟。
        options.AllowAdmin = true;
        options.AbortOnConnectFail = true;
        return ConnectionMultiplexer.Connect(options);
    }

    /// <summary>
    /// 清空測試 DB(只限 <see cref="Database"/>,絕不碰 db 0)。
    /// </summary>
    public static void FlushTestDatabase(IConnectionMultiplexer connectionMultiplexer)
    {
        if (Database == 0)
        {
            throw new InvalidOperationException("測試 Redis DB index 不得為 0");
        }

        foreach (var endPoint in connectionMultiplexer.GetEndPoints())
        {
            connectionMultiplexer.GetServer(endPoint).FlushDatabase(Database);
        }
    }

    /// <summary>
    /// 本機目前沒有程式監聽的埠(<c>127.0.0.1:{port}</c>):真實連線、真實連線失敗,用於「Redis 停機」情境。
    /// </summary>
    public static string UnavailableEndPoint()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return $"127.0.0.1:{port},connectTimeout=1000,connectRetry=1";
    }
}
