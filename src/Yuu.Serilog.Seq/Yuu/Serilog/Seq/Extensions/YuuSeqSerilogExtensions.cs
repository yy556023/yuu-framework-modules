using Serilog;

namespace Yuu.Serilog.Seq.Extensions;

public static class YuuSeqSerilogExtensions
{
    public static LoggerConfiguration UseSeqAsync(this LoggerConfiguration c, string serverUrl = "http://localhost:5341")
    {
        c.WriteTo.Async(action => action.Seq(serverUrl));

        return c;
    }

    public static LoggerConfiguration UseSeq(this LoggerConfiguration c, string serverUrl = "http://localhost:5341")
    {
        c.WriteTo.Seq(serverUrl);

        return c;
    }
}
