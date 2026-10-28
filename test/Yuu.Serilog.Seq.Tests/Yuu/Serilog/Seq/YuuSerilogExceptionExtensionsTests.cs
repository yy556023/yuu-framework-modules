using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using Shouldly;
using Xunit;
using Yuu.Serilog.Seq.Extensions;

namespace Yuu.Serilog.Seq;

/// <summary>
/// OPS-LOG-EVENT-SIZE-01（2026-10-19）：EF 樂觀併發例外的 Error 事件必須小於 Seq sink 預設 256 KB 上限，
/// 且不得把實體欄位值序列化進事件。只驗 Serilog 設定本身，不啟動 Host、不連資料庫（InMemory provider 只用來建 change tracker）。
/// </summary>
public class YuuSerilogExceptionExtensionsTests
{
    private const int SeqDefaultEventBodyLimitBytes = 256 * 1024;

    [Fact]
    public void Concurrency_exception_event_stays_under_seq_limit_and_omits_entity_values()
    {
        using var context = new SampleDbContext();
        var entity = new SampleTokenEntity { Id = Guid.NewGuid(), Payload = "token-payload-must-not-leak" };
        var inner = new DbUpdateConcurrencyException("test concurrency", [CreateUpdateEntry(context, entity)]);
        // 正式路徑由 ABP 以 AbpDbConcurrencyException 包一層；框架不依賴 ABP，改用普通 Exception 包裝，語意相同。
        var exception = new InvalidOperationException("test wrapper", inner);
        // 300 KB 的 Data（單獨就超過 256 KB 上限）模擬上游把大型內容掛進例外；瘦身設定須把它截斷而非整包遺失。
        exception.Data["Huge"] = new string('x', 300 * 1024);

        var json = RenderSingleErrorEvent(exception);

        Encoding.UTF8.GetByteCount(json).ShouldBeLessThan(SeqDefaultEventBodyLimitBytes);
        json.ShouldContain("\"EntityType\":\"" + nameof(SampleTokenEntity) + "\"");
        json.ShouldContain("\"State\":\"Modified\"");
        json.ShouldContain("\"Key\":\"" + entity.Id + "\"");
        json.ShouldNotContain(entity.Payload);
        // 舊行為會把 DbContext 與實體型別 metadata 整包展開；兩者都不該再出現。
        json.ShouldNotContain("IQueryable skipped");
        json.ShouldNotContain("Annotations");
        // Data 仍須保留 Huge 鍵，且值被截到 8K 上限（Serilog 截斷後以「…」結尾），而非整個 Data 被丟掉。
        var huge = ExtractDataValue(json, "Huge");
        huge.Length.ShouldBeLessThanOrEqualTo(YuuSerilogExceptionExtensions.DefaultMaxStringLength);
        huge.Length.ShouldBeGreaterThan(YuuSerilogExceptionExtensions.DefaultMaxStringLength - 8);
        huge.ShouldStartWith("xxxx");
    }

    [Fact]
    public void Derived_db_update_exception_still_uses_summary_destructurer()
    {
        using var context = new SampleDbContext();
        var entity = new SampleTokenEntity { Id = Guid.NewGuid(), Payload = "derived-payload-must-not-leak" };
        // 衍生型別放在 InnerException：同時驗證 enricher 會沿例外鏈找出尚未登記的型別。
        var exception = new InvalidOperationException(
            "test wrapper",
            new DerivedDbUpdateException("test derived", [CreateUpdateEntry(context, entity)]));

        var json = RenderSingleErrorEvent(exception);

        json.ShouldContain("\"EntityType\":\"" + nameof(SampleTokenEntity) + "\"");
        json.ShouldContain("\"Key\":\"" + entity.Id + "\"");
        json.ShouldNotContain(entity.Payload);
        json.ShouldNotContain("Annotations");
    }

    private static IUpdateEntry CreateUpdateEntry(SampleDbContext context, SampleTokenEntity entity)
    {
        context.Attach(entity);
        context.Entry(entity).State = EntityState.Modified;
        // EF 只開放以 IUpdateEntry 建構例外；正式路徑由 SaveChanges 內部建立，測試只能經 infrastructure 取得同一物件。
#pragma warning disable EF1001
        return (IUpdateEntry)context.Entry(entity).GetInfrastructure();
#pragma warning restore EF1001
    }

    private static string RenderSingleErrorEvent(Exception exception)
    {
        var sink = new CollectingSink();
        using (var logger = new LoggerConfiguration()
                   .UseYuuExceptionDetails()
                   .WriteTo.Sink(sink)
                   .CreateLogger())
        {
            logger.Error(exception, "Save failed for {EntityName}", nameof(SampleTokenEntity));
        }

        var writer = new StringWriter();
        new JsonFormatter().Format(sink.Events.Single(), writer);
        return writer.ToString();
    }

    private static string ExtractDataValue(string json, string key)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var detail = document.RootElement.GetProperty("Properties").GetProperty("ExceptionDetail");
        var data = detail.GetProperty("Data");
        return data.GetProperty(key).GetString() ?? string.Empty;
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private sealed class DerivedDbUpdateException(string message, IReadOnlyList<IUpdateEntry> entries)
        : DbUpdateException(message, entries);

    private sealed class SampleTokenEntity
    {
        public Guid Id { get; set; }

        public string Payload { get; set; } = string.Empty;
    }

    private sealed class SampleDbContext : DbContext
    {
        public DbSet<SampleTokenEntity> Tokens => Set<SampleTokenEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 只用來建 model 與 change tracker，不會寫入任何資料。
            optionsBuilder.UseInMemoryDatabase(nameof(YuuSerilogExceptionExtensionsTests));
        }
    }
}
