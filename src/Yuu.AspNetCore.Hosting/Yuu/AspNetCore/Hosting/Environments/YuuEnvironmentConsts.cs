namespace Yuu.AspNetCore.Hosting.Environments;

public static class YuuEnvironmentConsts
{
    public static class Local
    {
        /// <summary>
        /// local 的判定已改為「<c>ASPNETCORE_ENVIRONMENT</c> 為空」（2026-09-06），
        /// 因此不再有 <c>local-dev</c> / <c>local-stag</c> / <c>local-prod</c> 這類組合環境名。
        /// </summary>
        public const string Prefix = "local";
    }
}
