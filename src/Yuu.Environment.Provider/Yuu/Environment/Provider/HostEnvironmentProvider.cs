using Microsoft.Extensions.Hosting;
using Volo.Abp.DependencyInjection;

namespace Yuu.Environment.Provider;

public class HostEnvironmentProvider : IEnvironmentProvider, ISingletonDependency
{
    private readonly IHostEnvironment _env;

    public HostEnvironmentProvider(IHostEnvironment env)
    {
        _env = env;
    }

    public string EnvName => _env.EnvironmentName;
    public bool IsLocal() => _env.IsLocal();
    public bool IsDev() => _env.IsDev();
    public bool IsStag() => _env.IsStag();
    public bool IsProd() => _env.IsProd();
}
