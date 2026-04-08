// ============================================================
// KITSUNE – SQL Script Runner Service
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

        private static readonly Regex _goBatch = new(
            @"^\s*GO(?:\s+(?<count>\d+))?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        public SqlScriptRunnerService(IConfiguration cfg, ILogger<SqlScriptRunnerService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        public async Task<ScriptRunResult> RunAsync(ScriptRunRequest request)
        {
            var result  = new ScriptRunResult();
            var batches = SplitBatches(request.Script);
            var cs      = string.IsNullOrEmpty(request.Database) ? _conn : AppendDatabase(_conn, request.Database);
            var sw      = Stopwatch.StartNew();

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();

            if (request.ParseOnly)
                return await ParseOnlyAsync(request.Script);

            SqlTransaction? tran = null;
            if (request.WrapInTransaction)
                tran = conn.BeginTransaction();

            try
            {
                foreach (var batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    var batchSw = Stopwatch.StartNew();
                    try
                    {
                        await using var cmd = tran is not null
                            ? new SqlCommand(batch, conn, tran) { CommandTimeout = 120 }
                            : new SqlCommand(batch, conn) { CommandTimeout = 120 };
                        int rows = await cmd.ExecuteNonQueryAsync();
                        batchSw.Stop();
                        result.Batches.Add(new BatchResult
                        {
                            BatchIndex  = result.Batches.Count,
                            Status      = "OK",
                            RowsAffected= rows,
                            ExecutionMs = batchSw.Elapsed.TotalMilliseconds,
                            Preview     = batch.Length > 80 ? batch[..80] + "…" : batch,
                        });
                    }
                    catch (SqlException ex)
                    {
                        batchSw.Stop();
                        result.Batches.Add(new BatchResult
                        {
                            BatchIndex = result.Batches.Count,
                            Status     = "ERROR",
                            Error      = ex.Message,
                            ExecutionMs= batchSw.Elapsed.TotalMilliseconds,
                            Preview    = batch.Length > 80 ? batch[..80] + "…" : batch,
                        });
                        if (request.StopOnError)
                        {
                            if (tran is not null) await tran.RollbackAsync();
                            sw.Stop();
                            result.Success     = false;
                            result.ExecutionMs = sw.Elapsed.TotalMilliseconds;
                            return result;
                        }
                    }
                }

                if (tran is not null) await tran.CommitAsync();
                sw.Stop();
                result.Success     = result.Batches.TrueForAll(b => b.Status == "OK");
                result.ExecutionMs = sw.Elapsed.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                if (tran is not null) try { await tran.RollbackAsync(); } catch { }
                _log.LogError(ex, "Script runner fatal error");
                sw.Stop();
                result.Success     = false;
                result.ExecutionMs = sw.Elapsed.TotalMilliseconds;
            }
            return result;
        }

        public async Task<ScriptRunResult> ParseOnlyAsync(string script)
        {
            var result = new ScriptRunResult();
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await new SqlCommand("SET PARSEONLY ON;", conn).ExecuteNonQueryAsync();
            try
            {
                await new SqlCommand(script, conn).ExecuteNonQueryAsync();
                result.Success = true;
            }
            catch (SqlException ex)
            {
                result.Success = false;
                result.Batches.Add(new BatchResult
                {
                    Status  = "ERROR",
                    Error   = ex.Message,
                    Preview = script.Length > 80 ? script[..80] + "…" : script,
                });
            }
            finally
            {
                try { await new SqlCommand("SET PARSEONLY OFF;", conn).ExecuteNonQueryAsync(); } catch { }
            }
            return result;
        }

        public List<string> SplitBatches(string script)
        {
            var batches = new List<string>();
            var parts   = _goBatch.Split(script);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    batches.Add(trimmed);
            }
            return batches;
        }

        private static string AppendDatabase(string connStr, string dbName)
        {
            if (connStr.Contains("Database=", StringComparison.OrdinalIgnoreCase))
                return Regex.Replace(connStr, @"Database=[^;]+", $"Database={dbName}", RegexOptions.IgnoreCase);
            return connStr + $";Database={dbName}";
        }
    }
}
