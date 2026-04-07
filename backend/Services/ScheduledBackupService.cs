// ============================================================
// KITSUNE – Scheduled Backup Service
// Runs as a .NET BackgroundService; backs up configured objects
// on a cron-like schedule stored in SQL Server
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
    // ── Schedule entry ────────────────────────────────────────
    {
        public int      Id              { get; set; }
        public string   ObjectName      { get; set; } = "";
        public string   ObjectType      { get; set; } = "";
        public int      IntervalMinutes { get; set; } = 60;
        public bool     IsEnabled       { get; set; } = true;
        public DateTime? LastRunAt      { get; set; }
        public string   LastStatus      { get; set; } = "";
        public DateTime CreatedAt       { get; set; }
    }

    {
        public string ObjectName      { get; set; } = "";
        public string ObjectType      { get; set; } = "PROCEDURE";
        public int    IntervalMinutes { get; set; } = 60;
    }

    public interface IScheduledBackupService
    {
        Task EnsureTableAsync();
        Task<int>  AddScheduleAsync(ScheduleRequest req);
        Task<List<BackupSchedule>> ListSchedulesAsync();
        Task<bool> ToggleScheduleAsync(int id, bool enabled);
        Task<bool> DeleteScheduleAsync(int id);
        Task RunDueBackupsAsync(CancellationToken ct);
    }

    public class ScheduledBackupService : IScheduledBackupService
    {
        private readonly string _conn;
        private readonly IServiceProvider _sp;
        private readonly ILogger<ScheduledBackupService> _log;

        public ScheduledBackupService(IConfiguration cfg, IServiceProvider sp,
            ILogger<ScheduledBackupService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _sp   = sp;
            _log  = log;
        }

        public async Task EnsureTableAsync()
        {
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='KitsuneBackupSchedules')
                BEGIN
                    CREATE TABLE dbo.KitsuneBackupSchedules (
                        Id              INT IDENTITY(1,1) PRIMARY KEY,
                        ObjectName      NVARCHAR(256)  NOT NULL,
                        ObjectType      NVARCHAR(64)   NOT NULL DEFAULT 'PROCEDURE',
                        IntervalMinutes INT            NOT NULL DEFAULT 60,
                        IsEnabled       BIT            NOT NULL DEFAULT 1,
                        LastRunAt       DATETIME2      NULL,
                        LastStatus      NVARCHAR(64)   NOT NULL DEFAULT '',
                        CreatedAt       DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                END";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await new SqlCommand(ddl, conn).ExecuteNonQueryAsync();
        }

        public async Task<int> AddScheduleAsync(ScheduleRequest req)
        {
            const string sql = @"
                INSERT INTO dbo.KitsuneBackupSchedules (ObjectName, ObjectType, IntervalMinutes)
                VALUES (@Name, @Type, @Interval);
                SELECT SCOPE_IDENTITY();";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name",     req.ObjectName);
            cmd.Parameters.AddWithValue("@Type",     req.ObjectType);
            cmd.Parameters.AddWithValue("@Interval", req.IntervalMinutes);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<List<BackupSchedule>> ListSchedulesAsync()
        {
            const string sql = @"
                SELECT Id, ObjectName, ObjectType, IntervalMinutes,
                       IsEnabled, LastRunAt, LastStatus, CreatedAt
                FROM dbo.KitsuneBackupSchedules
                ORDER BY ObjectName;";
            var results = new List<BackupSchedule>();
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var r = await new SqlCommand(sql, conn).ExecuteReaderAsync();
            while (await r.ReadAsync())
                results.Add(new BackupSchedule
                {
                    Id              = Convert.ToInt32(r["Id"]),
                    ObjectName      = r["ObjectName"].ToString()!,
                    ObjectType      = r["ObjectType"].ToString()!,
                    IntervalMinutes = Convert.ToInt32(r["IntervalMinutes"]),
                    IsEnabled       = Convert.ToBoolean(r["IsEnabled"]),
                    LastRunAt       = r["LastRunAt"] == DBNull.Value ? null : Convert.ToDateTime(r["LastRunAt"]),
                    LastStatus      = r["LastStatus"].ToString()!,
                    CreatedAt       = Convert.ToDateTime(r["CreatedAt"]),
                });
            return results;
        }

        public async Task<bool> ToggleScheduleAsync(int id, bool enabled)
        {
            const string sql = "UPDATE dbo.KitsuneBackupSchedules SET IsEnabled=@E WHERE Id=@Id;";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@E",  enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteScheduleAsync(int id)
        {
            const string sql = "DELETE FROM dbo.KitsuneBackupSchedules WHERE Id=@Id;";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task RunDueBackupsAsync(CancellationToken ct)
        {
            const string sql = @"
                SELECT Id, ObjectName, ObjectType, IntervalMinutes
                FROM dbo.KitsuneBackupSchedules
                WHERE IsEnabled = 1
                  AND (LastRunAt IS NULL
                       OR DATEDIFF(MINUTE, LastRunAt, SYSUTCDATETIME()) >= IntervalMinutes);";

            var due = new List<(int id, string name, string type)>();

            await using (var conn = new SqlConnection(_conn))
            {
                await conn.OpenAsync(ct);
                await using var r = await new SqlCommand(sql, conn).ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    due.Add((Convert.ToInt32(r["Id"]), r["ObjectName"].ToString()!, r["ObjectType"].ToString()!));
            }

            foreach (var (id, name, type) in due)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    using var scope    = _sp.CreateScope();
                    var backupSvc      = scope.ServiceProvider.GetRequiredService<IBackupVersioningService>();
                    var result         = await backupSvc.BackupAsync(new BackupRequest { ObjectName = name, ObjectType = type });
                    var status         = result.Success ? $"OK v{result.VersionNumber}" : "FAIL";

                    await UpdateLastRunAsync(id, status);
                    _log.LogInformation("Scheduled backup: {Name} → {Status}", name, status);
                }
                catch (Exception ex)
                {
                    await UpdateLastRunAsync(id, "ERROR");
                    _log.LogError(ex, "Scheduled backup failed for {Name}", name);
                }
            }
        }

        private async Task UpdateLastRunAsync(int id, string status)
        {
            const string sql = @"
                UPDATE dbo.KitsuneBackupSchedules
                SET LastRunAt=SYSUTCDATETIME(), LastStatus=@Status
                WHERE Id=@Id;";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ── BackgroundService host ────────────────────────────────
    public class BackupSchedulerWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<BackupSchedulerWorker> _log;

        public BackupSchedulerWorker(IServiceProvider sp, ILogger<BackupSchedulerWorker> log)
        { _sp = sp; _log = log; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("Backup scheduler started");
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
                    _log.LogError(ex, "Scheduler tick error");
                }
                // Check every 5 minutes
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
