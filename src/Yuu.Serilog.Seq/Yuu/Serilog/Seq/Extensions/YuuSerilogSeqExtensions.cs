using Serilog;
using Serilog.Events;

namespace Yuu.Serilog.Seq.Extensions;

public static class YuuSerilogSeqExtensions
{
    /// <summary>
    /// 以 Async 包裝掛上 Seq sink。
    /// <para>
    /// <paramref name="eventBodyLimitBytes"/>：單一事件的序列化上限。官方 Serilog.Sinks.Seq 預設 256 KB，超過時整筆事件被丟棄、
    /// 只送出「Event JSON representation exceeds the byte size limit」占位事件，真正的錯誤內容就看不到了；
    /// 這裡預設放寬到 1 MB，當作最後防線（2026-10-19）。來源瘦身（EF 例外摘要、字串／集合長度上限）見同套件的
    /// <see cref="YuuSerilogExceptionExtensions.UseYuuExceptionDetails"/>。Seq server 端若另有較小的事件上限，仍以 server 設定為準。
    /// </para>
    /// </summary>
    public static LoggerConfiguration UseSeqAsync(
        this LoggerConfiguration c,
        string serverUrl = "http://localhost:5341",
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Information,
        long eventBodyLimitBytes = 1024 * 1024)
    {
        c.WriteTo.Async(action => action.Seq(
            serverUrl,
            restrictedToMinimumLevel: restrictedToMinimumLevel,
            eventBodyLimitBytes: eventBodyLimitBytes));

        return c;
    }

    public static LoggerConfiguration UseSeq(this LoggerConfiguration c, string serverUrl = "http://localhost:5341", LogEventLevel restrictedToMinimumLevel = LogEventLevel.Information)
    {
        c.WriteTo.Seq(serverUrl, restrictedToMinimumLevel: restrictedToMinimumLevel);

        return c;
    }
}
