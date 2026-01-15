using Volo.Abp.AspNetCore.WebClientInfo;
using Volo.Abp.DependencyInjection;

namespace Yuu.ClientInfo.Provider;

public class ClientInfoProvider : IClientInfoProvider, ITransientDependency
{
    private readonly IWebClientInfoProvider _webClientInfoProvider;

    public ClientInfoProvider(IWebClientInfoProvider webClientInfoProvider)
    {
        _webClientInfoProvider = webClientInfoProvider;
    }

    public string? BrowserInfo => _webClientInfoProvider.BrowserInfo;
    public string? ClientIpAddress => _webClientInfoProvider.ClientIpAddress;
    public string? DeviceInfo => _webClientInfoProvider.DeviceInfo;
}
