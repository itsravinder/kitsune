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
        // ── SQL SERVER ────────────────────────────────────────

        public async Task<DatabaseSchema> ExtractSqlServerSchemaAsync(string? databaseName = null)
        private async Task<List<TableSchema>> ExtractTablesAsync(SqlConnection conn)
        private async Task<List<IndexSchema>> ExtractIndexesAsync(
            SqlConnection conn, string schema, string table)
        private async Task<List<FKSchema>> ExtractForeignKeysAsync(
            SqlConnection conn, string schema, string table)
        private async Task<long> GetRowCountAsync(SqlConnection conn, string schema, string table)
        private async Task<List<ViewSchema>> ExtractViewsAsync(SqlConnection conn)
        private async Task<List<ProcedureSchema>> ExtractProceduresAsync(SqlConnection conn)
        private async Task<List<FunctionSchema>> ExtractFunctionsAsync(SqlConnection conn)
        // ── Single table quick extract ─────────────────────────

        public async Task<TableSchema> ExtractSingleTableAsync(string tableName)
        // ── MONGODB ───────────────────────────────────────────

        public async Task<DatabaseSchema> ExtractMongoSchemaAsync(string databaseName)
        // ── DDL Summary (fed to AI for context) ───────────────

        public async Task<string> GenerateDDLSummaryAsync(DatabaseSchema schema)
    }
}
