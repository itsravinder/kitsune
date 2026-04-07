// ============================================================
// KITSUNE – Schema Extraction Service
// Supports: SQL Server (tables, views, SPs, functions)
//           MongoDB (collections + inferred field types)
// ============================================================
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    // ── Schema Models ─────────────────────────────────────────

    {
        public string                  DatabaseName { get; set; } = "";
        public string                  DatabaseType { get; set; } = "";
        public List<TableSchema>       Tables       { get; set; } = new();
        public List<ViewSchema>        Views        { get; set; } = new();
        public List<ProcedureSchema>   Procedures   { get; set; } = new();
        public List<FunctionSchema>    Functions    { get; set; } = new();
        public List<CollectionSchema>  Collections  { get; set; } = new();
        public DateTime                ExtractedAt  { get; set; } = DateTime.UtcNow;
        public string                  DDLSummary   { get; set; } = "";
    }

    {
        public string            Schema       { get; set; } = "dbo";
        public string            Name         { get; set; } = "";
        public List<ColumnSchema> Columns     { get; set; } = new();
        public List<IndexSchema>  Indexes     { get; set; } = new();
        public List<FKSchema>     ForeignKeys { get; set; } = new();
        public long              RowCount     { get; set; }
    }

    {
        public string Name          { get; set; } = "";
        public string DataType      { get; set; } = "";
        public bool   IsNullable    { get; set; }
        public bool   IsPrimaryKey  { get; set; }
        public bool   IsIdentity    { get; set; }
        public string? DefaultValue { get; set; }
        public int?   MaxLength     { get; set; }
        public int?   Precision     { get; set; }
        public int?   Scale         { get; set; }
    }

    {
        public string       Name      { get; set; } = "";
        public bool         IsUnique  { get; set; }
        public bool         IsClustered { get; set; }
        public List<string> Columns   { get; set; } = new();
    }

    {
        public string ConstraintName   { get; set; } = "";
        public string ForeignKeyColumn { get; set; } = "";
        public string ReferencedTable  { get; set; } = "";
        public string ReferencedColumn { get; set; } = "";
    }

    {
        public string Schema     { get; set; } = "dbo";
        public string Name       { get; set; } = "";
        public string Definition { get; set; } = "";
    }

    {
        public string             Schema     { get; set; } = "dbo";
        public string             Name       { get; set; } = "";
        public string             Definition { get; set; } = "";
        public List<ParameterInfo> Parameters { get; set; } = new();
        public DateTime           ModifiedAt { get; set; }
    }

    {
        public string             Schema      { get; set; } = "dbo";
        public string             Name        { get; set; } = "";
        public string             Definition  { get; set; } = "";
        public string             ReturnType  { get; set; } = "";
        public List<ParameterInfo> Parameters { get; set; } = new();
    }

    {
        public string                     Name        { get; set; } = "";
        public long                       DocumentCount { get; set; }
        public List<MongoFieldSchema>     Fields      { get; set; } = new();
        public List<MongoIndexSchema>     Indexes     { get; set; } = new();
    }

    {
        public string Name         { get; set; } = "";
        public string InferredType { get; set; } = "";
        public double Frequency    { get; set; }  // fraction of docs that have this field
        public bool   IsNested     { get; set; }
    }

    {
        public string Name   { get; set; } = "";
        public bool   Unique { get; set; }
        public string Keys   { get; set; } = "";
    }

    // ── Interface ─────────────────────────────────────────────

    public interface ISchemaExtractionService
    {
        Task<DatabaseSchema> ExtractSqlServerSchemaAsync(string? databaseName = null);
        Task<DatabaseSchema> ExtractMongoSchemaAsync(string databaseName);
        Task<TableSchema>    ExtractSingleTableAsync(string tableName);
        Task<string>         GenerateDDLSummaryAsync(DatabaseSchema schema);
    }

    // ── Implementation ────────────────────────────────────────

    public class SchemaExtractionService : ISchemaExtractionService
    {
        private readonly string  _sqlConnString;
        private readonly string  _mongoConnString;
        private readonly ILogger<SchemaExtractionService> _log;

        public SchemaExtractionService(IConfiguration cfg, ILogger<SchemaExtractionService> log)
        {
            _sqlConnString   = cfg.GetConnectionString("SqlServer")  ?? "";
            _mongoConnString = cfg.GetConnectionString("MongoDB")    ?? "mongodb://localhost:27017";
            _log = log;
        }

        // ── SQL SERVER ────────────────────────────────────────

        public async Task<DatabaseSchema> ExtractSqlServerSchemaAsync(string? databaseName = null)
        {
            var schema = new DatabaseSchema { DatabaseType = "SqlServer" };

            await using var conn = new SqlConnection(_sqlConnString);
            await conn.OpenAsync();

            schema.DatabaseName = databaseName ?? conn.Database;

            // Switch to target DB if specified
            if (!string.IsNullOrEmpty(databaseName))
                await new SqlCommand($"USE [{databaseName}];", conn).ExecuteNonQueryAsync();

            schema.Tables     = await ExtractTablesAsync(conn);
            schema.Views      = await ExtractViewsAsync(conn);
            schema.Procedures = await ExtractProceduresAsync(conn);
            schema.Functions  = await ExtractFunctionsAsync(conn);
            schema.DDLSummary = await GenerateDDLSummaryAsync(schema);

            return schema;
        }

        private async Task<List<TableSchema>> ExtractTablesAsync(SqlConnection conn)
        {
            const string sql = @"
                SELECT
                    s.name                                  AS SchemaName,
                    t.name                                  AS TableName,
                    c.name                                  AS ColumnName,
                    tp.name                                 AS DataType,
                    c.is_nullable,
                    c.is_identity,
                    c.max_length,
                    c.precision,
                    c.scale,
                    dc.definition                           AS DefaultValue,
                    CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey
                FROM sys.tables t
                INNER JOIN sys.schemas s      ON s.schema_id = t.schema_id
                INNER JOIN sys.columns c      ON c.object_id = t.object_id
                INNER JOIN sys.types tp       ON tp.user_type_id = c.user_type_id
                LEFT JOIN  sys.default_constraints dc ON dc.object_id = c.default_object_id
                LEFT JOIN (
                    SELECT ic.object_id, ic.column_id
                    FROM sys.index_columns ic
                    INNER JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                    WHERE i.is_primary_key = 1
                ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
                ORDER BY s.name, t.name, c.column_id;";

            var tableMap = new Dictionary<string, TableSchema>();

            await using var cmd    = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var key = $"{reader["SchemaName"]}.{reader["TableName"]}";
                if (!tableMap.TryGetValue(key, out var tbl))
                {
                    tbl = new TableSchema
                    {
                        Schema = reader["SchemaName"].ToString()!,
                        Name   = reader["TableName"].ToString()!,
                    };
                    tableMap[key] = tbl;
                }

                tbl.Columns.Add(new ColumnSchema
                {
                    Name         = reader["ColumnName"].ToString()!,
                    DataType     = reader["DataType"].ToString()!,
                    IsNullable   = Convert.ToBoolean(reader["is_nullable"]),
                    IsIdentity   = Convert.ToBoolean(reader["is_identity"]),
                    IsPrimaryKey = Convert.ToInt32(reader["IsPrimaryKey"]) == 1,
                    MaxLength    = reader["max_length"] as int?,
                    Precision    = reader["precision"] is byte b ? (int?)b : null,
                    Scale        = reader["scale"]     is byte sc ? (int?)sc : null,
                    DefaultValue = reader["DefaultValue"]?.ToString(),
                });
            }

            var tables = new List<TableSchema>(tableMap.Values);

            // Attach indexes and foreign keys per table
            foreach (var tbl in tables)
            {
                tbl.Indexes     = await ExtractIndexesAsync(conn,      tbl.Schema, tbl.Name);
                tbl.ForeignKeys = await ExtractForeignKeysAsync(conn,  tbl.Schema, tbl.Name);
                tbl.RowCount    = await GetRowCountAsync(conn,         tbl.Schema, tbl.Name);
            }

            return tables;
        }

        private async Task<List<IndexSchema>> ExtractIndexesAsync(
            SqlConnection conn, string schema, string table)
        {
            const string sql = @"
                SELECT
                    i.name      AS IndexName,
                    i.is_unique,
                    i.type_desc,
                    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Cols
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                INNER JOIN sys.columns c        ON c.object_id  = i.object_id AND c.column_id = ic.column_id
                WHERE i.object_id = OBJECT_ID(@ObjName) AND i.type > 0
                GROUP BY i.name, i.is_unique, i.type_desc;";

            var results = new List<IndexSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ObjName", $"{schema}.{table}");

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                results.Add(new IndexSchema
                {
                    Name        = r["IndexName"].ToString()!,
                    IsUnique    = Convert.ToBoolean(r["is_unique"]),
                    IsClustered = r["type_desc"].ToString() == "CLUSTERED",
                    Columns     = new List<string>(r["Cols"].ToString()!.Split(", ")),
                });
            }
            return results;
        }

        private async Task<List<FKSchema>> ExtractForeignKeysAsync(
            SqlConnection conn, string schema, string table)
        {
            const string sql = @"
                SELECT
                    fk.name                                         AS ConstraintName,
                    COL_NAME(fkc.parent_object_id, fkc.parent_column_id)   AS FKCol,
                    OBJECT_NAME(fkc.referenced_object_id)           AS RefTable,
                    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS RefCol
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                WHERE fk.parent_object_id = OBJECT_ID(@ObjName);";

            var results = new List<FKSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ObjName", $"{schema}.{table}");

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                results.Add(new FKSchema
                {
                    ConstraintName   = r["ConstraintName"].ToString()!,
                    ForeignKeyColumn = r["FKCol"].ToString()!,
                    ReferencedTable  = r["RefTable"].ToString()!,
                    ReferencedColumn = r["RefCol"].ToString()!,
                });
            }
            return results;
        }

        private async Task<long> GetRowCountAsync(SqlConnection conn, string schema, string table)
        {
            const string sql = @"
                SELECT SUM(p.rows)
                FROM sys.partitions p
                WHERE p.object_id = OBJECT_ID(@ObjName) AND p.index_id IN (0, 1);";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ObjName", $"{schema}.{table}");
            var result = await cmd.ExecuteScalarAsync();
            return result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }

        private async Task<List<ViewSchema>> ExtractViewsAsync(SqlConnection conn)
        {
            const string sql = @"
                SELECT s.name AS SchemaName, v.name AS ViewName, m.definition
                FROM sys.views v
                INNER JOIN sys.schemas s    ON s.schema_id = v.schema_id
                INNER JOIN sys.sql_modules m ON m.object_id = v.object_id
                ORDER BY s.name, v.name;";

            var results = new List<ViewSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                results.Add(new ViewSchema
                {
                    Schema     = r["SchemaName"].ToString()!,
                    Name       = r["ViewName"].ToString()!,
                    Definition = r["definition"].ToString()!,
                });
            return results;
        }

        private async Task<List<ProcedureSchema>> ExtractProceduresAsync(SqlConnection conn)
        {
            const string sql = @"
                SELECT s.name AS SchemaName, p.name AS ProcName, m.definition, p.modify_date
                FROM sys.procedures p
                INNER JOIN sys.schemas s     ON s.schema_id = p.schema_id
                INNER JOIN sys.sql_modules m ON m.object_id = p.object_id
                ORDER BY s.name, p.name;";

            var results = new List<ProcedureSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                results.Add(new ProcedureSchema
                {
                    Schema     = r["SchemaName"].ToString()!,
                    Name       = r["ProcName"].ToString()!,
                    Definition = r["definition"].ToString()!,
                    ModifiedAt = Convert.ToDateTime(r["modify_date"]),
                });
            return results;
        }

        private async Task<List<FunctionSchema>> ExtractFunctionsAsync(SqlConnection conn)
        {
            const string sql = @"
                SELECT
                    s.name    AS SchemaName,
                    o.name    AS FuncName,
                    m.definition,
                    o.type_desc
                FROM sys.objects o
                INNER JOIN sys.schemas s     ON s.schema_id = o.schema_id
                INNER JOIN sys.sql_modules m ON m.object_id = o.object_id
                WHERE o.type IN ('FN','IF','TF')
                ORDER BY s.name, o.name;";

            var results = new List<FunctionSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                results.Add(new FunctionSchema
                {
                    Schema     = r["SchemaName"].ToString()!,
                    Name       = r["FuncName"].ToString()!,
                    Definition = r["definition"].ToString()!,
                    ReturnType = r["type_desc"].ToString()!,
                });
            return results;
        }

        // ── Single table quick extract ─────────────────────────

        public async Task<TableSchema> ExtractSingleTableAsync(string tableName)
        {
            await using var conn = new SqlConnection(_sqlConnString);
            await conn.OpenAsync();

            var tables = await ExtractTablesAsync(conn);
            return tables.Find(t =>
                t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase) ||
                $"{t.Schema}.{t.Name}".Equals(tableName, StringComparison.OrdinalIgnoreCase))
                ?? new TableSchema { Name = tableName };
        }

        // ── MONGODB ───────────────────────────────────────────

        public async Task<DatabaseSchema> ExtractMongoSchemaAsync(string databaseName)
        {
            var schema = new DatabaseSchema
            {
                DatabaseName = databaseName,
                DatabaseType = "MongoDB",
            };

            var client  = new MongoClient(_mongoConnString);
            var db      = client.GetDatabase(databaseName);

            var collectionNames = await (await db.ListCollectionNamesAsync()).ToListAsync();

            foreach (var name in collectionNames)
            {
                var collection = db.GetCollection<BsonDocument>(name);
                var count      = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

                // Sample up to 200 docs to infer field schema
                var sample = await collection
                    .Find(FilterDefinition<BsonDocument>.Empty)
                    .Limit(200)
                    .ToListAsync();

                var fieldStats    = new Dictionary<string, (int count, string type)>();
                int totalDocs     = sample.Count;

                foreach (var doc in sample)
                    foreach (var elem in doc.Elements)
                    {
                        var bsonType = elem.Value.BsonType.ToString();
                        if (!fieldStats.ContainsKey(elem.Name))
                            fieldStats[elem.Name] = (0, bsonType);
                        var (cnt, _) = fieldStats[elem.Name];
                        fieldStats[elem.Name] = (cnt + 1, bsonType);
                    }

                var fields = new List<MongoFieldSchema>();
                foreach (var (field, (cnt, type)) in fieldStats)
                    fields.Add(new MongoFieldSchema
                    {
                        Name         = field,
                        InferredType = type,
                        Frequency    = totalDocs > 0 ? Math.Round((double)cnt / totalDocs, 2) : 0,
                        IsNested     = type == "Document",
                    });

                // Get indexes
                var indexes     = new List<MongoIndexSchema>();
                var indexCursor = await collection.Indexes.ListAsync();
                var indexDocs   = await indexCursor.ToListAsync();
                foreach (var idx in indexDocs)
                    indexes.Add(new MongoIndexSchema
                    {
                        Name   = idx.GetValue("name", "").ToString(),
                        Unique = idx.Contains("unique") && idx["unique"].AsBoolean,
                        Keys   = idx["key"].ToJson(),
                    });

                schema.Collections.Add(new CollectionSchema
                {
                    Name          = name,
                    DocumentCount = count,
                    Fields        = fields,
                    Indexes       = indexes,
                });
            }

            schema.DDLSummary = await GenerateDDLSummaryAsync(schema);
            return schema;
        }

        // ── DDL Summary (fed to AI for context) ───────────────

        public async Task<string> GenerateDDLSummaryAsync(DatabaseSchema schema)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-- Database: {schema.DatabaseName} ({schema.DatabaseType})");
            sb.AppendLine($"-- Extracted: {DateTime.UtcNow:u}");
            sb.AppendLine();

            if (schema.DatabaseType == "SqlServer")
            {
                foreach (var tbl in schema.Tables)
                {
                    sb.AppendLine($"CREATE TABLE [{tbl.Schema}].[{tbl.Name}] (");
                    foreach (var col in tbl.Columns)
                    {
                        var pk    = col.IsPrimaryKey ? " PRIMARY KEY" : "";
                        var nn    = col.IsNullable   ? " NULL"        : " NOT NULL";
                        var ident = col.IsIdentity   ? " IDENTITY"    : "";
                        sb.AppendLine($"  [{col.Name}] {col.DataType.ToUpper()}{ident}{nn}{pk},");
                    }
                    // Foreign keys
                    foreach (var fk in tbl.ForeignKeys)
                        sb.AppendLine($"  FOREIGN KEY ([{fk.ForeignKeyColumn}]) REFERENCES [{fk.ReferencedTable}]([{fk.ReferencedColumn}]),");

                    sb.AppendLine($"); -- {tbl.RowCount:N0} rows");
                    sb.AppendLine();
                }

                foreach (var v in schema.Views)
                    sb.AppendLine($"-- VIEW: {v.Schema}.{v.Name}");

                foreach (var p in schema.Procedures)
                    sb.AppendLine($"-- PROCEDURE: {p.Schema}.{p.Name}  (modified: {p.ModifiedAt:d})");
            }
            else // MongoDB
            {
                foreach (var col in schema.Collections)
                {
                    sb.AppendLine($"// Collection: {col.Name} ({col.DocumentCount:N0} documents)");
                    sb.AppendLine("// Fields (inferred):");
                    foreach (var f in col.Fields)
                        sb.AppendLine($"//   {f.Name}: {f.InferredType} (present in {f.Frequency*100:0}% of docs)");
                    sb.AppendLine();
                }
            }

            return await Task.FromResult(sb.ToString());
        }
    }
}
