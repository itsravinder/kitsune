// ============================================================
// KITSUNE – Enhanced Backup & Versioning Service v2
// Table: dbo.KitsuneObjectVersions (enterprise, with CreatedBy)
// Legacy dbo.ObjectVersions kept in sync for backward compat
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
        Task<BackupResponse>      BackupAsync(BackupRequest request, string createdBy = "system");
        Task<List<ObjectVersion>> GetVersionsAsync(string objectName);
        Task<RollbackResponse>    RollbackAsync(RollbackRequest request, string rolledBackBy = "system");
        Task<string?>             GetCurrentDefinitionAsync(string objectName);
        Task                      EnsureVersionTableAsync();
    }

    public class BackupVersioningService : IBackupVersioningService
    {
        private const int MaxVersionsToKeep = 5;

        private readonly string _conn;
        private readonly ILogger<BackupVersioningService> _log;

        public BackupVersioningService(IConfiguration cfg, ILogger<BackupVersioningService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        public async Task EnsureVersionTableAsync()
        {
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='KitsuneObjectVersions')
                    CREATE TABLE dbo.KitsuneObjectVersions (
                        Id            INT IDENTITY(1,1) PRIMARY KEY,
                        ObjectName    NVARCHAR(256)  NOT NULL,
                        ObjectType    NVARCHAR(64)   NOT NULL,
                        VersionNumber INT            NOT NULL,
                        ScriptContent NVARCHAR(MAX)  NOT NULL,
                        CreatedAt     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
                        CreatedBy     NVARCHAR(128)  NOT NULL DEFAULT 'system',
                        CONSTRAINT UQ_KOV UNIQUE (ObjectName,VersionNumber)
                    );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='ObjectVersions')
                    CREATE TABLE dbo.ObjectVersions (
                        Id            INT IDENTITY(1,1) PRIMARY KEY,
                        ObjectName    NVARCHAR(256)  NOT NULL,
                        ObjectType    NVARCHAR(64)   NOT NULL,
                        VersionNumber INT            NOT NULL,
                        ScriptContent NVARCHAR(MAX)  NOT NULL,
                        CreatedAt     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
                        CONSTRAINT UQ_ObjectVersions UNIQUE (ObjectName,VersionNumber)
                    );
                IF NOT EXISTS (SELECT 1 FROM sys.columns
                    WHERE object_id=OBJECT_ID('dbo.ObjectVersions') AND name='CreatedBy')
                    ALTER TABLE dbo.ObjectVersions ADD CreatedBy NVARCHAR(128) NOT NULL DEFAULT 'system';
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_KOV_Name')
                    CREATE INDEX IX_KOV_Name ON dbo.KitsuneObjectVersions(ObjectName,VersionNumber DESC);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OV_ObjectName')
                    CREATE INDEX IX_OV_ObjectName ON dbo.ObjectVersions(ObjectName,VersionNumber DESC);";

            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await new SqlCommand(ddl, conn).ExecuteNonQueryAsync();
        }

        public async Task<BackupResponse> BackupAsync(BackupRequest request, string createdBy = "system")
        {
            var response = new BackupResponse { ObjectName = request.ObjectName };
            try
            {
                var currentScript = await GetCurrentDefinitionAsync(request.ObjectName);
                if (currentScript is null)
                {
                    response.Success = false;
                    response.Message = $"'{request.ObjectName}' not found or has no SQL definition.";
                    return response;
                }

                await using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                await using var tran = conn.BeginTransaction();
                try
                {
                    var maxCmd = new SqlCommand("SELECT ISNULL(MAX(VersionNumber),0) FROM dbo.KitsuneObjectVersions WHERE ObjectName=@N;", conn, tran);
                    maxCmd.Parameters.AddWithValue("@N", request.ObjectName);
                    int nextVer = (int)(await maxCmd.ExecuteScalarAsync() ?? 0) + 1;

                    const string ins = @"
                        INSERT INTO dbo.KitsuneObjectVersions(ObjectName,ObjectType,VersionNumber,ScriptContent,CreatedAt,CreatedBy)
                        VALUES(@N,@T,@V,@S,SYSUTCDATETIME(),@By);
                        IF NOT EXISTS(SELECT 1 FROM dbo.ObjectVersions WHERE ObjectName=@N AND VersionNumber=@V)
                            INSERT INTO dbo.ObjectVersions(ObjectName,ObjectType,VersionNumber,ScriptContent,CreatedAt,CreatedBy)
                            VALUES(@N,@T,@V,@S,SYSUTCDATETIME(),@By);";

                    var insCmd = new SqlCommand(ins, conn, tran);
                    insCmd.Parameters.AddWithValue("@N",  request.ObjectName);
                    insCmd.Parameters.AddWithValue("@T",  request.ObjectType);
                    insCmd.Parameters.AddWithValue("@V",  nextVer);
                    insCmd.Parameters.AddWithValue("@S",  currentScript);
                    insCmd.Parameters.AddWithValue("@By", createdBy);
                    await insCmd.ExecuteNonQueryAsync();

                    const string purge = @"
                        DELETE FROM dbo.KitsuneObjectVersions WHERE ObjectName=@N AND VersionNumber NOT IN(
                            SELECT TOP(@K) VersionNumber FROM dbo.KitsuneObjectVersions WHERE ObjectName=@N ORDER BY VersionNumber DESC);
                        DELETE FROM dbo.ObjectVersions WHERE ObjectName=@N AND VersionNumber NOT IN(
                            SELECT TOP(@K) VersionNumber FROM dbo.ObjectVersions WHERE ObjectName=@N ORDER BY VersionNumber DESC);";

                    var purgeCmd = new SqlCommand(purge, conn, tran);
                    purgeCmd.Parameters.AddWithValue("@N", request.ObjectName);
                    purgeCmd.Parameters.AddWithValue("@K", MaxVersionsToKeep);
                    await purgeCmd.ExecuteNonQueryAsync();

                    await tran.CommitAsync();
                    response.Success       = true;
                    response.VersionNumber = nextVer;
                    response.Message       = $"Backed up as v{nextVer} (by {createdBy}). Retaining last {MaxVersionsToKeep} versions.";
                }
                catch { await tran.RollbackAsync(); throw; }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Backup failed for {Object}", request.ObjectName);
                response.Success = false;
                response.Message = $"Backup failed: {ex.Message}";
            }
            return response;
        }

        public async Task<List<ObjectVersion>> GetVersionsAsync(string objectName)
        {
            const string sql = @"
                SELECT TOP 5 Id,ObjectName,ObjectType,VersionNumber,ScriptContent,CreatedAt
                FROM dbo.KitsuneObjectVersions WHERE ObjectName=@N ORDER BY VersionNumber DESC;";

            var results = new List<ObjectVersion>();
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@N", objectName);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                results.Add(new ObjectVersion
                {
                    Id            = Convert.ToInt32(r["Id"]),
                    ObjectName    = r["ObjectName"].ToString()!,
                    ObjectType    = r["ObjectType"].ToString()!,
                    VersionNumber = Convert.ToInt32(r["VersionNumber"]),
                    ScriptContent = r["ScriptContent"].ToString()!,
                    CreatedAt     = Convert.ToDateTime(r["CreatedAt"]),
                });
            return results;
        }

        public async Task<RollbackResponse> RollbackAsync(RollbackRequest request, string rolledBackBy = "system")
        {
            var response = new RollbackResponse();
            try
            {
                string? script = null; string? objType = null;

                await using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();

                await using (var fetchCmd = new SqlCommand(
                    "SELECT ScriptContent,ObjectType FROM dbo.KitsuneObjectVersions WHERE ObjectName=@N AND VersionNumber=@V;", conn))
                {
                    fetchCmd.Parameters.AddWithValue("@N", request.ObjectName);
                    fetchCmd.Parameters.AddWithValue("@V", request.VersionNumber);
                    await using var r = await fetchCmd.ExecuteReaderAsync();
                    if (await r.ReadAsync()) { script=r["ScriptContent"].ToString(); objType=r["ObjectType"].ToString(); }
                }

                if (script is null)
                {
                    response.Success = false;
                    response.Message = $"Version {request.VersionNumber} of '{request.ObjectName}' not found.";
                    return response;
                }

                // Auto-backup current state before rollback
                await BackupAsync(new BackupRequest { ObjectName=request.ObjectName, ObjectType=objType??"PROCEDURE" },
                    $"pre-rollback/{rolledBackBy}");

                var rollbackScript = script.TrimStart().StartsWith("CREATE ", StringComparison.OrdinalIgnoreCase)
                    ? "ALTER " + script.TrimStart()[7..] : script;

                await using var execCmd = new SqlCommand(rollbackScript, conn) { CommandTimeout=60 };
                await execCmd.ExecuteNonQueryAsync();

                response.Success        = true;
                response.Message        = $"Rolled back '{request.ObjectName}' to v{request.VersionNumber} by {rolledBackBy}.";
                response.RestoredScript = script;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Rollback failed for {Object}", request.ObjectName);
                response.Success = false;
                response.Message = $"Rollback failed: {ex.Message}";
            }
            return response;
        }

        public async Task<string?> GetCurrentDefinitionAsync(string objectName)
        {
            const string sql = @"
                SELECT m.definition FROM sys.objects o
                INNER JOIN sys.sql_modules m ON m.object_id=o.object_id
                WHERE o.object_id=OBJECT_ID(@N);";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@N", objectName);
            return (await cmd.ExecuteScalarAsync())?.ToString();
        }
    }
}
