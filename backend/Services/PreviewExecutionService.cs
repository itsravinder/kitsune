// ============================================================
// KITSUNE – Preview Execution Service  (v2 – dynamic DB)
// Single source of truth: request.DatabaseName drives everything
// Falls back to connection string DB if not supplied
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

            if (DangerousPatterns.IsMatch(request.SqlQuery))
            {
                response.Success = false;
                response.Errors.Add("SAFE MODE: Destructive DDL blocked.");
                return response;
            }

            var snippet = request.SqlQuery.Length > 120
                ? request.SqlQuery[..120] + "…"
                : request.SqlQuery;

            // ── Resolve target database ───────────────────────────────
            // Priority: 1. request.DatabaseName (UI selection)
            //           2. Database= in connection string
            //           3. Whatever SQL Server defaults to (master)
            string? requestedDb = string.IsNullOrWhiteSpace(request.DatabaseName)
                ? ExtractDatabaseFromConnStr(_connectionString)
                : request.DatabaseName.Trim();

            _logger.LogInformation(
                "[PREVIEW] Query incoming | requestedDb={Db} | snippet={Snippet}",
                requestedDb ?? "(none)", snippet);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                conn.InfoMessage += (_, e) =>
                {
                    foreach (SqlError msg in e.Errors)
                        response.Messages.Add($"[{(msg.Class == 0 ? "INFO" : "WARN")}] {msg.Message}");
                };

                await conn.OpenAsync();

                string connectedDb = conn.Database;
                string connectedServer = conn.DataSource;

                // Switch to requested DB if different from what connection opened on
                if (!string.IsNullOrEmpty(requestedDb)
                    && !connectedDb.Equals(requestedDb, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "[PREVIEW] DB mismatch — connected={Connected}, requested={Requested}. Switching.",
                        connectedDb, requestedDb);
                    conn.ChangeDatabase(requestedDb);
                    connectedDb = conn.Database;
                }

                _logger.LogInformation(
                    "[PREVIEW] Executing on server={Server} db={Db}",
                    connectedServer, connectedDb);

                // Validate: confirm we are on the right DB before running
                if (!string.IsNullOrEmpty(requestedDb)
                    && !connectedDb.Equals(requestedDb, StringComparison.OrdinalIgnoreCase))
                {
                    response.Success = false;
                    response.Errors.Add(
                        $"DB validation failed: connected to '{connectedDb}' but requested '{requestedDb}'. " +
                        "Check connection string.");
                    return response;
                }

                // Expose to caller so UI can confirm which DB was used
                response.Messages.Add($"[DB] Server: {connectedServer}  Database: {connectedDb}");

                string safeScript = BuildSafeScript(request.SqlQuery, request.IsStoredProc, connectedDb);
                _logger.LogDebug("[PREVIEW] Script:\n{Script}", safeScript);

                await using var cmd = new SqlCommand(safeScript, conn)
                {
                    CommandTimeout = Math.Min(
                        request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30, 120)
                };

                if (request.Parameters is not null)
                    foreach (var (key, value) in request.Parameters)
                        cmd.Parameters.AddWithValue(
                            key.StartsWith("@") ? key : "@" + key,
                            value ?? DBNull.Value);

                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default);

                bool capturedCols = false;
                int  setIndex     = 0;

                do
                {
                    setIndex++;
                    if (reader.FieldCount == 0)
                    {
                        _logger.LogDebug("[PREVIEW] Result set {N}: 0 fields — skipping", setIndex);
                        continue;
                    }

                    if (!capturedCols)
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                            response.Columns.Add(reader.GetName(i));
                        capturedCols = true;
                        _logger.LogDebug("[PREVIEW] Columns: [{Cols}]",
                            string.Join(", ", response.Columns));
                    }

                    int rowsRead = 0;
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var val = reader.GetValue(i);
                            row[reader.GetName(i)] = val is DBNull ? null : val;
                        }
                        response.ResultSet.Add(row);
                        rowsRead++;
                        if (response.ResultSet.Count >= 500)
                        {
                            response.Messages.Add("PREVIEW TRUNCATED: first 500 rows shown.");
                            break;
                        }
                    }
                    _logger.LogDebug("[PREVIEW] Result set {N}: {Rows} rows", setIndex, rowsRead);

                } while (await reader.NextResultAsync());

                stopwatch.Stop();
                response.RowCount    = response.ResultSet.Count;
                response.ExecutionMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Success     = true;

                _logger.LogInformation(
                    "[PREVIEW] Done | DB={Db} Rows={Rows} Cols={Cols} {Ms}ms",
                    connectedDb, response.RowCount, response.Columns.Count,
                    stopwatch.Elapsed.TotalMilliseconds.ToString("F0"));
            }
            catch (SqlException ex)
            {
                stopwatch.Stop();
                response.ExecutionMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Success     = false;
                foreach (SqlError err in ex.Errors)
                    response.Errors.Add($"[Line {err.LineNumber}] SQL {err.Number}: {err.Message}");
                _logger.LogWarning(ex, "[PREVIEW] SQL error | {Snippet}", snippet);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                response.ExecutionMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Success     = false;
                response.Errors.Add($"Error: {ex.Message}");
                _logger.LogError(ex, "[PREVIEW] Unexpected error");
            }

            return response;
        }

        // ── Safe script builder ───────────────────────────────────────
        private static string BuildSafeScript(string sql, bool isStoredProc, string dbName)
        {
            // USE [db] is the single source of truth for DB context.
            // SET NOCOUNT ON suppresses row-count pseudo-result-sets.
            string useDb = !string.IsNullOrEmpty(dbName) ? $"USE [{dbName}];\n" : "";

            if (isStoredProc)
            {
                return $@"{useDb}SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
    EXEC {SanitizeName(sql)};
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @Msg  NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @Line INT            = ERROR_LINE();
    RAISERROR('[KITSUNE] SP error at line %d: %s', 16, 1, @Line, @Msg);
END CATCH;";
            }

            return $@"{useDb}SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY

{sql}

    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @Msg  NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @Line INT            = ERROR_LINE();
    RAISERROR('[KITSUNE] Error at line %d: %s', 16, 1, @Line, @Msg);
END CATCH;";
        }

        private static string SanitizeName(string name) =>
            Regex.Replace(name, @"[^\w\.\[\]@]", "");

        private static string? ExtractDatabaseFromConnStr(string cs)
        {
            var m = Regex.Match(cs,
                @"(?:Database|Initial\s+Catalog)\s*=\s*([^;]+)",
                RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }
    }
}
