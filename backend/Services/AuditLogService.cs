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

namespace Kitsune.Backend.Services
{
    public enum AuditAction
    {
        Generate, Validate, Preview, Backup, Rollback, Apply, SchemaExtract, RiskAnalysis
    }

    public class AuditEntry
    {
        public long      Id          { get; set; }
        public string    Action      { get; set; } = "";
        public string    ObjectName  { get; set; } = "";
        public string    ObjectType  { get; set; } = "";
        public string    DatabaseName{ get; set; } = "";
        public string    Status      { get; set; } = "";   // SUCCESS | FAIL | WARN
        public string    RequestJson { get; set; } = "";
        public string    ResultJson  { get; set; } = "";
        public string    ModelUsed   { get; set; } = "";
        public double    DurationMs  { get; set; }
        public string    UserAgent   { get; set; } = "";
        public string    ClientIp    { get; set; } = "";
        public DateTime  CreatedAt   { get; set; }
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
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        public async Task EnsureTableAsync()
        {
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='KitsuneAuditLog')
                BEGIN
                    CREATE TABLE dbo.KitsuneAuditLog (
                        Id           BIGINT IDENTITY(1,1) PRIMARY KEY,
                        Action       NVARCHAR(64)    NOT NULL,
                        ObjectName   NVARCHAR(256)   NOT NULL DEFAULT '',
                        ObjectType   NVARCHAR(64)    NOT NULL DEFAULT '',
                        DatabaseName NVARCHAR(128)   NOT NULL DEFAULT '',
                        Status       NVARCHAR(32)    NOT NULL DEFAULT '',
                        RequestJson  NVARCHAR(MAX)   NOT NULL DEFAULT '',
                        ResultJson   NVARCHAR(MAX)   NOT NULL DEFAULT '',
                        ModelUsed    NVARCHAR(128)   NOT NULL DEFAULT '',
                        DurationMs   FLOAT           NOT NULL DEFAULT 0,
                        UserAgent    NVARCHAR(512)   NOT NULL DEFAULT '',
                        ClientIp     NVARCHAR(64)    NOT NULL DEFAULT '',
                        CreatedAt    DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                    CREATE INDEX IX_KAL_ObjectName ON dbo.KitsuneAuditLog (ObjectName, CreatedAt DESC);
                    CREATE INDEX IX_KAL_Action     ON dbo.KitsuneAuditLog (Action, CreatedAt DESC);
                END";

            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(ddl, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task LogAsync(
            AuditAction action, string objectName, string objectType,
            string status, object? request, object? result,
            string modelUsed = "", double durationMs = 0, string database = "")
        {
            try
            {
                const string sql = @"
                    INSERT INTO dbo.KitsuneAuditLog
                        (Action, ObjectName, ObjectType, DatabaseName, Status,
                         RequestJson, ResultJson, ModelUsed, DurationMs, CreatedAt)
                    VALUES
                        (@Action, @ObjectName, @ObjectType, @DatabaseName, @Status,
                         @RequestJson, @ResultJson, @ModelUsed, @DurationMs, SYSUTCDATETIME());";

                await using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Action",       action.ToString());
                cmd.Parameters.AddWithValue("@ObjectName",   objectName);
                cmd.Parameters.AddWithValue("@ObjectType",   objectType);
                cmd.Parameters.AddWithValue("@DatabaseName", database);
                cmd.Parameters.AddWithValue("@Status",       status);
                cmd.Parameters.AddWithValue("@RequestJson",  request is null ? "" : JsonSerializer.Serialize(request));
                cmd.Parameters.AddWithValue("@ResultJson",   result  is null ? "" : JsonSerializer.Serialize(result));
                cmd.Parameters.AddWithValue("@ModelUsed",    modelUsed);
                cmd.Parameters.AddWithValue("@DurationMs",   durationMs);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Audit log write failed (non-fatal)");
            }
        }

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
