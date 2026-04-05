// ============================================================
// KITSUNE – Backup & Versioning Service
// ============================================================
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface IBackupVersioningService
    {
        Task<BackupResponse>         BackupAsync(BackupRequest request);
        Task<List<ObjectVersion>>    GetVersionsAsync(string objectName);
        Task<RollbackResponse>       RollbackAsync(RollbackRequest request);
        Task<string?>                GetCurrentDefinitionAsync(string objectName);
        Task                         EnsureVersionTableAsync();
    }

    public class BackupVersioningService : IBackupVersioningService
    {
        private const int MaxVersionsToKeep = 3;

        private readonly string _connectionString;
        private readonly ILogger<BackupVersioningService> _logger;

        public BackupVersioningService(
            IConfiguration config,
            ILogger<BackupVersioningService> logger)
        {
            _connectionString = config.GetConnectionString("SqlServer")
                ?? throw new InvalidOperationException("SqlServer connection string missing.");
            _logger = logger;
        }

        // ── Ensure ObjectVersions table exists ────────────────
        public async Task EnsureVersionTableAsync()
        {
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ObjectVersions' AND schema_id = SCHEMA_ID('dbo'))
                BEGIN
                    CREATE TABLE dbo.ObjectVersions (
                        Id            INT IDENTITY(1,1) PRIMARY KEY,
                        ObjectName    NVARCHAR(256)  NOT NULL,
                        ObjectType    NVARCHAR(64)   NOT NULL,
                        VersionNumber INT            NOT NULL,
                        ScriptContent NVARCHAR(MAX)  NOT NULL,
                        CreatedAt     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
                        CONSTRAINT UQ_ObjectVersions UNIQUE (ObjectName, VersionNumber)
                    );
                    CREATE INDEX IX_OV_ObjectName ON dbo.ObjectVersions (ObjectName, VersionNumber DESC);
                END";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(ddl, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Backup: extract current definition → store version ─
        public async Task<BackupResponse> BackupAsync(BackupRequest request)
        {
            var response = new BackupResponse { ObjectName = request.ObjectName };

            try
            {
                // 1. Get current script
                var currentScript = await GetCurrentDefinitionAsync(request.ObjectName);
                if (currentScript is null)
                {
                    response.Success = false;
                    response.Message = $"Object '{request.ObjectName}' not found or has no SQL definition.";
                    return response;
                }

                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var tran = conn.BeginTransaction();

                try
                {
                    // 2. Determine next version number
                    const string maxVersionSql = @"
                        SELECT ISNULL(MAX(VersionNumber), 0)
                        FROM dbo.ObjectVersions
                        WHERE ObjectName = @ObjectName;";

                    await using var maxCmd = new SqlCommand(maxVersionSql, conn, tran);
                    maxCmd.Parameters.AddWithValue("@ObjectName", request.ObjectName);
                    var maxVersion = (int)(await maxCmd.ExecuteScalarAsync() ?? 0);
                    int nextVersion = maxVersion + 1;

                    // 3. Insert new version
                    const string insertSql = @"
                        INSERT INTO dbo.ObjectVersions (ObjectName, ObjectType, VersionNumber, ScriptContent, CreatedAt)
                        VALUES (@ObjectName, @ObjectType, @VersionNumber, @ScriptContent, SYSUTCDATETIME());";

                    await using var insertCmd = new SqlCommand(insertSql, conn, tran);
                    insertCmd.Parameters.AddWithValue("@ObjectName",    request.ObjectName);
                    insertCmd.Parameters.AddWithValue("@ObjectType",    request.ObjectType);
                    insertCmd.Parameters.AddWithValue("@VersionNumber", nextVersion);
                    insertCmd.Parameters.AddWithValue("@ScriptContent", currentScript);
                    await insertCmd.ExecuteNonQueryAsync();

                    // 4. Purge old versions (keep last 3 only)
                    const string purgeSql = @"
                        DELETE FROM dbo.ObjectVersions
                        WHERE ObjectName = @ObjectName
                          AND VersionNumber NOT IN (
                              SELECT TOP (@Keep) VersionNumber
                              FROM dbo.ObjectVersions
                              WHERE ObjectName = @ObjectName
                              ORDER BY VersionNumber DESC
                          );";

                    await using var purgeCmd = new SqlCommand(purgeSql, conn, tran);
                    purgeCmd.Parameters.AddWithValue("@ObjectName", request.ObjectName);
                    purgeCmd.Parameters.AddWithValue("@Keep",       MaxVersionsToKeep);
                    await purgeCmd.ExecuteNonQueryAsync();

                    await tran.CommitAsync();

                    response.Success       = true;
                    response.VersionNumber = nextVersion;
                    response.Message       = $"Backup created as version {nextVersion}. Older versions pruned to keep last {MaxVersionsToKeep}.";
                }
                catch
                {
                    await tran.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup failed for {Object}", request.ObjectName);
                response.Success = false;
                response.Message = $"Backup failed: {ex.Message}";
            }

            return response;
        }

        // ── Get version history for an object ────────────────
        public async Task<List<ObjectVersion>> GetVersionsAsync(string objectName)
        {
            const string sql = @"
                SELECT TOP 3 Id, ObjectName, ObjectType, VersionNumber, ScriptContent, CreatedAt
                FROM dbo.ObjectVersions
                WHERE ObjectName = @ObjectName
                ORDER BY VersionNumber DESC;";

            var results = new List<ObjectVersion>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ObjectName", objectName);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new ObjectVersion
                {
                    Id            = Convert.ToInt32(reader["Id"]),
                    ObjectName    = reader["ObjectName"]?.ToString()    ?? "",
                    ObjectType    = reader["ObjectType"]?.ToString()    ?? "",
                    VersionNumber = Convert.ToInt32(reader["VersionNumber"]),
                    ScriptContent = reader["ScriptContent"]?.ToString() ?? "",
                    CreatedAt     = Convert.ToDateTime(reader["CreatedAt"]),
                });
            }

            return results;
        }

        // ── Rollback: restore object from a stored version ────
        public async Task<RollbackResponse> RollbackAsync(RollbackRequest request)
        {
            var response = new RollbackResponse();

            try
            {
                // 1. Fetch the target version script
                const string fetchSql = @"
                    SELECT ScriptContent, ObjectType
                    FROM dbo.ObjectVersions
                    WHERE ObjectName = @ObjectName AND VersionNumber = @VersionNumber;";

                string? script     = null;
                string? objectType = null;

                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using (var fetchCmd = new SqlCommand(fetchSql, conn))
                {
                    fetchCmd.Parameters.AddWithValue("@ObjectName",    request.ObjectName);
                    fetchCmd.Parameters.AddWithValue("@VersionNumber", request.VersionNumber);

                    await using var reader = await fetchCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        script     = reader["ScriptContent"]?.ToString();
                        objectType = reader["ObjectType"]?.ToString();
                    }
                }

                if (script is null)
                {
                    response.Success = false;
                    response.Message = $"Version {request.VersionNumber} of '{request.ObjectName}' not found.";
                    return response;
                }

                // 2. Backup current state before rollback
                await BackupAsync(new BackupRequest
                {
                    ObjectName = request.ObjectName,
                    ObjectType = objectType ?? "UNKNOWN"
                });

                // 3. Convert CREATE → ALTER (for SP/Function/View)
                string rollbackScript = PrepareRollbackScript(script, objectType ?? "");

                // 4. Execute the rollback script
                await using var rollbackCmd = new SqlCommand(rollbackScript, conn);
                rollbackCmd.CommandTimeout = 60;
                await rollbackCmd.ExecuteNonQueryAsync();

                response.Success        = true;
                response.Message        = $"Successfully rolled back '{request.ObjectName}' to version {request.VersionNumber}.";
                response.RestoredScript = script;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rollback failed for {Object} v{Version}",
                    request.ObjectName, request.VersionNumber);
                response.Success = false;
                response.Message = $"Rollback failed: {ex.Message}";
            }

            return response;
        }

        // ── Extract current definition from sys.sql_modules ──
        public async Task<string?> GetCurrentDefinitionAsync(string objectName)
        {
            const string sql = @"
                SELECT m.definition
                FROM sys.objects o
                INNER JOIN sys.sql_modules m ON m.object_id = o.object_id
                WHERE o.object_id = OBJECT_ID(@ObjectName);";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ObjectName", objectName);

            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        // ── Helper: rewrite CREATE as ALTER for rollback ──────
        private static string PrepareRollbackScript(string script, string objectType)
        {
            // Strip leading whitespace/newlines
            var trimmed = script.TrimStart();

            // Replace CREATE PROCEDURE / CREATE FUNCTION / CREATE VIEW with ALTER equivalent
            if (trimmed.StartsWith("CREATE ", StringComparison.OrdinalIgnoreCase))
            {
                return "ALTER " + trimmed[7..]; // skip "CREATE "
            }

            return script;
        }
    }
}
