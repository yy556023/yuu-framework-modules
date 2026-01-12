namespace Yuu.AspNetCore.Hosting.Environments;

public static class YuuEnvironmentConsts
{
    public static class Local
    {
        public const string Prefix = "local";

        public const string Dev = Prefix + "-dev";
        public const string Stag = Prefix + "-stag";
        public const string Prod = Prefix + "-prod";
    }
}
