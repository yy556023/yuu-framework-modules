using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Yuu.Abp.EntityFrameworkCore.Tests;

/// <summary>
/// 資料庫設定組字串、來源疊加與舊設定相容性測試；密碼皆為測試假值。
/// </summary>
public class YuuConnectionStringConfigurationTests
{
    [Fact]
    public void Should_Create_Three_ConnectionStrings()
    {
        var values = CreateSettings();
        values["Databases:Hangfire:Provider"] = "PostgreSql";
        values["Databases:HangfireUser:Provider"] = "PostgreSql";
        values["Databases:Hangfire:Host"] = "jobs-host";
        values["Databases:Hangfire:Port"] = "5433";
        values["Databases:Hangfire:Database"] = "hangfire_admin";
        values["Databases:Hangfire:Username"] = "jobs_admin";
        values["Databases:Hangfire:Password"] = "dummy_admin";
        values["Databases:HangfireUser:Host"] = "users-host";
        values["Databases:HangfireUser:Database"] = "hangfire_user";
        values["Databases:HangfireUser:Username"] = "jobs_user";
        values["Databases:HangfireUser:Password"] = "dummy_user";
        values["Databases:Default:IncludeErrorDetail"] = "true";
        values["Databases:Default:AdditionalOptions"] = "Pooling=true;Maximum Pool Size=100";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).AddYuuConnectionStrings().Build();

        configuration.GetSection("ConnectionStrings").GetChildren().Count().ShouldBe(3);
        configuration.GetConnectionString("Default").ShouldBe(
            "Host=localhost;Port=5432;Database=clouds;Username=runtime;Password=dummy;" +
            "Include Error Detail=true;Pooling=true;Maximum Pool Size=100");
        configuration.GetConnectionString("Hangfire").ShouldBe(
            "Host=jobs-host;Port=5433;Database=hangfire_admin;Username=jobs_admin;Password=dummy_admin;Include Error Detail=false;");
        configuration.GetConnectionString("HangfireUser").ShouldBe(
            "Host=users-host;Port=5432;Database=hangfire_user;Username=jobs_user;Password=dummy_user;Include Error Detail=false;");
    }

    [Theory]
    [InlineData("Host=legacy;Database=old")]
    [InlineData("")]
    public void Should_Preserve_Explicit_ConnectionString(string connectionString)
    {
        var values = CreateSettings();
        values["ConnectionStrings:Default"] = connectionString;
        values.Remove("Databases:Default:Host");
        values["Databases:Default:Provider"] = "invalid";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).AddYuuConnectionStrings().Build();

        configuration.GetConnectionString("Default").ShouldBe(connectionString);
    }

    [Theory]
    [InlineData("Host", "Databases:Default:Host")]
    [InlineData("Username", "Databases:Default:Username")]
    [InlineData("Database", "Databases:Default:Database")]
    public void Should_Reject_Missing_Required_Value(string key, string configurationKey)
    {
        var values = CreateSettings();
        values[configurationKey] = " ";
        var builder = new ConfigurationBuilder().AddInMemoryCollection(values);

        var exception = Should.Throw<InvalidOperationException>(() => builder.AddYuuConnectionStrings());
        exception.Message.ShouldContain("Default");
        exception.Message.ShouldContain(key);
    }

    [Theory]
    [InlineData("dummy; password")]
    [InlineData("dummy=password")]
    [InlineData("dummy\"password")]
    [InlineData("dummy'password")]
    [InlineData(" dummy ")]
    public void Should_Quote_And_RoundTrip_Password(string password)
    {
        var options = new YuuDatabaseConnectionOptions { Provider = YuuDatabaseProvider.PostgreSql, Host = "localhost", Database = "clouds", Username = "runtime", Password = password };
        var connectionString = YuuConnectionStringBuilder.Build("Default", options);

        connectionString.ShouldContain("Password=\"");
        var parsed = new DbConnectionStringBuilder { ConnectionString = connectionString };
        parsed["Password"].ShouldBe(password);
    }

    [Fact]
    public void Should_Allow_Empty_Password_And_Use_Defaults()
    {
        var options = new YuuDatabaseConnectionOptions { Provider = YuuDatabaseProvider.PostgreSql, Host = "localhost", Database = "clouds", Username = "runtime" };

        options.Port.ShouldBeNull();
        YuuConnectionStringBuilder.Build("Default", options).ShouldBe(
            "Host=localhost;Port=5432;Database=clouds;Username=runtime;Password=;Include Error Detail=false;");
    }

    [Fact]
    public void Should_Use_PostgreSql_Default_Port_When_Omitted()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(CreateSettings()).AddYuuConnectionStrings().Build();

        configuration.GetConnectionString("Default").ShouldNotBeNull().ShouldContain("Port=5432;");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("4")]
    [InlineData("PostgreSql, SqlServer")]
    public void Should_Reject_Missing_Or_Invalid_Provider(string? provider)
    {
        var values = CreateSettings();
        if (provider is null)
        {
            values.Remove("Databases:Default:Provider");
        }
        else
        {
            values["Databases:Default:Provider"] = provider;
        }

        var builder = new ConfigurationBuilder().AddInMemoryCollection(values);
        var exception = Should.Throw<InvalidOperationException>(() => builder.AddYuuConnectionStrings());

        exception.Message.ShouldContain("Default");
        exception.Message.ShouldContain("Provider");
    }

    [Theory]
    [InlineData(YuuDatabaseProvider.SqlServer)]
    [InlineData(YuuDatabaseProvider.MySql)]
    public void Should_Reject_Unimplemented_Provider(YuuDatabaseProvider provider)
    {
        var values = CreateSettings();
        values["Databases:Default:Provider"] = provider.ToString();
        var builder = new ConfigurationBuilder().AddInMemoryCollection(values);
        var exception = Should.Throw<NotSupportedException>(() => builder.AddYuuConnectionStrings());

        exception.Message.ShouldContain("Default");
        exception.Message.ShouldContain(provider.ToString());
        exception.Message.ShouldContain("尚未實作");
    }

    [Theory]
    [InlineData((YuuDatabaseProvider)0)]
    [InlineData((YuuDatabaseProvider)4)]
    public void Should_Reject_Invalid_Provider_In_Direct_Build(YuuDatabaseProvider provider)
    {
        var options = new YuuDatabaseConnectionOptions { Provider = provider };
        var exception = Should.Throw<InvalidOperationException>(() => YuuConnectionStringBuilder.Build("Jobs", options));

        exception.Message.ShouldContain("Jobs");
        exception.Message.ShouldContain("Provider");
    }

    [Fact]
    public void Should_Not_Add_Source_Without_Section()
    {
        var builder = new ConfigurationBuilder();
        builder.AddYuuConnectionStrings().ShouldBeSameAs(builder);

        builder.Sources.ShouldBeEmpty();
        builder.Build().AsEnumerable().ShouldBeEmpty();
    }

    [Fact]
    public void Should_Merge_Base_And_Secrets_Json_Files()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "appsettings.json"),
                """{"Databases":{"Default":{"Provider":"PostgreSql","Host":"localhost","Port":5433,"Database":"clouds"}}}""");
            File.WriteAllText(Path.Combine(directory, "appsettings.secrets.json"),
                """{"Databases":{"Default":{"Username":"runtime","Password":"dummy"}}}""");
            var configuration = new ConfigurationBuilder().SetBasePath(directory)
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.secrets.json")
                .AddYuuConnectionStrings().Build();
            using var configurationLifetime = (IDisposable)configuration;

            configuration.GetConnectionString("Default").ShouldBe(
                "Host=localhost;Port=5433;Database=clouds;Username=runtime;Password=dummy;Include Error Detail=false;");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Should_Support_Custom_Section_Name()
    {
        var values = CreateSettings().ToDictionary(pair => pair.Key.Replace("Databases:", "Custom:"), pair => pair.Value);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).AddYuuConnectionStrings("Custom").Build();

        configuration.GetConnectionString("Default").ShouldNotBeNull().ShouldContain("Database=clouds;");
    }

    [Fact]
    public void Should_Configure_Host_ConnectionStrings()
    {
        using var host = new HostBuilder()
            .ConfigureAppConfiguration((context, builder) => builder.AddInMemoryCollection(CreateSettings()))
            .AddYuuConnectionStrings()
            .Build();

        host.Services.GetRequiredService<IConfiguration>().GetConnectionString("Default").ShouldBe(
            "Host=localhost;Port=5432;Database=clouds;Username=runtime;Password=dummy;Include Error Detail=false;");
    }

    private static Dictionary<string, string?> CreateSettings()
    {
        return new Dictionary<string, string?>
        {
            ["Databases:Default:Provider"] = "PostgreSql",
            ["Databases:Default:Host"] = "localhost",
            ["Databases:Default:Username"] = "runtime",
            ["Databases:Default:Password"] = "dummy",
            ["Databases:Default:Database"] = "clouds"
        };
    }
}
