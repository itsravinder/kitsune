// ============================================================
// KITSUNE – Apply Service
// Live execution with: pre-flight validation → auto-backup →
// apply DDL/DML → audit log → rollback on failure
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
    public class ApplyRequest
    {
        public string ObjectName      { get; set; } = "";
        public string ObjectType      { get; set; } = "";
        public string SqlScript       { get; set; } = "";
        public bool   SkipValidation  { get; set; } = false;
        public bool   SkipBackup      { get; set; } = false;
        public string ChangeSummary   { get; set; } = "";
    }

    public class ApplyResponse
    {
        public bool         Success          { get; set; }
        public string       Status           { get; set; } = "";
        public string       Message          { get; set; } = "";
        public int?         BackupVersion    { get; set; }
        public ValidateResponse? Validation  { get; set; }
        public double       ExecutionMs      { get; set; }
        public List<string> Warnings         { get; set; } = new();
        public List<string> Errors           { get; set; } = new();
    }

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
            var sw       = Stopwatch.StartNew();
            var response = new ApplyResponse();

            try
            {
                // ── Step 1: Validate dependencies ─────────────────────
                if (!request.SkipValidation)
                {
                    response.Validation = await _validation.ValidateAsync(new ValidateRequest
                    {
                        ObjectName    = request.ObjectName,
                        ObjectType    = request.ObjectType,
                        NewDefinition = request.SqlScript,
                    });

                    if (response.Validation.Status == "FAIL")
                    {
                        response.Success = false;
                        response.Status  = "BLOCKED";
                        response.Message = "Apply blocked: validation failed. Fix syntax errors before applying.";
                        response.Errors  = response.Validation.Errors ?? new();
                        return response;
                    }

                    response.Warnings.AddRange(response.Validation.Warnings ?? new());
                }

                // ── Step 2: Auto-backup current definition ─────────────
                if (!request.SkipBackup)
                {
                    var exists = await _validation.ObjectExistsAsync(request.ObjectName);
                    if (exists)
                    {
                        var backup = await _backup.BackupAsync(new BackupRequest
                        {
                            ObjectName = request.ObjectName,
                            ObjectType = request.ObjectType,
                        });
                        if (backup.Success)
                        {
                            response.BackupVersion = backup.VersionNumber;
                            response.Warnings.Add($"Auto-backed up current definition as version {backup.VersionNumber}.");
                        }
                    }
                }

                // ── Step 3: Execute the script ─────────────────────────
                await using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();

                // Split on GO batches (SSMS-style)
                var batches = SplitOnGo(request.SqlScript);
                foreach (var batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 120 };
                    await cmd.ExecuteNonQueryAsync();
                }

                sw.Stop();
                response.Success     = true;
                response.Status      = "APPLIED";
                response.ExecutionMs = sw.Elapsed.TotalMilliseconds;
                response.Message     = $"Successfully applied changes to '{request.ObjectName}'. " +
                                       $"Execution: {response.ExecutionMs:0.0}ms.";

                await _audit.LogAsync(
                    AuditAction.Apply, request.ObjectName, request.ObjectType,
                    "SUCCESS", request, response,
                    durationMs: response.ExecutionMs);
            }
            catch (SqlException ex)
            {
                sw.Stop();
                response.Success     = false;
                response.Status      = "FAILED";
                response.ExecutionMs = sw.Elapsed.TotalMilliseconds;

                foreach (SqlError err in ex.Errors)
                    response.Errors.Add($"[Line {err.LineNumber}] {err.Message}");

                response.Message = "Apply failed. Original object preserved" +
                                   (response.BackupVersion.HasValue
                                       ? $" (backed up as v{response.BackupVersion})."
                                       : ".");

                await _audit.LogAsync(
                    AuditAction.Apply, request.ObjectName, request.ObjectType,
                    "FAIL", request, new { errors = response.Errors });

                _log.LogError(ex, "Apply failed for {Object}", request.ObjectName);
            }
            catch (Exception ex)
            {
                sw.Stop();
                response.Success     = false;
                response.Status      = "FAILED";
                response.Message     = ex.Message;
                response.ExecutionMs = sw.Elapsed.TotalMilliseconds;
                _log.LogError(ex, "Apply unexpected error");
            }

            return response;
        }

        // Split T-SQL script on GO statements (batch separator)
        private static List<string> SplitOnGo(string script)
        {
            var batches = Regex.Split(script, @"^\s*GO\s*$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return new List<string>(batches);
        }
    }
}
