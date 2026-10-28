namespace Yuu.AspNetCore.Hosting.Environments;

public static class YuuEnvironmentConsts
{
    public static class Local
    {
        /// <summary>
        /// 環境名稱包含此字串（不分大小寫）才算 local，例如 <c>local-dev</c>。
        /// 空環境名不算 local（2026-10-29 使用者決定）。
        /// </summary>
        public const string Prefix = "local";
    }
}
