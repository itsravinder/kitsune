// ============================================================
// KITSUNE – SQL Script Runner Service
// Executes multi-statement SQL scripts with GO batch separators
// Supports: live run, dry-run (PARSEONLY), transaction wrap
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
    {
        public string SqlScript      { get; set; } = "";
        public bool   DryRun         { get; set; } = false;   // PARSEONLY – syntax check only
        public bool   UseTransaction { get; set; } = false;   // wrap all batches in one transaction
        public int    TimeoutSeconds { get; set; } = 120;
        public string? DatabaseName  { get; set; }
    }

    {
        public int     BatchNumber  { get; set; }
        public string  BatchSql     { get; set; } = "";
        public bool    Success      { get; set; }
        public int     RowsAffected { get; set; }
        public double  ExecutionMs  { get; set; }
        public string? Error        { get; set; }
    }

    {
        public bool              Success        { get; set; }
        public int               TotalBatches   { get; set; }
        public int               SuccessCount   { get; set; }
        public int               FailureCount   { get; set; }
        public double            TotalMs        { get; set; }
        public List<BatchResult> Batches        { get; set; } = new();
        public List<string>      Messages       { get; set; } = new();
        public string            Mode           { get; set; } = "LIVE";
    }

    public interface ISqlScriptRunnerService
    {
        Task<ScriptRunResult> RunAsync(ScriptRunRequest request);
        Task<ScriptRunResult> ParseOnlyAsync(string script);
        List<string>          SplitBatches(string script);
    }

    public class SqlScriptRunnerService : ISqlScriptRunnerService
    {
        private readonly string _conn;
        private readonly ILogger<SqlScriptRunnerService> _log;

        // Matches GO on its own line (optional count, e.g. GO 3)
        private static readonly Regex _goBatch = new(
            @"^\s*GO(?:\s+(?<count>\d+))?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        public SqlScriptRunnerService(IConfiguration cfg, ILogger<SqlScriptRunnerService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        // ── Main entry point ──────────────────────────────────
        public async Task<ScriptRunResult> RunAsync(ScriptRunRequest request)
        {
            var overall = Stopwatch.StartNew();
            var result  = new ScriptRunResult
            {
                Mode = request.DryRun ? "PARSEONLY" : request.UseTransaction ? "TRANSACTION" : "LIVE"
            };

            if (request.DryRun)
                return await ParseOnlyAsync(request.SqlScript);

            var batches = SplitBatches(request.SqlScript);
            result.TotalBatches = batches.Count;

            var connStr = string.IsNullOrEmpty(request.DatabaseName)
                ? _conn
                : AppendDatabase(_conn, request.DatabaseName);

            await using var conn = new SqlConnection(connStr);

            conn.InfoMessage += (_, e) =>
            {
                foreach (SqlError msg in e.Errors)
                    result.Messages.Add($"[{(msg.Class == 0 ? "INFO" : "WARN")} Batch-{result.Batches.Count + 1}] {msg.Message}");
            };

            await conn.OpenAsync();

            SqlTransaction? tran = null;
            if (request.UseTransaction)
                tran = conn.BeginTransaction();

            try
            {
                for (int i = 0; i < batches.Count; i++)
                {
                    var batchSql = batches[i].Trim();
                    if (string.IsNullOrWhiteSpace(batchSql)) continue;

                    var br  = new BatchResult { BatchNumber = i + 1, BatchSql = batchSql.Length > 200 ? batchSql[..200] + "…" : batchSql };
                    var bsw = Stopwatch.StartNew();

                    try
                    {
                        await using var cmd = new SqlCommand(batchSql, conn, tran)
                        {
                            CommandTimeout = request.TimeoutSeconds
                        };
                        br.RowsAffected = await cmd.ExecuteNonQueryAsync();
                        bsw.Stop();
                        br.Success     = true;
                        br.ExecutionMs = bsw.Elapsed.TotalMilliseconds;
                        result.SuccessCount++;
                    }
                    catch (SqlException ex)
                    {
                        bsw.Stop();
                        br.Success     = false;
                        br.ExecutionMs = bsw.Elapsed.TotalMilliseconds;
                        br.Error       = $"[Line {ex.LineNumber}] {ex.Message}";
                        result.FailureCount++;
                        result.Messages.Add($"Batch {i + 1} FAILED: {br.Error}");

                        if (request.UseTransaction)
                        {
                            tran?.Rollback();
                            result.Messages.Add("Transaction rolled back due to error.");
                            result.Batches.Add(br);
                            break;
                        }
                    }

                    result.Batches.Add(br);
                }

                if (request.UseTransaction && result.FailureCount == 0)
                {
                    tran?.Commit();
                    result.Messages.Add("Transaction committed successfully.");
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Script runner unhandled error");
                tran?.Rollback();
                result.Messages.Add($"Fatal error: {ex.Message}");
            }

            overall.Stop();
            result.TotalMs  = overall.Elapsed.TotalMilliseconds;
            result.Success  = result.FailureCount == 0;
            return result;
        }

        // ── PARSEONLY – syntax check, no execution ─────────────
        public async Task<ScriptRunResult> ParseOnlyAsync(string script)
        {
            var result  = new ScriptRunResult { Mode = "PARSEONLY" };
            var batches = SplitBatches(script);
            result.TotalBatches = batches.Count;

            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await new SqlCommand("SET PARSEONLY ON;", conn).ExecuteNonQueryAsync();

            for (int i = 0; i < batches.Count; i++)
            {
                var batchSql = batches[i].Trim();
                if (string.IsNullOrWhiteSpace(batchSql)) continue;

                var br = new BatchResult { BatchNumber = i + 1, BatchSql = batchSql.Length > 200 ? batchSql[..200] + "…" : batchSql };
                var sw = Stopwatch.StartNew();
                try
                {
                    await using var cmd = new SqlCommand(batchSql, conn) { CommandTimeout = 30 };
                    await cmd.ExecuteNonQueryAsync();
                    br.Success = true;
                    result.SuccessCount++;
                }
                catch (SqlException ex)
                {
                    br.Success = false;
                    br.Error   = $"[Line {ex.LineNumber}] {ex.Message}";
                    result.FailureCount++;
                }
                finally
                {
                    sw.Stop();
                    br.ExecutionMs = sw.Elapsed.TotalMilliseconds;
                    result.Batches.Add(br);
                }
            }

            await new SqlCommand("SET PARSEONLY OFF;", conn).ExecuteNonQueryAsync();
            result.Success = result.FailureCount == 0;
            return result;
        }

        // ── Split script on GO ────────────────────────────────
        public List<string> SplitBatches(string script)
        {
            var results = new List<string>();
            var matches = _goBatch.Matches(script);
            int lastEnd = 0;

            foreach (Match m in matches)
            {
                var batch = script[lastEnd..m.Index];
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    // Handle GO <count>
                    int count = 1;
                    if (m.Groups["count"].Success)
                        int.TryParse(m.Groups["count"].Value, out count);
                    for (int i = 0; i < count; i++)
                        results.Add(batch);
                }
                lastEnd = m.Index + m.Length;
            }

            // Remainder after last GO
            if (lastEnd < script.Length)
            {
                var tail = script[lastEnd..];
                if (!string.IsNullOrWhiteSpace(tail))
                    results.Add(tail);
            }

            return results;
        }

        private static string AppendDatabase(string connStr, string dbName)
        {
            var builder = new SqlConnectionStringBuilder(connStr) { InitialCatalog = dbName };
            return builder.ToString();
        }
    }
}
