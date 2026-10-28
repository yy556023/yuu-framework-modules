using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.Destructurers;

namespace Yuu.Serilog.Seq.Exceptions;

/// <summary>
/// EF Core <see cref="DbUpdateException"/>（含 <see cref="DbUpdateConcurrencyException"/>）的精簡解構器
/// （OPS-LOG-EVENT-SIZE-01，2026-10-19；原在 clouds-admin-api Host，因 User Host 同樣有 OpenIddict token 洩漏風險而上移到框架）。
/// <para>
/// 根因：Serilog.Exceptions 預設以反射解構未知例外型別，會沿 <see cref="DbUpdateException.Entries"/> 走進
/// <see cref="EntityEntry"/> 的 <c>Entity</c>（整筆欄位值，OpenIddictToken 甚至含 token payload）、
/// <c>Context</c>（DbContext 上百個 DbSet）與 <c>Metadata</c>（整個實體型別的 annotations 文字），
/// 一筆樂觀併發失敗就超過 Seq sink 的 256 KB 事件上限，Seq 只收到截斷占位事件、看不到真正的錯誤。
/// </para>
/// <para>
/// 這裡只保留例外共通欄位（型別、訊息、堆疊等由基底類別處理）與每筆 entry 的實體型別、狀態、主鍵值；
/// 不輸出任何欄位值——欄位值對排查併發衝突幫助有限，卻可能洩漏 token、金額等敏感內容。
/// </para>
/// </summary>
public class DbUpdateExceptionSummaryDestructurer : ExceptionDestructurer
{
    /// <summary>
    /// 單一事件最多列出的 entry 數；批次 SaveChanges 失敗時 entry 可能上千筆，列前幾筆已足以定位。
    /// </summary>
    public const int MaxEntries = 20;

    private readonly Type[] _targetTypes;

    /// <summary>
    /// 只處理 EF 內建的 <see cref="DbUpdateException"/> 與 <see cref="DbUpdateConcurrencyException"/>。
    /// </summary>
    public DbUpdateExceptionSummaryDestructurer()
        : this([])
    {
    }

    /// <summary>
    /// 另外處理指定的 <see cref="DbUpdateException"/> 衍生型別。
    /// Serilog.Exceptions 以 <c>exception.GetType()</c> 精確比對 <see cref="TargetTypes"/>，衍生型別不列進來就會掉回反射解構、
    /// 整包展開 EntityEntry；衍生型別由 <see cref="YuuExceptionDetailsEnricher"/> 在執行期遇到時補進來。
    /// </summary>
    public DbUpdateExceptionSummaryDestructurer(IEnumerable<Type> derivedTypes)
    {
        _targetTypes = new[] { typeof(DbUpdateException), typeof(DbUpdateConcurrencyException) }
            .Concat(derivedTypes.Where(type => typeof(DbUpdateException).IsAssignableFrom(type)))
            .Distinct()
            .ToArray();
    }

    /// <inheritdoc />
    public override Type[] TargetTypes => _targetTypes;

    /// <inheritdoc />
    public override void Destructure(
        Exception exception,
        IExceptionPropertiesBag propertiesBag,
        Func<Exception, IReadOnlyDictionary<string, object?>?> destructureException)
    {
        base.Destructure(exception, propertiesBag, destructureException);

        var dbUpdateException = (DbUpdateException)exception;
        IReadOnlyList<EntityEntry> entries;
        try
        {
            entries = dbUpdateException.Entries;
        }
        catch (Exception)
        {
            // Entries 在部分 provider／已釋放 context 下可能拋例外；日誌解構不能讓原本的錯誤事件也跟著遺失。
            return;
        }

        propertiesBag.AddProperty("EntryCount", entries.Count);
        propertiesBag.AddProperty("Entries", entries.Take(MaxEntries).Select(Summarize).ToList());
    }

    private static IReadOnlyDictionary<string, string?> Summarize(EntityEntry entry)
    {
        try
        {
            var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties ?? [];
            var key = string.Join(",", keyProperties.Select(property => entry.Property(property.Name).CurrentValue?.ToString()));
            return new Dictionary<string, string?>
            {
                ["EntityType"] = entry.Metadata.ClrType.Name,
                ["State"] = entry.State.ToString(),
                ["Key"] = key,
            };
        }
        catch (Exception)
        {
            // 讀主鍵失敗時至少留下型別名稱，仍可知道是哪一張表衝突。
            return new Dictionary<string, string?> { ["EntityType"] = entry.Entity.GetType().Name };
        }
    }
}
