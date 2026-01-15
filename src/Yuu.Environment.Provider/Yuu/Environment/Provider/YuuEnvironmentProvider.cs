using Microsoft.Extensions.Hosting;
using Volo.Abp.DependencyInjection;
using Yuu.Environment.Provider.Extensions;

namespace Yuu.Environment.Provider;

public class YuuEnvironmentProvider : IEnvironmentProvider, ITransientDependency
{
    private readonly IHostEnvironment _env;

    public YuuEnvironmentProvider(IHostEnvironment env)
    {
        _env = env;
    }

    public string EnvName => _env.EnvironmentName;
    public bool IsLocal() => _env.IsLocal();
    public bool IsDev() => _env.IsDev();
    public bool IsStag() => _env.IsStag();
    public bool IsProd() => _env.IsProd();
}
