// ============================================================
// KITSUNE – Scheduled Backup Service
// ============================================================
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface IScheduledBackupService
    {
        Task EnsureTableAsync();
        Task<int>                AddScheduleAsync(ScheduleRequest req);
        Task<List<BackupSchedule>> ListSchedulesAsync();
        Task<bool>               ToggleScheduleAsync(int id, bool enabled);
        Task<bool>               DeleteScheduleAsync(int id);
        Task                     RunDueBackupsAsync(CancellationToken ct);
    }

    public class ScheduledBackupService : IScheduledBackupService
    {
        private readonly string _conn;
        private readonly IServiceProvider _sp;
        private readonly ILogger<ScheduledBackupService> _log;

        public ScheduledBackupService(IConfiguration cfg, IServiceProvider sp, ILogger<ScheduledBackupService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _sp   = sp;
            _log  = log;
        }

        public async Task EnsureTableAsync()
        {
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KitsuneBackupSchedules')
                    CREATE TABLE dbo.KitsuneBackupSchedules (
                        Id          INT IDENTITY(1,1) PRIMARY KEY,
                        ObjectName  NVARCHAR(256) NOT NULL,
                        ObjectType  NVARCHAR(64)  NOT NULL DEFAULT 'PROCEDURE',
                        FrequencyMins INT          NOT NULL DEFAULT 60,
                        IsEnabled   BIT           NOT NULL DEFAULT 1,
                        LastRunAt   DATETIME2     NULL,
                        LastStatus  NVARCHAR(50)  NULL,
                        CreatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
                    );";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await new SqlCommand(ddl, conn).ExecuteNonQueryAsync();
        }

        public async Task<int> AddScheduleAsync(ScheduleRequest req)
        {
            const string sql = @"
                INSERT INTO dbo.KitsuneBackupSchedules (ObjectName, ObjectType, FrequencyMins, IsEnabled)
                VALUES (@N, @T, @F, 1);
                SELECT SCOPE_IDENTITY();";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@N", req.ObjectName);
            cmd.Parameters.AddWithValue("@T", req.ObjectType ?? "PROCEDURE");
            cmd.Parameters.AddWithValue("@F", req.FrequencyMinutes > 0 ? req.FrequencyMinutes : 60);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<List<BackupSchedule>> ListSchedulesAsync()
        {
            const string sql = "SELECT Id, ObjectName, ObjectType, FrequencyMins, IsEnabled, LastRunAt, LastStatus, CreatedAt FROM dbo.KitsuneBackupSchedules ORDER BY ObjectName;";
            var list = new List<BackupSchedule>();
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new BackupSchedule
                {
                    Id             = Convert.ToInt32(r["Id"]),
                    ObjectName     = r["ObjectName"].ToString()!,
                    ObjectType     = r["ObjectType"].ToString()!,
                    FrequencyMins  = Convert.ToInt32(r["FrequencyMins"]),
                    IsEnabled      = Convert.ToBoolean(r["IsEnabled"]),
                    LastRunAt      = r["LastRunAt"] == DBNull.Value ? null : Convert.ToDateTime(r["LastRunAt"]),
                    LastStatus     = r["LastStatus"]?.ToString(),
                    CreatedAt      = Convert.ToDateTime(r["CreatedAt"]),
                });
            return list;
        }

        public async Task<bool> ToggleScheduleAsync(int id, bool enabled)
        {
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("UPDATE dbo.KitsuneBackupSchedules SET IsEnabled=@E WHERE Id=@Id;", conn);
            cmd.Parameters.AddWithValue("@E",  enabled);
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteScheduleAsync(int id)
        {
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM dbo.KitsuneBackupSchedules WHERE Id=@Id;", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task RunDueBackupsAsync(CancellationToken ct)
        {
            const string sql = @"
                SELECT Id, ObjectName, ObjectType
                FROM dbo.KitsuneBackupSchedules
                WHERE IsEnabled = 1
                  AND (LastRunAt IS NULL OR DATEDIFF(MINUTE, LastRunAt, SYSUTCDATETIME()) >= FrequencyMins);";

            var due = new List<(int, string, string)>();
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                due.Add((Convert.ToInt32(r["Id"]), r["ObjectName"].ToString()!, r["ObjectType"].ToString()!));

            foreach (var (id, name, type) in due)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    using var scope  = _sp.CreateScope();
                    var backupSvc    = scope.ServiceProvider.GetRequiredService<IBackupVersioningService>();
                    await backupSvc.BackupAsync(new BackupRequest { ObjectName = name, ObjectType = type }, "scheduler");
                    await UpdateLastRunAsync(id, "OK");
                    _log.LogInformation("Scheduled backup OK: {Object}", name);
                }
                catch (Exception ex)
                {
                    await UpdateLastRunAsync(id, "FAILED");
                    _log.LogError(ex, "Scheduled backup failed: {Object}", name);
                }
            }
        }

        private async Task UpdateLastRunAsync(int id, string status)
        {
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE dbo.KitsuneBackupSchedules SET LastRunAt=SYSUTCDATETIME(), LastStatus=@S WHERE Id=@Id;", conn);
            cmd.Parameters.AddWithValue("@S",  status);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public class BackupSchedulerWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<BackupSchedulerWorker> _log;

        public BackupSchedulerWorker(IServiceProvider sp, ILogger<BackupSchedulerWorker> log)
        {
            _sp  = sp;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("Backup scheduler started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IScheduledBackupService>();
                    await svc.RunDueBackupsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Backup scheduler cycle failed.");
                }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
