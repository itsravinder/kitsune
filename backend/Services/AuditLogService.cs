// ============================================================
// KITSUNE – Audit Log Service
// Persists every action: generate, validate, preview, backup,
// rollback, apply — with full request/response payloads.
// ============================================================
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public enum AuditAction
    {
        Generate, Validate, Preview, Backup, Rollback, Apply, SchemaExtract, RiskAnalysis
    }

    public interface IAuditLogService
    {
        Task             LogAsync(AuditAction action, string objectName, string objectType,
                                  string status, object? request, object? result,
                                  string modelUsed = "", double durationMs = 0,
                                  string database = "");
        Task<List<AuditEntry>> GetLogsAsync(string? objectName = null, int top = 100);
        Task             EnsureTableAsync();
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly string _conn;
        private readonly ILogger<AuditLogService> _log;

        public AuditLogService(IConfiguration cfg, ILogger<AuditLogService> log)
        public async Task EnsureTableAsync()
        public async Task LogAsync(
            AuditAction action, string objectName, string objectType,
            string status, object? request, object? result,
            string modelUsed = "", double durationMs = 0, string database = "")
        public async Task<List<AuditEntry>> GetLogsAsync(string? objectName = null, int top = 100)
        {
            var wherePart = objectName is not null ? "WHERE ObjectName = @ObjectName" : "";
            var sql = $@"
                SELECT TOP (@Top)
                    Id, Action, ObjectName, ObjectType, DatabaseName,
                    Status, ModelUsed, DurationMs, CreatedAt,
                    LEFT(RequestJson, 500) AS RequestJson,
                    LEFT(ResultJson,  500) AS ResultJson
                FROM dbo.KitsuneAuditLog
                {wherePart}
                ORDER BY CreatedAt DESC;";

            var results = new List<AuditEntry>();
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Top", top);
            if (objectName is not null)
                cmd.Parameters.AddWithValue("@ObjectName", objectName);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                results.Add(new AuditEntry
                {
                    Id          = Convert.ToInt64(r["Id"]),
                    Action      = r["Action"].ToString()!,
                    ObjectName  = r["ObjectName"].ToString()!,
                    ObjectType  = r["ObjectType"].ToString()!,
                    DatabaseName= r["DatabaseName"].ToString()!,
                    Status      = r["Status"].ToString()!,
                    ModelUsed   = r["ModelUsed"].ToString()!,
                    DurationMs  = Convert.ToDouble(r["DurationMs"]),
                    RequestJson = r["RequestJson"].ToString()!,
                    ResultJson  = r["ResultJson"].ToString()!,
                    CreatedAt   = Convert.ToDateTime(r["CreatedAt"]),
                });

            return results;
        }
    }
}
