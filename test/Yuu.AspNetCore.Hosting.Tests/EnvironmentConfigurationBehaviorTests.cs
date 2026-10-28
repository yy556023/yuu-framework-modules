using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Shouldly;
using Xunit;
using Yuu.Observability.Extensions;

namespace Yuu.AspNetCore.Hosting.Configuration.Tests;

// 測試會暫時變更程序環境變數與 Console，必須避免與其他測試並行。
[CollectionDefinition("Environment configuration", DisableParallelization = true)]
public class EnvironmentConfigurationCollection;

[Collection("Environment configuration")]
public class EnvironmentConfigurationBehaviorTests
{
    /// <summary>
    /// 空環境名不算 local，但設定載入仍與 K2m 一致：不新增環境檔來源。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyEnvironmentDoesNotAddConfigurationSources(string? environmentName)
    {
        var previous = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            System.Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
            var builder = new ConfigurationBuilder();
            builder.AddAppSettingsEnvsJson(environmentName).ShouldBeSameAs(builder);
            builder.Sources.ShouldBeEmpty();

            var sourceCount = -1;
            var hostBuilder = new HostBuilder()
                .ConfigureAppConfiguration((_, configuration) => sourceCount = configuration.Sources.Count)
                .AddAppSettingsEnvsJson(environmentName)
                .ConfigureAppConfiguration((_, configuration) => configuration.Sources.Count.ShouldBe(sourceCount));
            using var host = hostBuilder.Build();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previous);
        }
    }

    /// <summary>
    /// 透過公開 Console 設定驗證 bootstrap 等級，並保留呼叫端明確覆寫等級的能力。
    /// </summary>
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    [InlineData("  ", null, false)]
    [InlineData("develop-LoCaL", null, true)]
    [InlineData("Production", null, false)]
    [InlineData("", LogEventLevel.Debug, true)]
    [InlineData("local", LogEventLevel.Information, false)]
    public void ConsoleUsesInformationUnlessLocalOrExplicitlyOverridden(
        string? environmentName,
        LogEventLevel? minimumLevel,
        bool includesDebug)
    {
        var previousEnvironment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var previousOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            System.Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
            Console.SetOut(output);
            using (var logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteToEnvironmentConsole(minimumLevel, environmentName)
                .CreateLogger())
            {
                logger.Debug("debug-marker");
                logger.Information("information-marker");
            }
        }
        finally
        {
            Console.SetOut(previousOutput);
            System.Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
        }

        var rendered = output.ToString();
        rendered.ShouldContain("information-marker");
        rendered.Contains("debug-marker", StringComparison.Ordinal).ShouldBe(includesDebug);
    }
}
