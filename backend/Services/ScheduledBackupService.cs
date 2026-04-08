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
        public async Task EnsureTableAsync()
        public async Task<int> AddScheduleAsync(ScheduleRequest req)
        public async Task<List<BackupSchedule>> ListSchedulesAsync()
        public async Task<bool> ToggleScheduleAsync(int id, bool enabled)
        public async Task<bool> DeleteScheduleAsync(int id)
        public async Task RunDueBackupsAsync(CancellationToken ct)
        private async Task UpdateLastRunAsync(int id, string status)
    }

    // ── BackgroundService host ────────────────────────────────
    public class BackupSchedulerWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<BackupSchedulerWorker> _log;

        public BackupSchedulerWorker(IServiceProvider sp, ILogger<BackupSchedulerWorker> log)
        { _sp = sp; _log = log; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    }
}
