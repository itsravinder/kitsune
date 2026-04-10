// ============================================================
// KITSUNE – Preview Execution Service  (fixed)
// Fixes:
//   1. SET NOCOUNT ON  — eliminates row-count pseudo-result-sets
//   2. USE [database]  — forces correct DB context
//   3. Full logging    — DB name, server, query, row count
//   4. Clean DataReader loop — columns from first real result set
// ============================================================
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface IPreviewExecutionService
    {
        Task<PreviewResponse> PreviewAsync(PreviewRequest request);
    }

    public class PreviewExecutionService : IPreviewExecutionService
    {
        private readonly string _connectionString;
        private readonly ILogger<PreviewExecutionService> _logger;

        private static readonly Regex DangerousPatterns = new(
            @"\b(DROP\s+TABLE|DROP\s+DATABASE|TRUNCATE|DROP\s+PROCEDURE|DROP\s+FUNCTION|DROP\s+VIEW)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public PreviewExecutionService(
            IConfiguration config,
            ILogger<PreviewExecutionService> logger)
        {
            _connectionString = config.GetConnectionString("SqlServer")
                ?? throw new InvalidOperationException("SqlServer connection string missing.");
            _logger = logger;
        }

        public async Task<PreviewResponse> PreviewAsync(PreviewRequest request)
        {
            var response = new PreviewResponse { Mode = "SAFE_PREVIEW" };

            // ── Guard ─────────────────────────────────────────────────
            if (DangerousPatterns.IsMatch(request.SqlQuery))
            {
                response.Success = false;
                response.Errors.Add("SAFE MODE: Destructive DDL (DROP TABLE, TRUNCATE etc.) blocked.");
                return response;
            }

            // ── Log incoming query ────────────────────────────────────
            var snippet = request.SqlQuery.Length > 120
                ? request.SqlQuery[..120] + "…"
                : request.SqlQuery;
            _logger.LogInformation("[PREVIEW] Incoming query: {Query}", snippet);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await using var conn = new SqlConnection(_connectionString);

                // Capture SQL Server info messages (PRINT, row counts, warnings)
                conn.InfoMessage += (_, e) =>
                {
                    foreach (SqlError msg in e.Errors)
                        response.Messages.Add($"[{(msg.Class == 0 ? "INFO" : "WARN")}] {msg.Message}");
                };

                await conn.OpenAsync();

                // ── Log which DB and server we actually connected to ──
                string actualDb     = conn.Database;
                string actualServer = conn.DataSource;
                _logger.LogInformation("[PREVIEW] Connected to server={Server} database={Database}",
                    actualServer, actualDb);
                response.Messages.Add($"[DEBUG] Server: {actualServer}  Database: {actualDb}");

                // ── Force correct DB (guards against appsettings mismatch) ──
                // Extract database name from connection string
                string? targetDb = ExtractDatabaseFromConnStr(_connectionString);
                if (!string.IsNullOrEmpty(targetDb)
                    && !actualDb.Equals(targetDb, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("[PREVIEW] DB mismatch! Connected to '{Actual}', expected '{Target}'. Switching.",
                        actualDb, targetDb);
                    conn.ChangeDatabase(targetDb);
                    actualDb = conn.Database;
                    _logger.LogInformation("[PREVIEW] Switched to database={Database}", actualDb);
                }

                // ── Build the safe wrapped script ─────────────────────
                string safeScript = BuildSafeScript(request, actualDb);
                _logger.LogDebug("[PREVIEW] Executing script:\n{Script}", safeScript);

                await using var cmd = new SqlCommand(safeScript, conn)
                {
                    CommandTimeout = Math.Min(request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30, 120)
                };

                if (request.Parameters is not null)
                {
                    foreach (var (key, value) in request.Parameters)
                        cmd.Parameters.AddWithValue(
                            key.StartsWith("@") ? key : "@" + key,
                            value ?? DBNull.Value);
                }

                // ── Execute and read ──────────────────────────────────
                // CommandBehavior.Default: all result sets returned
                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default);

                bool capturedColumns = false;
                int  resultSetIndex  = 0;

                do
                {
                    resultSetIndex++;

                    // Only care about result sets that actually have columns (real SELECT output)
                    // Skip "rows affected" pseudo result sets (FieldCount == 0)
                    if (reader.FieldCount == 0)
                    {
                        _logger.LogDebug("[PREVIEW] Result set {N} has 0 fields — skipping (row-count set)", resultSetIndex);
                        continue;
                    }

                    // Capture columns from the first real result set
                    if (!capturedColumns)
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                            response.Columns.Add(reader.GetName(i));
                        capturedColumns = true;
                        _logger.LogDebug("[PREVIEW] Columns captured from result set {N}: [{Cols}]",
                            resultSetIndex, string.Join(", ", response.Columns));
                    }

                    // Read rows
                    int rowsThisSet = 0;
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var val = reader.GetValue(i);
                            row[reader.GetName(i)] = val is DBNull ? null : val;
                        }
                        response.ResultSet.Add(row);
                        rowsThisSet++;

                        if (response.ResultSet.Count >= 500)
                        {
                            response.Messages.Add("PREVIEW TRUNCATED: first 500 rows shown.");
                            break;
                        }
                    }
                    _logger.LogDebug("[PREVIEW] Result set {N}: {Rows} rows read", resultSetIndex, rowsThisSet);

                } while (await reader.NextResultAsync());

                stopwatch.Stop();
                response.RowCount    = response.ResultSet.Count;
                response.ExecutionMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Success     = true;

                _logger.LogInformation(
                    "[PREVIEW] Done. DB={DB} Rows={Rows} Cols={Cols} Time={Ms}ms",
                    actualDb, response.RowCount, response.Columns.Count,
                    stopwatch.Elapsed.TotalMilliseconds.ToString("F0"));
            }
            catch (SqlException ex)
            {
                stopwatch.Stop();
                response.ExecutionMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Success     = false;
                foreach (SqlError err in ex.Errors)
                    response.Errors.Add($"[Line {err.LineNumber}] SQL Error {err.Number}: {err.Message}");
                _logger.LogWarning(ex, "[PREVIEW] SQL error for: {Query}", snippet);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                response.ExecutionMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Success     = false;
                response.Errors.Add($"Execution error: {ex.Message}");
                _logger.LogError(ex, "[PREVIEW] Unexpected error");
            }

            return response;
        }

        // ── Wrap in BEGIN TRAN / ROLLBACK ─────────────────────────────
        private static string BuildSafeScript(PreviewRequest request, string dbName)
        {
            // USE [db] forces correct database context regardless of connection string
            string useDb = !string.IsNullOrEmpty(dbName) ? $"USE [{dbName}];\n" : "";

            if (request.IsStoredProc)
            {
                return $@"{useDb}SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
    EXEC {SanitizeName(request.SqlQuery)};
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrMsg  NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrLine INT            = ERROR_LINE();
    RAISERROR('[KITSUNE] SP Error at line %d: %s', 16, 1, @ErrLine, @ErrMsg);
END CATCH;";
            }
            else
            {
                // SET NOCOUNT ON: suppresses "N rows affected" messages
                // that appear as extra pseudo-result-sets with SET NOCOUNT OFF
                // This is the key fix for 0-row SELECT queries
                return $@"{useDb}SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY

{request.SqlQuery}

    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrMsg  NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrLine INT            = ERROR_LINE();
    RAISERROR('[KITSUNE] Error at line %d: %s', 16, 1, @ErrLine, @ErrMsg);
END CATCH;";
            }
        }

        private static string SanitizeName(string name) =>
            Regex.Replace(name, @"[^\w\.\[\]@]", "");

        // ── Extract Database= from connection string ──────────────────
        private static string? ExtractDatabaseFromConnStr(string cs)
        {
            // Handles both "Database=X" and "Initial Catalog=X"
            var m = Regex.Match(cs, @"(?:Database|Initial\s+Catalog)\s*=\s*([^;]+)",
                RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }
    }
}
