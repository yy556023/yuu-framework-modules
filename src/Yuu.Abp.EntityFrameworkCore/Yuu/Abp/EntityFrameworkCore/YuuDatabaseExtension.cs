using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Yuu.Abp.EntityFrameworkCore.Modeling;

namespace Yuu.Abp.EntityFrameworkCore;

public static class YuuDatabaseExtension
{
    public static void ConvertNameToSnakeCase(this ModelBuilder builder, bool setComment = true, string? dbTablePrefix = null, string? dbSchema = null)
    {
        var entities = builder.Model.GetEntityTypes().ToList();

        foreach (var entity in entities)
        {
            if (entity.FindPrimaryKey() == null)
            {
                continue;
            }

            // 取得屬性描述並設定為欄位註解
            var clrType = entity.ClrType;

            // EF Core 10 的 Identity passkey data 是 owned type；重新呼叫 Entity(clrType)
            // 會嘗試把它改成一般 entity，導致 design-time model 無法建立。
            if (!entity.IsOwned())
            {
                builder.Entity(clrType)
                    .ConfigureByConvention();

                builder.Entity(clrType)
                    .ConfigureCommentByConvention();

                builder.Entity(clrType)
                    .ApplyObjectExtensionMappings();

                foreach (var prop in clrType.GetProperties())
                {
                    var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description;
                    if (!string.IsNullOrEmpty(description))
                    {
                        builder.Entity(clrType)
                            .Property(prop.Name)
                            .HasComment(description);
                    }
                }
            }

            // JSON-mapped owned type 使用 JSON property name，不可再設定 relational column name。
            if (entity.IsMappedToJson())
            {
                continue;
            }

            // 轉換資料表名稱
            var tableName = entity.GetTableName();
            if (!string.IsNullOrEmpty(tableName))
            {
                var description = clrType.GetCustomAttribute<DescriptionAttribute>()?.Description;
                if (!string.IsNullOrEmpty(description))
                {
                    if (setComment)
                    {
                        entity.SetComment(description);
                        entity.SetSchema(dbSchema);
                        tableName = dbTablePrefix + tableName;
                        entity.SetTableName(ToSnakeCase(tableName));
                    }
                    else
                    {
                        builder.Ignore(clrType);
                    }
                }
                else
                {
                    entity.SetTableName(ToSnakeCase(tableName));
                }
            }

            // 轉換所有欄位名稱
            foreach (var property in entity.GetProperties())
            {
                var columnName = property.Name;
                if (!string.IsNullOrEmpty(columnName))
                {
                    property.SetColumnName(ToSnakeCase(columnName));
                }
            }

            // 轉換所有外鍵 constraint 名稱
            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var fkName = foreignKey.GetConstraintName();
                if (!string.IsNullOrEmpty(fkName))
                {
                    var convertedName = ToSnakeCase(fkName);
                    // MySQL FK 名稱限制為 64 字元
                    if (convertedName.Length > 64)
                    {
                        convertedName = convertedName[..64];
                    }

                    foreignKey.SetConstraintName(convertedName);
                }
            }

            // 轉換所有索引名稱
            foreach (var index in entity.GetIndexes())
            {
                var indexName = index.GetDatabaseName();
                if (!string.IsNullOrEmpty(indexName))
                {
                    var convertedName = ToSnakeCase(indexName);
                    // MySQL 索引名稱限制為 64 字元
                    if (convertedName.Length > 64)
                    {
                        convertedName = convertedName[..64];
                    }

                    index.SetDatabaseName(convertedName);
                }
            }

            var primaryKey = entity.FindPrimaryKey();
            if (primaryKey != null)
            {
                var pkName = primaryKey.GetName();
                if (!string.IsNullOrEmpty(pkName))
                {
                    primaryKey.SetName(ToSnakeCase(pkName));
                }
            }
        }
    }

    /// <summary>
    /// 將 PascalCase 或 camelCase 字串轉為 snake_case
    /// </summary>
    public static string ToSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var builder = new System.Text.StringBuilder();
        var previousCharWasUpper = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (char.IsUpper(c))
            {
                // 如果不是第一個字，且前一個不是底線與不是大寫，才加底線
                if (i > 0 && (!previousCharWasUpper || (i + 1 < input.Length && char.IsLower(input[i + 1]))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
                previousCharWasUpper = true;
            }
            else
            {
                builder.Append(c);
                previousCharWasUpper = false;
            }
        }

        return builder.ToString();
    }
}
