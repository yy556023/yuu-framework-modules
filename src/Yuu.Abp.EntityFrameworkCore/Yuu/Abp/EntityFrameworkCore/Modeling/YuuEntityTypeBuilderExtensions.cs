using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Yuu.Abp.EntityFrameworkCore.Modeling;

public static class YuuEntityTypeBuilderExtensions
{
    public static void ConfigureCommentByConvention(this EntityTypeBuilder b)
    {
        var keyType = b.Metadata.FindPrimaryKey()?.Properties[0].ClrType;

        if (keyType == null)
        {
            return;
        }

        var method = (typeof(YuuEntityTypeBuilderExtensions)
            .GetMethod(nameof(ConfigureCommentByConventionGeneric), BindingFlags.Public | BindingFlags.Static)
            ?.MakeGenericMethod(keyType))
            ?? throw new InvalidOperationException($"找不到 {nameof(ConfigureCommentByConventionGeneric)} 方法。");

        try
        {
            method.Invoke(null, [b]);
        }
        catch (TargetInvocationException ex)
        {
            throw new InvalidOperationException($"呼叫 ConfigureCommentByConventionGeneric<{keyType.Name}> 發生例外。", ex);
        }
    }

    public static void ConfigureCommentByConventionGeneric<TKey>(this EntityTypeBuilder b)
    {
        b.TryConfigureId<TKey>();
        b.TryConfigureConcurrencyStamp();
        b.TryConfigureExtraProperties();
        b.TryConfigureMayHaveCreator();
        b.TryConfigureMustHaveCreator();
        b.TryConfigureSoftDelete();
        b.TryConfigureDeletionTime();
        b.TryConfigureDeletionAudited();
        b.TryConfigureCreationTime();
        b.TryConfigureLastModificationTime();
        b.TryConfigureModificationAudited();
        b.TryConfigureMultiTenant();
    }

    public static void TryConfigureId<TKey>(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IEntity<TKey>>())
        {
            b.Property(nameof(IEntity<TKey>.Id))
                .HasComment("主鍵");
        }
    }

    public static void TryConfigureConcurrencyStamp(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IHasConcurrencyStamp>())
        {
            b.Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp))
                .HasComment("併發戳記");
        }
    }

    public static void TryConfigureExtraProperties(this EntityTypeBuilder b)
    {
        if (!b.Metadata.ClrType.IsAssignableTo<IHasExtraProperties>())
        {
            return;
        }

        b.Property<ExtraPropertyDictionary>(nameof(IHasExtraProperties.ExtraProperties))
            .HasComment("擴充屬性");
    }

    public static void TryConfigureSoftDelete(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<ISoftDelete>())
        {
            b.Property(nameof(ISoftDelete.IsDeleted))
                .HasComment("是否已刪除");
        }
    }

    public static void TryConfigureDeletionTime(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IHasDeletionTime>())
        {
            b.TryConfigureSoftDelete();
            b.Property(nameof(IHasDeletionTime.DeletionTime))
                .HasComment("刪除時間");
        }
    }

    public static void TryConfigureMayHaveCreator(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IMayHaveCreator>())
        {
            b.Property(nameof(IMayHaveCreator.CreatorId))
                .HasComment("建立者 ID");
        }
    }

    public static void TryConfigureMustHaveCreator(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IMustHaveCreator>())
        {
            b.Property(nameof(IMustHaveCreator.CreatorId))
                .HasComment("建立者 ID");
        }
    }

    public static void TryConfigureDeletionAudited(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IDeletionAuditedObject>())
        {
            b.TryConfigureDeletionTime();
            b.Property(nameof(IDeletionAuditedObject.DeleterId))
                .HasComment("刪除者 ID");
        }
    }

    public static void TryConfigureCreationTime(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IHasCreationTime>())
        {
            b.Property(nameof(IHasCreationTime.CreationTime))
                .HasComment("建立時間");
        }
    }

    public static void TryConfigureLastModificationTime(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IHasModificationTime>())
        {
            b.Property(nameof(IHasModificationTime.LastModificationTime))
                .HasComment("最後修改時間");
        }
    }

    public static void TryConfigureModificationAudited(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IModificationAuditedObject>())
        {
            b.TryConfigureLastModificationTime();
            b.Property(nameof(IModificationAuditedObject.LastModifierId))
                .HasComment("最後修改者 ID");
        }
    }

    public static void TryConfigureMultiTenant(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IMultiTenant>())
        {
            b.Property(nameof(IMultiTenant.TenantId))
                .HasComment("租戶 ID");
        }
    }
}
