using System;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Yuu.EntityFrameworkCore.Logging.Abstractions;
using Yuu.EntityFrameworkCore.Logging.Options;

namespace Yuu.EntityFrameworkCore.Logging.Interceptors;

public sealed class ToQueryStringCommandInterceptor : DbCommandInterceptor, IToQueryStringCommandInterceptor, ISingletonDependency
{
    private readonly ILogger _logger;
    private readonly EfCoreSqlLoggingOptions _options;

    public ToQueryStringCommandInterceptor(ILoggerFactory loggerFactory, IOptions<EfCoreSqlLoggingOptions> options)
    {
        _logger = loggerFactory.CreateLogger(options.Value.CategoryName);
        _options = options.Value;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        LogSql(command, eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        LogSql(command, eventData);
        return new(result);
    }

    // ★ 攔 INSERT / UPDATE / DELETE
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        LogSql(command, eventData);
        return result;
    }

    // ★ 攔 COUNT / Any / Sum / Max
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        LogSql(command, eventData);
        return result;
    }

    // ★ 只在失敗時一定要記（非常重要）
    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        if (_logger == null)
        {
            return;
        }

        if (!_options.LogOnError)
        {
            return;
        }

        var sql = BuildExecutableSql(command);

        _logger.LogError(
            eventData.Exception,
            """

            ╔══════════════════════════════════════════════════════════════
            ║ ❌ EF Core SQL FAILED
            ╠══════════════════════════════════════════════════════════════
            {Sql}
            ╚══════════════════════════════════════════════════════════════
            """,
            sql
        );
    }

    private void LogSql(DbCommand command, CommandEventData eventData)
    {
        if (_logger == null)
        {
            return;
        }

        if (_options.IgnoreContains.Any(x => command.CommandText.Contains(x, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        // 原本的過濾條件保留
        if (command.CommandText.Contains("background_jobs", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!_options.Enabled)
        {
            return;
        }

        // ★ 只在 指定 Level 開啟時才做昂貴處理
        if (!_logger.IsEnabled(_options.LogLevel))
        {
            return;
        }

        var sql = BuildExecutableSql(command);

        _logger.LogDebug(
            """

            ╔══════════════════════════════════════════════════════════════
            ║ 🧠 EF Core SQL (Executable)
            ╠══════════════════════════════════════════════════════════════
            {Sql}
            ╚══════════════════════════════════════════════════════════════
            """,
            sql
        );
    }

    private string BuildExecutableSql(DbCommand command)
    {
        var sql = command.CommandText;

        foreach (DbParameter p in command.Parameters)
        {
            var formatted = FormatParameter(p);
            sql = sql.Replace(p.ParameterName, formatted);
        }

        return sql;
    }

    private string FormatParameter(DbParameter p)
    {
        return p.Value == null || p.Value == DBNull.Value
            ? "NULL"
            : p.Value switch
            {
                string s => $"'{s.Replace("'", "''")}'",
                DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss.ffffff}'",
                DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss.ffffff}'",
                bool b => b ? "1" : "0",
                Guid g => $"'{g}'",
                _ when IsNumeric(p.Value) =>
                    Convert.ToString(p.Value, CultureInfo.InvariantCulture)!,
                _ =>
                    $"'{p.Value.ToString()!.Replace("'", "''")}'"
            };
    }

    private static bool IsNumeric(object value)
        => value is byte or sbyte
        or short or ushort
        or int or uint
        or long or ulong
        or float or double or decimal;
}
