using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Yuu.EntityFrameworkCore.Logging.Options;

public sealed class EfCoreSqlLoggingOptions
{
    /// <summary>
    /// 是否啟用 Executable SQL Logging
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// SQL Log 使用的最低 LogLevel
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// 是否記錄失敗的 SQL
    /// </summary>
    public bool LogOnError { get; set; } = false;

    /// <summary>
    /// 忽略的 CommandText 關鍵字
    /// </summary>
    public List<string> IgnoreContains { get; set; } = ["background_jobs", "BackgroundJobs"];

    /// <summary>
    /// 分類名稱
    /// </summary>
    public string CategoryName { get; set; } = "Yuu.EntityFrameworkCore.Database.Command";
}
