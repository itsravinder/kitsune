// ============================================================
// KITSUNE – Preview Execution Service  (v3 – clean DB context)
// DB context rules:
//   1. NEVER use USE [db] statement — it overrides conn.ChangeDatabase()
//      and hardcodes a DB name into the script text
//   2. DB is set ONCE via conn.ChangeDatabase(requestedDb)
//   3. requestedDb = request.DatabaseName (UI) or connection-string DB
//   4. If requestedDb is empty/invalid → use whatever the conn opened on
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

            // ── Resolve target DB ─────────────────────────────────────
            // Priority order:
            //   1. request.DatabaseName — set by UI selectedDatabase
            //   2. Database= in the appsettings connection string
            //   3. Leave connection on whatever it opened on (do not crash)
            string? requestedDb = string.IsNullOrWhiteSpace(request.DatabaseName)
                ? ExtractDatabaseFromConnStr(_connectionString)
                : request.DatabaseName.Trim();

            // Reject obviously wrong values (e.g. "SqlServer" fell through from UI bug)
            if (requestedDb != null && (
                requestedDb.Equals("SqlServer",  StringComparison.OrdinalIgnoreCase) ||
                requestedDb.Equals("MongoDB",    StringComparison.OrdinalIgnoreCase) ||
                requestedDb.Equals("MySQL",      StringComparison.OrdinalIgnoreCase) ||
                requestedDb.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("[PREVIEW] Ignoring invalid databaseName '{Db}' — using connection string default", requestedDb);
                requestedDb = ExtractDatabaseFromConnStr(_connectionString);
            }

            _logger.LogInformation("[PREVIEW] requestedDb={Db} | {Snippet}", requestedDb ?? "(default)", snippet);

            var sw = Stopwatch.StartNew();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                conn.InfoMessage += (_, e) =>
                {
                    foreach (SqlError msg in e.Errors)
                        response.Messages.Add($"[{(msg.Class == 0 ? "INFO" : "WARN")}] {msg.Message}");
                };

                await conn.OpenAsync();
                string connectedDb     = conn.Database;
                string connectedServer = conn.DataSource;

                // Switch DB via ADO.NET — this is the ONLY place DB is set
                // NO USE statement is injected into the SQL script
                if (!string.IsNullOrEmpty(requestedDb)
                    && !connectedDb.Equals(requestedDb, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("[PREVIEW] ChangeDatabase: {From} → {To}", connectedDb, requestedDb);
                    try
                    {
                        conn.ChangeDatabase(requestedDb);
                        connectedDb = conn.Database;
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogWarning("[PREVIEW] ChangeDatabase failed: {Err} — staying on {Db}", dbEx.Message, connectedDb);
                        response.Messages.Add($"[WARN] Could not switch to '{requestedDb}': {dbEx.Message}. Using '{connectedDb}'.");
                    }
                }

                _logger.LogInformation("[PREVIEW] Executing on {Server}/{Db}", connectedServer, connectedDb);
                response.Messages.Add($"[DB] {connectedServer} / {connectedDb}");

                // Validate only if a DB was explicitly requested
                if (!string.IsNullOrEmpty(requestedDb)
                    && !connectedDb.Equals(requestedDb, StringComparison.OrdinalIgnoreCase))
                {
                    response.Success = false;
                    response.Errors.Add($"Could not execute on '{requestedDb}' (connected to '{connectedDb}'). Check the database name.");
                    return response;
                }

                // Build script — NO USE statement, DB already set via ChangeDatabase
                string safeScript = BuildSafeScript(request.SqlQuery, request.IsStoredProc);
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
                int  setIdx       = 0;

                do
                {
                    setIdx++;
                    if (reader.FieldCount == 0)
                    {
                        _logger.LogDebug("[PREVIEW] Result set {N}: 0 fields — skip", setIdx);
                        continue;
                    }

                    if (!capturedCols)
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                            response.Columns.Add(reader.GetName(i));
                        capturedCols = true;
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
                    _logger.LogDebug("[PREVIEW] Set {N}: {Rows} rows", setIdx, rowsRead);

                } while (await reader.NextResultAsync());

                sw.Stop();
                response.RowCount    = response.ResultSet.Count;
                response.ExecutionMs = sw.Elapsed.TotalMilliseconds;
                response.Success     = true;

                _logger.LogInformation("[PREVIEW] Done | DB={Db} Rows={R} Cols={C} {Ms}ms",
                    connectedDb, response.RowCount, response.Columns.Count,
                    sw.Elapsed.TotalMilliseconds.ToString("F0"));
            }
            catch (SqlException ex)
            {
                sw.Stop();
                response.ExecutionMs = sw.Elapsed.TotalMilliseconds;
                response.Success     = false;
                foreach (SqlError err in ex.Errors)
                    response.Errors.Add($"[Line {err.LineNumber}] SQL {err.Number}: {err.Message}");
                _logger.LogWarning(ex, "[PREVIEW] SQL error | {Snippet}", snippet);
            }
            catch (Exception ex)
            {
                sw.Stop();
                response.ExecutionMs = sw.Elapsed.TotalMilliseconds;
                response.Success     = false;
                response.Errors.Add($"Error: {ex.Message}");
                _logger.LogError(ex, "[PREVIEW] Unexpected error");
            }

            return response;
        }

        // ── Script builder — NO USE statement ────────────────────────
        // The connection's DB is already set via ChangeDatabase() above.
        // Injecting USE [db] would:
        //   a) Hardcode a DB name into the script (breaks when user selects a different DB)
        //   b) Override the ChangeDatabase() call silently
        private static string BuildSafeScript(string sql, bool isStoredProc)
        {
            if (isStoredProc)
            {
                return $@"SET NOCOUNT ON;
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

            return $@"SET NOCOUNT ON;
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
