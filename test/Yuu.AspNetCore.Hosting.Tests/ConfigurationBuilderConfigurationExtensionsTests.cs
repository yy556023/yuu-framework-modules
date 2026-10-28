using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Shouldly;
using Xunit;

namespace Yuu.AspNetCore.Hosting.Configuration.Tests;

/// <summary>
/// 驗證環境設定與環境機密設定的載入順序及缺檔行為。
/// </summary>
public sealed class ConfigurationBuilderConfigurationExtensionsTests : IDisposable
{
    private readonly string _directory;
    private readonly PhysicalFileProvider _fileProvider;

    /// <summary>
    /// 建立獨立暫存目錄與基礎、develop、develop 機密設定檔。
    /// </summary>
    public ConfigurationBuilderConfigurationExtensionsTests()
    {
        _directory = Directory.CreateTempSubdirectory("Yuu.Hosting.Tests-").FullName;
        _fileProvider = new PhysicalFileProvider(_directory);
        File.WriteAllText(Path.Combine(_directory, "appsettings.json"), """{"Value":"base","BaseOnly":"retained"}""");
        File.WriteAllText(Path.Combine(_directory, "appsettings.develop.json"), """{"Value":"develop","EnvOnly":"retained"}""");
        File.WriteAllText(Path.Combine(_directory, "appsettings.develop.secrets.json"), """{"Value":"env-secret"}""");
    }

    /// <summary>
    /// 同 key 以最後載入的環境機密設定為準，並保留其他來源的不同 key。
    /// </summary>
    [Fact]
    public void EnvSecretsOverrideEnvironmentAndBaseSettings()
    {
        var builder = CreateBuilder();

        builder.AddAppSettingsEnvsJson("develop").ShouldBeSameAs(builder);
        using var configuration = (ConfigurationRoot)builder.Build();

        configuration["Value"].ShouldBe("env-secret");
        configuration["BaseOnly"].ShouldBe("retained");
        configuration["EnvOnly"].ShouldBe("retained");
    }

    /// <summary>
    /// 環境機密設定檔不存在時仍可載入必要的環境設定檔。
    /// </summary>
    [Fact]
    public void MissingEnvSecretsDoesNotThrow()
    {
        File.Delete(Path.Combine(_directory, "appsettings.develop.secrets.json"));
        var builder = CreateBuilder().AddAppSettingsEnvsJson("develop");

        using var configuration = (ConfigurationRoot)builder.Build();

        configuration["Value"].ShouldBe("develop");
    }

    /// <summary>
    /// 環境名稱包含 local 時不分大小寫，既有環境檔也不會被疊加。
    /// </summary>
    [Fact]
    public void LocalEnvironmentDoesNotAddAnySources()
    {
        File.Move(Path.Combine(_directory, "appsettings.develop.json"), Path.Combine(_directory, "appsettings.develop-LoCaL.json"));
        File.Move(
            Path.Combine(_directory, "appsettings.develop.secrets.json"),
            Path.Combine(_directory, "appsettings.develop-LoCaL.secrets.json"));
        var builder = CreateBuilder();
        var sourceCount = builder.Sources.Count;

        builder.AddAppSettingsEnvsJson("develop-LoCaL").ShouldBeSameAs(builder);
        using var configuration = (ConfigurationRoot)builder.Build();

        builder.Sources.Count.ShouldBe(sourceCount);
        configuration["Value"].ShouldBe("base");
        configuration["EnvOnly"].ShouldBeNull();
    }

    /// <summary>
    /// 環境設定檔不存在且 optional 為 false 時拋出例外，即使環境機密檔存在也不放行。
    /// </summary>
    [Fact]
    public void MissingRequiredEnvironmentFileThrows()
    {
        File.Delete(Path.Combine(_directory, "appsettings.develop.json"));
        var builder = CreateBuilder().AddAppSettingsEnvsJson("develop", optional: false);

        Should.Throw<FileNotFoundException>(() =>
        {
            using var configuration = (ConfigurationRoot)builder.Build();
        });
    }

    /// <summary>
    /// 釋放檔案提供者並刪除本測試建立的暫存目錄。
    /// </summary>
    public void Dispose()
    {
        _fileProvider.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private ConfigurationBuilder CreateBuilder()
    {
        var builder = new ConfigurationBuilder();
        builder.SetFileProvider(_fileProvider);
        builder.AddJsonFile("appsettings.json");
        return builder;
    }
}
