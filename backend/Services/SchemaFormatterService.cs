// ============================================================
// KITSUNE – SchemaFormatterService
// Two output formats from the same DatabaseSchema object:
//
//  1. LLM FORMAT  (SQLCoder prompt-compatible)
//     Customers(CustomerId int PK, FullName nvarchar NOT NULL)
//     Orders(OrderId int PK, CustomerId int FK→Customers.CustomerId)
//
//  2. UI FORMAT   (SchemaTab-compatible JSON shape)
//     schema.tables[].name          (string)
//     schema.tables[].schema        (string)
//     schema.tables[].rowCount      (long)
//     schema.tables[].columns[]     { name, dataType, isPrimaryKey, isNullable, isIdentity }
//     schema.tables[].foreignKeys[] { foreignKeyColumn, referencedTable, referencedColumn }
//     schema.tables[].indexes[]     { name, type, isUnique, isPrimaryKey, columns }
//     schema.views[].name / .schema
//     schema.procedures[].name / .schema
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface ISchemaFormatterService
    {
        /// <summary>
        /// SQLCoder-compatible schema string.
        /// One line per table: TableName(col1 type [PK], col2 type [FK→Ref])
        /// </summary>
        string ToLlmFormat(DatabaseSchema schema, IEnumerable<string>? filterTables = null);

        /// <summary>
        /// UI-compatible schema object that SchemaTab reads directly.
        /// Property names match exactly what Panels.jsx expects.
        /// </summary>
        SchemaUiDto ToUiFormat(DatabaseSchema schema);
    }

    // ── UI DTO types — property names match Panels.jsx exactly ──
    public class SchemaUiDto
    {
        public string                   databaseName { get; set; } = "";
        public string                   databaseType { get; set; } = "SqlServer";
        public List<TableUiDto>         tables       { get; set; } = new();
        public List<ViewUiDto>          views        { get; set; } = new();
        public List<ProcedureUiDto>     procedures   { get; set; } = new();
        public List<FunctionUiDto>      functions    { get; set; } = new();
        public DateTime                 extractedAt  { get; set; } = DateTime.UtcNow;
    }

    public class TableUiDto
    {
        public string             name        { get; set; } = "";
        public string             schema      { get; set; } = "dbo";
        public long               rowCount    { get; set; }
        public List<ColumnUiDto>  columns     { get; set; } = new();
        public List<FkUiDto>      foreignKeys { get; set; } = new();
        public List<IndexUiDto>   indexes     { get; set; } = new();
    }

    public class ColumnUiDto
    {
        public string  name         { get; set; } = "";
        public string  dataType     { get; set; } = "";
        public bool    isPrimaryKey { get; set; }
        public bool    isNullable   { get; set; }
        public bool    isIdentity   { get; set; }
        public string? defaultValue { get; set; }
        public int?    maxLength    { get; set; }
    }

    public class FkUiDto
    {
        public string foreignKeyColumn  { get; set; } = "";
        public string referencedTable   { get; set; } = "";
        public string referencedColumn  { get; set; } = "";
        public string referencedSchema  { get; set; } = "dbo";
        public string constraintName    { get; set; } = "";
    }

    public class IndexUiDto
    {
        public string name         { get; set; } = "";
        public string type         { get; set; } = "";
        public bool   isUnique     { get; set; }
        public bool   isPrimaryKey { get; set; }
        public string columns      { get; set; } = "";
    }

    public class ViewUiDto
    {
        public string name   { get; set; } = "";
        public string schema { get; set; } = "dbo";
    }

    public class ProcedureUiDto
    {
        public string name   { get; set; } = "";
        public string schema { get; set; } = "dbo";
    }

    public class FunctionUiDto
    {
        public string name       { get; set; } = "";
        public string schema     { get; set; } = "dbo";
        public string returnType { get; set; } = "";
    }

    // ── Service implementation ────────────────────────────────
    public class SchemaFormatterService : ISchemaFormatterService
    {
        // ── LLM format ────────────────────────────────────────
        public string ToLlmFormat(DatabaseSchema schema, IEnumerable<string>? filterTables = null)
        {
            var filter = filterTables?.Select(t => t.ToLowerInvariant()).ToHashSet();
            var sb = new StringBuilder();

            foreach (var table in schema.Tables)
            {
                if (filter != null
                    && !filter.Contains(table.TableName.ToLowerInvariant())
                    && !filter.Contains($"{table.SchemaName}.{table.TableName}".ToLowerInvariant()))
                    continue;

                // Identify primary key columns from indexes
                var pkCols = table.Indexes
                    .Where(i => i.IsPrimaryKey)
                    .SelectMany(i => i.Columns.Split(',').Select(c => c.Trim()))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Identify FK columns
                var fkMap = table.ForeignKeys.ToDictionary(
                    fk => fk.ColumnName,
                    fk => $"{fk.RefTable}.{fk.RefColumn}",
                    StringComparer.OrdinalIgnoreCase);

                sb.Append($"{table.SchemaName}.{table.TableName}(");
                var cols = table.Columns.Select(c =>
                {
                    var parts = new StringBuilder();
                    parts.Append($"{c.ColumnName} {c.DataType.ToUpperInvariant()}");
                    if (c.IsIdentity)        parts.Append(" IDENTITY");
                    if (pkCols.Contains(c.ColumnName)) parts.Append(" PK");
                    if (!c.IsNullable)       parts.Append(" NOT NULL");
                    if (fkMap.TryGetValue(c.ColumnName, out var ref_))
                        parts.Append($" FK→{ref_}");
                    return parts.ToString();
                });
                sb.Append(string.Join(", ", cols));
                sb.AppendLine(")");
            }

            // Append views briefly
            foreach (var v in schema.Views)
                sb.AppendLine($"-- VIEW {v.SchemaName}.{v.ViewName}");

            return sb.ToString().TrimEnd();
        }

        // ── UI format ─────────────────────────────────────────
        public SchemaUiDto ToUiFormat(DatabaseSchema schema)
        {
            var dto = new SchemaUiDto
            {
                databaseName = schema.DatabaseName,
                databaseType = schema.DatabaseType,
                extractedAt  = schema.ExtractedAt,
            };

            foreach (var t in schema.Tables)
            {
                var pkCols = t.Indexes
                    .Where(i => i.IsPrimaryKey)
                    .SelectMany(i => i.Columns.Split(',').Select(c => c.Trim()))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                dto.tables.Add(new TableUiDto
                {
                    name     = t.TableName,
                    schema   = t.SchemaName,
                    rowCount = t.RowCount,
                    columns  = t.Columns.Select(c => new ColumnUiDto
                    {
                        name         = c.ColumnName,
                        dataType     = c.DataType.ToUpperInvariant(),
                        isPrimaryKey = pkCols.Contains(c.ColumnName) || c.IsPrimaryKey,
                        isNullable   = c.IsNullable,
                        isIdentity   = c.IsIdentity,
                        defaultValue = c.DefaultValue,
                        maxLength    = c.MaxLength,
                    }).ToList(),
                    foreignKeys = t.ForeignKeys.Select(fk => new FkUiDto
                    {
                        foreignKeyColumn = fk.ColumnName,
                        referencedTable  = fk.RefTable,
                        referencedColumn = fk.RefColumn,
                        referencedSchema = fk.RefSchema,
                        constraintName   = fk.ConstraintName,
                    }).ToList(),
                    indexes = t.Indexes.Select(i => new IndexUiDto
                    {
                        name         = i.IndexName,
                        type         = i.IndexType,
                        isUnique     = i.IsUnique,
                        isPrimaryKey = i.IsPrimaryKey,
                        columns      = i.Columns,
                    }).ToList(),
                });
            }

            dto.views      = schema.Views.Select(v  => new ViewUiDto      { name = v.ViewName,      schema = v.SchemaName }).ToList();
            dto.procedures = schema.Procedures.Select(p => new ProcedureUiDto { name = p.ProcedureName, schema = p.SchemaName }).ToList();
            dto.functions  = schema.Functions.Select(f  => new FunctionUiDto  { name = f.FunctionName, schema = f.SchemaName, returnType = f.ReturnType }).ToList();

            return dto;
        }
    }
}
