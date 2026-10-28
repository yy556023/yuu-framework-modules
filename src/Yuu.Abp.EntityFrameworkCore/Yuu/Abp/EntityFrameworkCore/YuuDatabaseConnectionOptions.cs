namespace Yuu.Abp.EntityFrameworkCore;

/// <summary>
/// 單一資料庫連線的完整設定。
/// </summary>
public class YuuDatabaseConnectionOptions
{
    /// <summary>
    /// 資料庫種類；決定連線字串格式與預設連接埠，缺值啟動即丟例外。
    /// </summary>
    public YuuDatabaseProvider Provider { get; set; }
    /// <summary>
    /// 主機名稱或 IP（非敏感，可放 appsettings.json／env json）。
    /// </summary>
    public string Host { get; set; } = string.Empty;
    /// <summary>
    /// 未指定時使用提供者預設值：PostgreSql 5432、SqlServer 1433、MySql 3306。
    /// </summary>
    public int? Port { get; set; }
    /// <summary>
    /// 資料庫名稱（非敏感）。
    /// </summary>
    public string Database { get; set; } = string.Empty;
    /// <summary>
    /// 登入帳號（敏感，放 appsettings.secrets.json 同 key 物件下）。
    /// </summary>
    public string Username { get; set; } = string.Empty;
    /// <summary>
    /// 登入密碼（敏感，放 appsettings.secrets.json；允許空字串給本機 trust 認證）。
    /// </summary>
    public string Password { get; set; } = string.Empty;
    /// <summary>
    /// 是否在例外訊息帶出資料庫錯誤細節（PostgreSql 的 Include Error Detail）；正式環境應為 false。
    /// </summary>
    public bool IncludeErrorDetail { get; set; }
    /// <summary>
    /// 原樣追加在連線字串尾端的額外選項（例如連線池參數），格式由呼叫端負責。
    /// </summary>
    public string? AdditionalOptions { get; set; }
}
