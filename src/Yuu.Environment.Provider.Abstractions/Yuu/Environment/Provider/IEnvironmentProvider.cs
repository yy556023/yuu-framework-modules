namespace Yuu.Environment.Provider;

public interface IEnvironmentProvider
{
    string EnvName { get; }
    bool IsLocal();
    bool IsDev();
    bool IsStag();
    bool IsProd();
}
