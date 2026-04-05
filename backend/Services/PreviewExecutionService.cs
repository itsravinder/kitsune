// ============================================================
// KITSUNE – Preview Execution Service (SAFE MODE)
// Wraps every query in BEGIN TRAN / ROLLBACK – zero data change
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

        // DML that could modify data – we allow these inside rollback wrapper
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

            // ── Guard: block truly destructive DDL (DROP DATABASE etc.) ───
            if (DangerousPatterns.IsMatch(request.SqlQuery))
            {
                response.Success = false;
                response.Errors.Add("SAFE MODE: Destructive DDL statements (DROP DATABASE, TRUNCATE, etc.) " +
                                    "are blocked in preview mode. Use Apply with caution.");
                return response;
            }

            // ── Build the safe-mode wrapped script ─────────────────────────
            string safeScript = BuildSafeScript(request);

            await using var conn = new SqlConnection(_connectionString);

            // Capture informational messages from SQL Server (PRINT statements, row counts, etc.)
            conn.InfoMessage += (_, e) =>
            {
                foreach (SqlError msg in e.Errors)
                    response.Messages.Add($"[{(msg.Class == 0 ? "INFO" : "WARN")}] {msg.Message}");
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(safeScript, conn)
                {
                    CommandTimeout = Math.Min(request.TimeoutSeconds, 120)
                };

                // Add user-supplied parameters if provided
                if (request.Parameters is not null)
                {
                    foreach (var (key, value) in request.Parameters)
                        cmd.Parameters.AddWithValue(key.StartsWith("@") ? key : "@" + key, value ?? DBNull.Value);
                }

                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default);

                // ── Read result sets ────────────────────────────────────────
                bool firstSet = true;
                do
                {
                    if (firstSet)
                    {
                        // Capture column names from the first result set
                        for (int i = 0; i < reader.FieldCount; i++)
                            response.Columns.Add(reader.GetName(i));
                        firstSet = false;
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

                        // Cap preview at 500 rows to prevent UI overload
                        if (response.ResultSet.Count >= 500)
                        {
                            response.Messages.Add("PREVIEW TRUNCATED: Only first 500 rows are shown.");
                            break;
                        }
                    }
                } while (await reader.NextResultAsync());

                stopwatch.Stop();
                response.RowCount    = response.ResultSet.Count;
                response.ExecutionMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Success     = true;
            }
            catch (SqlException ex)
            {
                stopwatch.Stop();
                response.ExecutionMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Success     = false;

                foreach (SqlError err in ex.Errors)
                    response.Errors.Add($"[Line {err.LineNumber}] SQL Error {err.Number}: {err.Message}");

                _logger.LogWarning(ex, "Preview execution SQL error for query: {Snippet}",
                    request.SqlQuery.Length > 100 ? request.SqlQuery[..100] + "…" : request.SqlQuery);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                response.ExecutionMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Success     = false;
                response.Errors.Add($"Execution error: {ex.Message}");
                _logger.LogError(ex, "Preview execution unexpected error");
            }

            return response;
        }

        // ── Build BEGIN TRAN / ROLLBACK wrapper ───────────────────────────
        private static string BuildSafeScript(PreviewRequest request)
        {
            if (request.IsStoredProc)
            {
                // For stored procedures, execute the SP inside a transaction
                return $@"
SET NOCOUNT OFF;
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
    RAISERROR('[KITSUNE PREVIEW] SP Error at line %d: %s', 16, 1, @ErrLine, @ErrMsg);
END CATCH;";
            }
            else
            {
                // For raw SQL, wrap in rollback transaction
                return $@"
SET NOCOUNT OFF;
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
    RAISERROR('[KITSUNE PREVIEW] Error at line %d: %s', 16, 1, @ErrLine, @ErrMsg);
END CATCH;";
            }
        }

        // ── Basic name sanitizer (no parameterisation for identifiers) ───
        private static string SanitizeName(string name)
        {
            // Allow only alphanumeric, underscore, dot, square brackets
            return Regex.Replace(name, @"[^\w\.\[\]@]", "");
        }
    }
}
