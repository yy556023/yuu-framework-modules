using Serilog.Events;
using Serilog;

namespace Yuu.Serilog.Seq.Extensions;

public static class YuuSerilogSeqExtensions
{
    public static LoggerConfiguration UseSeqAsync(this LoggerConfiguration c, string serverUrl = "http://localhost:5341", LogEventLevel restrictedToMinimumLevel = LogEventLevel.Information)
    {
        c.WriteTo.Async(action => action.Seq(serverUrl, restrictedToMinimumLevel: restrictedToMinimumLevel));

        return c;
    }

    public static LoggerConfiguration UseSeq(this LoggerConfiguration c, string serverUrl = "http://localhost:5341", LogEventLevel restrictedToMinimumLevel = LogEventLevel.Information)
    {
        c.WriteTo.Seq(serverUrl, restrictedToMinimumLevel: restrictedToMinimumLevel);

        return c;
    }
}
