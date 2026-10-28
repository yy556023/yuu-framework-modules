using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions.Core;

namespace Yuu.Serilog.Seq.Exceptions;

/// <summary>
/// 包裝 Serilog.Exceptions 的 <see cref="ExceptionEnricher"/>，讓 <see cref="DbUpdateException"/> 的衍生型別也走
/// <see cref="DbUpdateExceptionSummaryDestructurer"/>（OPS-LOG-EVENT-SIZE-01，2026-10-19）。
/// <para>
/// 原因：<see cref="ExceptionEnricher"/> 建構時把各解構器的 <c>TargetTypes</c> 放進以型別為鍵的字典，查找用
/// <c>exception.GetType()</c> 精確比對，不看繼承鏈；provider 或應用程式自訂的 <see cref="DbUpdateException"/> 子類別
/// 因此會掉回反射解構，把 EntityEntry 的欄位值、DbContext 與 metadata 整包寫進事件。
/// 衍生型別無法事先列舉（Logger 在 Host 啟動最早期建立，多數組件尚未載入），所以改在事件進來時檢查例外鏈，
/// 遇到尚未登記的衍生型別就以新的型別清單重建一次內部 enricher；每個型別只重建一次，之後走原本的字典查找。
/// </para>
/// </summary>
public sealed class YuuExceptionDetailsEnricher : ILogEventEnricher
{
    /// <summary>
    /// 檢查例外鏈時最多走訪的例外數，防止循環參照或超大 AggregateException 拖慢記錄。
    /// </summary>
    private const int MaxInspectedExceptions = 32;

    private readonly int _destructuringDepth;
    private readonly object _syncRoot = new();
    private volatile EnricherState _state;

    /// <summary>
    /// 建立 enricher；<paramref name="destructuringDepth"/> 為例外詳情解構深度上限。
    /// </summary>
    public YuuExceptionDetailsEnricher(int destructuringDepth)
    {
        _destructuringDepth = destructuringDepth;
        _state = CreateState(new HashSet<Type>());
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Exception is not null)
        {
            RegisterDerivedDbUpdateExceptionTypes(logEvent.Exception);
        }

        _state.Enricher.Enrich(logEvent, propertyFactory);
    }

    private void RegisterDerivedDbUpdateExceptionTypes(Exception exception)
    {
        var current = _state;
        var unknownTypes = EnumerateExceptions(exception)
            .Select(item => item.GetType())
            .Where(type => typeof(DbUpdateException).IsAssignableFrom(type)
                && type != typeof(DbUpdateException)
                && type != typeof(DbUpdateConcurrencyException)
                && !current.DerivedTypes.Contains(type))
            .ToList();
        if (unknownTypes.Count == 0)
        {
            return;
        }

        lock (_syncRoot)
        {
            // 鎖內重讀：另一條執行緒可能剛登記過同一型別。
            var merged = new HashSet<Type>(_state.DerivedTypes);
            var added = false;
            foreach (var type in unknownTypes)
            {
                added |= merged.Add(type);
            }

            if (!added)
            {
                return;
            }

            _state = CreateState(merged);
        }
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception root)
    {
        // 只沿 InnerException／AggregateException.InnerExceptions 走；藏在 Data 或其他屬性的例外極少見，不為它付代價。
        var pending = new Stack<Exception>();
        pending.Push(root);
        var inspected = 0;
        while (pending.Count > 0 && inspected < MaxInspectedExceptions)
        {
            var exception = pending.Pop();
            inspected++;
            yield return exception;

            if (exception is AggregateException aggregateException)
            {
                foreach (var inner in aggregateException.InnerExceptions)
                {
                    pending.Push(inner);
                }
            }
            else if (exception.InnerException is not null)
            {
                pending.Push(exception.InnerException);
            }
        }
    }

    private EnricherState CreateState(IReadOnlySet<Type> derivedTypes)
    {
        var options = new DestructuringOptionsBuilder()
            .WithDefaultDestructurers()
            .WithDestructurers([new DbUpdateExceptionSummaryDestructurer(derivedTypes)])
            .WithDestructuringDepth(_destructuringDepth);
        return new EnricherState(derivedTypes, new ExceptionEnricher(options));
    }

    /// <summary>
    /// 已登記衍生型別與對應的內部 enricher 必須一起替換，所以包成單一不可變物件以 volatile 欄位發布。
    /// </summary>
    private sealed class EnricherState(IReadOnlySet<Type> derivedTypes, ExceptionEnricher enricher)
    {
        public IReadOnlySet<Type> DerivedTypes { get; } = derivedTypes;

        public ExceptionEnricher Enricher { get; } = enricher;
    }
}
