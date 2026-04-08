// ============================================================
// KITSUNE – Schema Extraction Service
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
    public interface ISchemaExtractionService
    {
        Task<DatabaseSchema> ExtractSqlServerSchemaAsync(string? databaseName = null);
        Task<DatabaseSchema> ExtractMongoSchemaAsync(string databaseName);
        Task<TableSchema>    ExtractSingleTableAsync(string tableName);
        Task<string>         GenerateDDLSummaryAsync(DatabaseSchema schema);
    }

    public class SchemaExtractionService : ISchemaExtractionService
    {
        private readonly string _sqlConnString;
        private readonly string _mongoConnString;
        private readonly ILogger<SchemaExtractionService> _log;

        public SchemaExtractionService(IConfiguration cfg, ILogger<SchemaExtractionService> log)
        {
            _sqlConnString   = cfg.GetConnectionString("SqlServer") ?? "";
            _mongoConnString = cfg.GetConnectionString("MongoDB")   ?? "mongodb://localhost:27017";
            _log = log;
        }

        public async Task<DatabaseSchema> ExtractSqlServerSchemaAsync(string? databaseName = null)
        {
            var schema = new DatabaseSchema { DatabaseType = "SqlServer" };
            await using var conn = new SqlConnection(_sqlConnString);
            await conn.OpenAsync();
            schema.DatabaseName = databaseName ?? conn.Database;
            schema.Tables       = await ExtractTablesAsync(conn);
            schema.Views        = await ExtractViewsAsync(conn);
            schema.Procedures   = await ExtractProceduresAsync(conn);
            schema.Functions    = await ExtractFunctionsAsync(conn);
            return schema;
        }

        private async Task<List<TableSchema>> ExtractTablesAsync(SqlConnection conn)
        {
            const string sql = @"
                SELECT TABLE_SCHEMA, TABLE_NAME,
                    (SELECT SUM(row_count) FROM sys.dm_db_partition_stats
                     WHERE object_id=OBJECT_ID(TABLE_SCHEMA+'.'+TABLE_NAME) AND index_id IN(0,1)) AS RowCount
                FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_SCHEMA,TABLE_NAME;";
            var tables = new List<TableSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                tables.Add(new TableSchema
                {
                    SchemaName = r["TABLE_SCHEMA"].ToString()!,
                    TableName  = r["TABLE_NAME"].ToString()!,
                    RowCount   = r["RowCount"] == DBNull.Value ? 0 : Convert.ToInt64(r["RowCount"]),
                });
            foreach (var t in tables)
            {
                t.Columns     = await ExtractColumnsAsync(conn, t.SchemaName, t.TableName);
                t.Indexes     = await ExtractIndexesAsync(conn, t.SchemaName, t.TableName);
                t.ForeignKeys = await ExtractForeignKeysAsync(conn, t.SchemaName, t.TableName);
            }
            return tables;
        }

        private static async Task<List<ColumnSchema>> ExtractColumnsAsync(SqlConnection conn, string schema, string table)
        {
            const string sql = @"
                SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, COLUMN_DEFAULT,
                    COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA+'.'+TABLE_NAME), COLUMN_NAME, 'IsIdentity') AS IsIdentity
                FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@S AND TABLE_NAME=@T ORDER BY ORDINAL_POSITION;";
            var cols = new List<ColumnSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@S", schema);
            cmd.Parameters.AddWithValue("@T", table);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                cols.Add(new ColumnSchema
                {
                    ColumnName  = r["COLUMN_NAME"].ToString()!,
                    DataType    = r["DATA_TYPE"].ToString()!,
                    MaxLength   = r["CHARACTER_MAXIMUM_LENGTH"] == DBNull.Value ? null : Convert.ToInt32(r["CHARACTER_MAXIMUM_LENGTH"]),
                    IsNullable  = r["IS_NULLABLE"].ToString() == "YES",
                    DefaultValue= r["COLUMN_DEFAULT"]?.ToString(),
                    IsIdentity  = Convert.ToBoolean(r["IsIdentity"]),
                });
            return cols;
        }

        private async Task<List<IndexSchema>> ExtractIndexesAsync(SqlConnection conn, string schema, string table)
        {
            const string sql = @"
                SELECT i.name, i.type_desc, i.is_unique, i.is_primary_key,
                    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                INNER JOIN sys.columns c        ON c.object_id=i.object_id  AND c.column_id=ic.column_id
                WHERE i.object_id=OBJECT_ID(@SN) AND i.name IS NOT NULL
                GROUP BY i.name, i.type_desc, i.is_unique, i.is_primary_key
                ORDER BY i.name;";
            var idxs = new List<IndexSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SN", $"{schema}.{table}");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                idxs.Add(new IndexSchema
                {
                    IndexName    = r["name"].ToString()!,
                    IndexType    = r["type_desc"].ToString()!,
                    IsUnique     = Convert.ToBoolean(r["is_unique"]),
                    IsPrimaryKey = Convert.ToBoolean(r["is_primary_key"]),
                    Columns      = r["Columns"].ToString()!,
                });
            return idxs;
        }

        private async Task<List<FKSchema>> ExtractForeignKeysAsync(SqlConnection conn, string schema, string table)
        {
            const string sql = @"
                SELECT fk.name, COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ChildCol,
                    OBJECT_SCHEMA_NAME(fkc.referenced_object_id) AS RefSchema,
                    OBJECT_NAME(fkc.referenced_object_id) AS RefTable,
                    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS RefCol
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
                WHERE fk.parent_object_id=OBJECT_ID(@SN);";
            var fks = new List<FKSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SN", $"{schema}.{table}");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                fks.Add(new FKSchema
                {
                    ConstraintName = r["name"].ToString()!,
                    ColumnName     = r["ChildCol"].ToString()!,
                    RefSchema      = r["RefSchema"].ToString()!,
                    RefTable       = r["RefTable"].ToString()!,
                    RefColumn      = r["RefCol"].ToString()!,
                });
            return fks;
        }

        private static Task<long> GetRowCountAsync(SqlConnection conn, string schema, string table)
            => Task.FromResult(0L);

        private async Task<List<ViewSchema>> ExtractViewsAsync(SqlConnection conn)
        {
            const string sql = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS ORDER BY TABLE_SCHEMA,TABLE_NAME;";
            var views = new List<ViewSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                views.Add(new ViewSchema
                {
                    SchemaName = r["TABLE_SCHEMA"].ToString()!,
                    ViewName   = r["TABLE_NAME"].ToString()!,
                });
            return views;
        }

        private async Task<List<ProcedureSchema>> ExtractProceduresAsync(SqlConnection conn)
        {
            const string sql = "SELECT ROUTINE_SCHEMA, ROUTINE_NAME FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE='PROCEDURE' ORDER BY ROUTINE_SCHEMA,ROUTINE_NAME;";
            var procs = new List<ProcedureSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                procs.Add(new ProcedureSchema
                {
                    SchemaName     = r["ROUTINE_SCHEMA"].ToString()!,
                    ProcedureName  = r["ROUTINE_NAME"].ToString()!,
                });
            return procs;
        }

        private async Task<List<FunctionSchema>> ExtractFunctionsAsync(SqlConnection conn)
        {
            const string sql = "SELECT ROUTINE_SCHEMA, ROUTINE_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE='FUNCTION' ORDER BY ROUTINE_SCHEMA,ROUTINE_NAME;";
            var funcs = new List<FunctionSchema>();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                funcs.Add(new FunctionSchema
                {
                    SchemaName    = r["ROUTINE_SCHEMA"].ToString()!,
                    FunctionName  = r["ROUTINE_NAME"].ToString()!,
                    ReturnType    = r["DATA_TYPE"]?.ToString() ?? "",
                });
            return funcs;
        }

        public async Task<TableSchema> ExtractSingleTableAsync(string tableName)
        {
            await using var conn = new SqlConnection(_sqlConnString);
            await conn.OpenAsync();
            var parts  = tableName.Split('.');
            var schema = parts.Length > 1 ? parts[0] : "dbo";
            var name   = parts.Length > 1 ? parts[1] : parts[0];
            return new TableSchema
            {
                SchemaName = schema,
                TableName  = name,
                Columns    = await ExtractColumnsAsync(conn, schema, name),
                Indexes    = await ExtractIndexesAsync(conn, schema, name),
                ForeignKeys= await ExtractForeignKeysAsync(conn, schema, name),
            };
        }

        public async Task<DatabaseSchema> ExtractMongoSchemaAsync(string databaseName)
        {
            var schema = new DatabaseSchema { DatabaseType = "MongoDB", DatabaseName = databaseName };
            var client = new MongoClient(_mongoConnString);
            var db     = client.GetDatabase(databaseName);
            var colls  = await (await db.ListCollectionNamesAsync()).ToListAsync();

            foreach (var coll in colls)
            {
                var col   = db.GetCollection<BsonDocument>(coll);
                var sample = await col.Find(FilterDefinition<BsonDocument>.Empty).Limit(5).ToListAsync();
                var fields = new List<MongoFieldSchema>();
                var seen   = new System.Collections.Generic.HashSet<string>();
                foreach (var doc in sample)
                    foreach (var elem in doc)
                        if (seen.Add(elem.Name))
                            fields.Add(new MongoFieldSchema { FieldName = elem.Name, BsonType = elem.Value.BsonType.ToString() });

                schema.Collections.Add(new CollectionSchema
                {
                    CollectionName = coll,
                    Fields         = fields,
                });
            }
            return schema;
        }

        public Task<string> GenerateDDLSummaryAsync(DatabaseSchema schema)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-- Database: {schema.DatabaseName} ({schema.DatabaseType})");
            sb.AppendLine($"-- Tables: {schema.Tables.Count}, Views: {schema.Views.Count}, Procs: {schema.Procedures.Count}");
            foreach (var t in schema.Tables)
            {
                sb.AppendLine($"-- TABLE [{t.SchemaName}].[{t.TableName}] ({t.RowCount:N0} rows, {t.Columns.Count} cols)");
                foreach (var c in t.Columns)
                    sb.AppendLine($"--   {c.ColumnName} {c.DataType}{(c.IsNullable ? "" : " NOT NULL")}");
            }
            return Task.FromResult(sb.ToString());
        }
    }
}
