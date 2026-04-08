// ============================================================
// KITSUNE – Apply Service
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface IApplyService
    {
        Task<ApplyResponse> ApplyAsync(ApplyRequest request);
    }

    public class ApplyService : IApplyService
    {
        private readonly string _conn;
        private readonly IDependencyValidationService _validation;
        private readonly IBackupVersioningService     _backup;
        private readonly IAuditLogService             _audit;
        private readonly ILogger<ApplyService>        _log;

        public ApplyService(
            IConfiguration cfg,
            IDependencyValidationService validation,
            IBackupVersioningService backup,
            IAuditLogService audit,
            ILogger<ApplyService> log)
        {
            _conn       = cfg.GetConnectionString("SqlServer") ?? "";
            _validation = validation;
            _backup     = backup;
            _audit      = audit;
            _log        = log;
        }

        public async Task<ApplyResponse> ApplyAsync(ApplyRequest request)
        {
            var response  = new ApplyResponse { ObjectName = request.ObjectName };
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var backupResp = await _backup.BackupAsync(new BackupRequest
                {
                    ObjectName = request.ObjectName,
                    ObjectType = request.ObjectType,
                });

                var batches = SplitOnGo(request.SqlScript);
                await using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();

                foreach (var batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 120 };
                    await cmd.ExecuteNonQueryAsync();
                }

                stopwatch.Stop();
                response.Success       = true;
                response.Message       = $"Applied successfully. Auto-backed up as v{backupResp.VersionNumber}.";
                response.BackupVersion = backupResp.VersionNumber;

                await _audit.LogAsync(AuditAction.Apply, request.ObjectName, request.ObjectType,
                    "APPLIED", request, response, durationMs: stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _log.LogError(ex, "Apply failed for {Object}", request.ObjectName);
                response.Success = false;
                response.Message = $"Apply failed: {ex.Message}";
                await _audit.LogAsync(AuditAction.Apply, request.ObjectName, request.ObjectType,
                    "FAILED", request, new { error = ex.Message }, durationMs: stopwatch.Elapsed.TotalMilliseconds);
            }
            return response;
        }

        private static List<string> SplitOnGo(string script)
        {
            var batches = new List<string>();
            var parts   = Regex.Split(script, @"^\s*GO\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    batches.Add(trimmed);
            }
            return batches;
        }
    }
}
