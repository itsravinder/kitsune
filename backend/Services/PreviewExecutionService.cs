// ============================================================
// KITSUNE – Preview Execution Service  (v4 – connection string DB)
// DB context rules:
//   REMOVED: conn.ChangeDatabase() — fails silently on named instances
//   REMOVED: USE [db] — overrides connection-level DB
//   NEW:     Rebuild connection string with Database={requestedDb}
//            so the connection opens on the correct DB from the start
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
        private readonly string _baseConnectionString;
        private readonly ILogger<PreviewExecutionService> _logger;

        // These are database TYPE names, never actual DB names
        private static readonly HashSet<string> InvalidDbNames =
            new(StringComparer.OrdinalIgnoreCase)
            { "SqlServer", "MongoDB", "MySQL", "PostgreSQL", "tempdb", "master", "model", "msdb" };

        private static readonly Regex DangerousPatterns = new(
            @"\b(DROP\s+TABLE|DROP\s+DATABASE|TRUNCATE|DROP\s+PROCEDURE|DROP\s+FUNCTION|DROP\s+VIEW)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public PreviewExecutionService(IConfiguration config, ILogger<PreviewExecutionService> logger)
        {
            _baseConnectionString = config.GetConnectionString("SqlServer")
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

            // ── Resolve target DB ─────────────────────────────────────
            string? requestedDb = ResolveDatabase(request.DatabaseName);

            var snippet = request.SqlQuery.Length > 120
                ? request.SqlQuery[..120] + "…"
                : request.SqlQuery;

            _logger.LogInformation("[PREVIEW] requestedDb={Db} | {Snippet}", requestedDb ?? "(conn-string default)", snippet);

            // ── Build connection string with the correct Database= ────
            // This is the ONLY way to reliably set DB on named SQL instances.
            // conn.ChangeDatabase() can fail silently on named instances with
            // Windows Auth, leaving the connection on master or tempdb.
            string connectionString = BuildConnectionStringForDb(requestedDb);

            var sw = Stopwatch.StartNew();
            try
            {
                await using var conn = new SqlConnection(connectionString);
                conn.InfoMessage += (_, e) =>
                {
                    foreach (SqlError msg in e.Errors)
                        response.Messages.Add($"[{(msg.Class == 0 ? "INFO" : "WARN")}] {msg.Message}");
                };

                await conn.OpenAsync();

                string connectedDb     = conn.Database;
                string connectedServer = conn.DataSource;

                _logger.LogInformation("[PREVIEW] Connected: {Server}/{Db}", connectedServer, connectedDb);
                response.Messages.Add($"[DB] {connectedServer} / {connectedDb}");

                // Validate we are on the right DB
                if (!string.IsNullOrEmpty(requestedDb)
                    && !connectedDb.Equals(requestedDb, StringComparison.OrdinalIgnoreCase))
                {
                    response.Success = false;
                    response.Errors.Add(
                        $"DB mismatch: requested '{requestedDb}' but connected to '{connectedDb}'. " +
                        $"Ensure the database exists and the login has access.");
                    return response;
                }

                string safeScript = BuildSafeScript(request.SqlQuery, request.IsStoredProc);
                _logger.LogDebug("[PREVIEW] Script:\n{Script}", safeScript);

                await using var cmd = new SqlCommand(safeScript, conn)
                {
                    CommandTimeout = Math.Min(request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30, 120)
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
                    if (reader.FieldCount == 0) continue;

                    if (!capturedCols)
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                            response.Columns.Add(reader.GetName(i));
                        capturedCols = true;
                    }

                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var val = reader.GetValue(i);
                            row[reader.GetName(i)] = val is DBNull ? null : val;
                        }
                        response.ResultSet.Add(row);
                        if (response.ResultSet.Count >= 500)
                        {
                            response.Messages.Add("PREVIEW TRUNCATED: first 500 rows shown.");
                            break;
                        }
                    }
                } while (await reader.NextResultAsync());

                sw.Stop();
                response.RowCount    = response.ResultSet.Count;
                response.ExecutionMs = sw.Elapsed.TotalMilliseconds;
                response.Success     = true;

                _logger.LogInformation("[PREVIEW] Done | DB={Db} Rows={R} {Ms}ms",
                    connectedDb, response.RowCount, sw.Elapsed.TotalMilliseconds.ToString("F0"));
            }
            catch (SqlException ex)
            {
                sw.Stop();
                response.ExecutionMs = sw.Elapsed.TotalMilliseconds;
                response.Success     = false;
                foreach (SqlError err in ex.Errors)
                    response.Errors.Add($"[Line {err.LineNumber}] SQL {err.Number}: {err.Message}");
                _logger.LogWarning(ex, "[PREVIEW] SQL error");
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

        // ── Resolve the database name to use ─────────────────────────
        private string? ResolveDatabase(string? requestedDb)
        {
            if (string.IsNullOrWhiteSpace(requestedDb))
                return ExtractDatabaseFromConnStr(_baseConnectionString);

            string db = requestedDb.Trim();

            // Reject system/type names — these are never valid user database names
            if (InvalidDbNames.Contains(db))
            {
                _logger.LogWarning("[PREVIEW] Rejected invalid databaseName '{Db}'", db);
                return ExtractDatabaseFromConnStr(_baseConnectionString);
            }

            return db;
        }

        // ── Build connection string with the target database injected ─
        // Uses SqlConnectionStringBuilder to safely replace Database=
        // so the connection opens on the right DB without needing ChangeDatabase()
        private string BuildConnectionStringForDb(string? dbName)
        {
            if (string.IsNullOrEmpty(dbName))
                return _baseConnectionString;

            var csb = new SqlConnectionStringBuilder(_baseConnectionString)
            {
                InitialCatalog = dbName   // replaces Database= in connection string
            };
            return csb.ConnectionString;
        }

        // ── Safe script — NO USE statement, NO ChangeDatabase ────────
        private static string BuildSafeScript(string sql, bool isStoredProc)
        {
            if (isStoredProc)
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
            var m = Regex.Match(cs, @"(?:Database|Initial\s+Catalog)\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }
    }
}
