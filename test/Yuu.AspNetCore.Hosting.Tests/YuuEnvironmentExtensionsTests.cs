using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;
using Yuu.Environment.Provider;
using Yuu.Environment.Provider.Extensions;

namespace Yuu.AspNetCore.Hosting.Configuration.Tests;

/// <summary>
/// 保護既有字串多載，並驗證主機環境與啟動階段使用相同判定規則。
/// </summary>
public class YuuEnvironmentExtensionsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("local", true)]
    [InlineData("develop-LoCaL", true)]
    [InlineData("Development", false)]
    [InlineData("Production", false)]
    public void LocalOverloadsUseTheSameRules(string? name, bool expected)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name!);

        YuuEnvironmentExtensions.IsLocal(name).ShouldBe(expected);
        environment.IsLocal().ShouldBe(expected);
        new YuuEnvironmentProvider(environment).IsLocal().ShouldBe(expected);
    }

    [Theory]
    [InlineData("DEVELOPMENT", true, false, false)]
    [InlineData("STAGING", false, true, false)]
    [InlineData("PRODUCTION", false, false, true)]
    [InlineData("local", false, false, false)]
    public void EnvironmentNamesIgnoreCase(string name, bool dev, bool stag, bool prod)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);

        environment.IsDev().ShouldBe(dev);
        environment.IsStag().ShouldBe(stag);
        environment.IsProd().ShouldBe(prod);
    }
}
