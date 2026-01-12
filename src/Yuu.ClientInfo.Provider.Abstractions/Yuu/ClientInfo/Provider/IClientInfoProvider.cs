namespace Yuu.ClientInfo.Provider;

public interface IClientInfoProvider
{
    string? BrowserInfo { get; }
    string? ClientIpAddress { get; }
    string? DeviceInfo { get; }
}
