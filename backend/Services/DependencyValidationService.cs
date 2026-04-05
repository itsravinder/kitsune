// ============================================================
// KITSUNE – Dependency Validation Service
// ============================================================
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface IDependencyValidationService
    {
        Task<ValidateResponse> ValidateAsync(ValidateRequest request);
        Task<List<AffectedObject>> GetDependencyTreeAsync(string objectName);
        Task<List<ParameterInfo>> GetParametersAsync(string objectName);
        Task<bool> ObjectExistsAsync(string objectName);
    }

    public class DependencyValidationService : IDependencyValidationService
    {
        private readonly string _connectionString;
        private readonly ILogger<DependencyValidationService> _logger;

        public DependencyValidationService(
            IConfiguration config,
            ILogger<DependencyValidationService> logger)
        {
            _connectionString = config.GetConnectionString("SqlServer")
                ?? throw new InvalidOperationException("SqlServer connection string missing.");
            _logger = logger;
        }

        // ── Main Validation Entry Point ───────────────────────
        public async Task<ValidateResponse> ValidateAsync(ValidateRequest request)
        {
            var response = new ValidateResponse { Status = "PASS" };

            try
            {
                // 1. Check object exists
                bool exists = await ObjectExistsAsync(request.ObjectName);
                if (!exists)
                {
                    response.Status  = "WARN";
                    response.Message = $"Object '{request.ObjectName}' does not exist yet – this will be a CREATE operation.";
                    return response;
                }

                // 2. Get full dependency tree
                var affected = await GetDependencyTreeAsync(request.ObjectName);
                response.AffectedObjects = affected;

                // 3. Get parameter changes (for procedures/functions)
                if (request.ObjectType is "PROCEDURE" or "FUNCTION" && !string.IsNullOrWhiteSpace(request.NewDefinition))
                {
                    var currentParams = await GetParametersAsync(request.ObjectName);
                    var paramWarnings = AnalyzeParameterChanges(currentParams, request.NewDefinition);
                    response.Warnings.AddRange(paramWarnings);
                }

                // 4. Syntax-check the new definition (attempt parse via SET PARSEONLY)
                if (!string.IsNullOrWhiteSpace(request.NewDefinition))
                {
                    var syntaxErrors = await SyntaxCheckAsync(request.NewDefinition);
                    if (syntaxErrors.Count > 0)
                    {
                        response.Status = "FAIL";
                        response.Errors = syntaxErrors;
                        response.Message = $"Syntax errors detected in new definition. {affected.Count} dependent object(s) also at risk.";
                        return response;
                    }
                }

                // 5. Build result
                if (affected.Count > 0)
                {
                    response.Status  = "WARN";
                    response.Message = $"Validation passed with warnings. {affected.Count} dependent object(s) will be affected by this change.";
                }
                else
                {
                    response.Status  = "PASS";
                    response.Message = "No dependent objects found. Safe to apply.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dependency validation failed for {Object}", request.ObjectName);
                response.Status  = "FAIL";
                response.Message = $"Validation engine error: {ex.Message}";
            }

            return response;
        }

        // ── Recursive Dependency Tree ─────────────────────────
        public async Task<List<AffectedObject>> GetDependencyTreeAsync(string objectName)
        {
            const string sql = @"
                WITH DependencyTree AS (
                    SELECT
                        sed.referencing_id                          AS ObjectId,
                        OBJECT_NAME(sed.referencing_id)             AS ObjectName,
                        o.type_desc                                 AS ObjectType,
                        OBJECT_SCHEMA_NAME(sed.referencing_id)      AS SchemaName,
                        1                                           AS Depth,
                        CAST(OBJECT_NAME(sed.referencing_id) AS NVARCHAR(MAX)) AS Path
                    FROM sys.sql_expression_dependencies sed
                    INNER JOIN sys.objects o ON o.object_id = sed.referencing_id
                    WHERE sed.referenced_id = OBJECT_ID(@ObjectName)
                      AND sed.referencing_id IS NOT NULL

                    UNION ALL

                    SELECT
                        sed2.referencing_id,
                        OBJECT_NAME(sed2.referencing_id),
                        o2.type_desc,
                        OBJECT_SCHEMA_NAME(sed2.referencing_id),
                        dt.Depth + 1,
                        dt.Path + N' → ' + OBJECT_NAME(sed2.referencing_id)
                    FROM sys.sql_expression_dependencies sed2
                    INNER JOIN sys.objects o2 ON o2.object_id = sed2.referencing_id
                    INNER JOIN DependencyTree dt ON dt.ObjectId = sed2.referenced_id
                    WHERE sed2.referencing_id IS NOT NULL
                      AND dt.Depth < 10
                )
                SELECT DISTINCT
                    ObjectId,
                    ObjectName,
                    ObjectType,
                    SchemaName,
                    Depth,
                    Path
                FROM DependencyTree
                ORDER BY Depth, ObjectName;";

            var results = new List<AffectedObject>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ObjectName", objectName);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new AffectedObject
                {
                    AffectedName     = reader["ObjectName"]?.ToString() ?? "",
                    AffectedType     = reader["ObjectType"]?.ToString() ?? "",
                    SchemaName       = reader["SchemaName"]?.ToString() ?? "",
                    Depth            = Convert.ToInt32(reader["Depth"]),
                    DependencyPath   = reader["Path"]?.ToString() ?? "",
                });
            }

            return results;
        }

        // ── Parameter Extraction ──────────────────────────────
        public async Task<List<ParameterInfo>> GetParametersAsync(string objectName)
        {
            const string sql = @"
                SELECT
                    p.name          AS ParameterName,
                    t.name          AS DataType,
                    p.max_length,
                    p.is_output,
                    p.has_default_value
                FROM sys.parameters p
                INNER JOIN sys.types t ON t.user_type_id = p.user_type_id
                WHERE p.object_id = OBJECT_ID(@ObjectName)
                ORDER BY p.parameter_id;";

            var results = new List<ParameterInfo>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ObjectName", objectName);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new ParameterInfo
                {
                    ParameterName   = reader["ParameterName"]?.ToString() ?? "",
                    DataType        = reader["DataType"]?.ToString() ?? "",
                    MaxLength       = Convert.ToInt32(reader["max_length"]),
                    IsOutput        = Convert.ToBoolean(reader["is_output"]),
                    HasDefaultValue = Convert.ToBoolean(reader["has_default_value"]),
                });
            }

            return results;
        }

        // ── Object Existence Check ────────────────────────────
        public async Task<bool> ObjectExistsAsync(string objectName)
        {
            const string sql = "SELECT COUNT(1) FROM sys.objects WHERE object_id = OBJECT_ID(@ObjectName);";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ObjectName", objectName);

            var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            return count > 0;
        }

        // ── SET PARSEONLY Syntax Check ────────────────────────
        private async Task<List<string>> SyntaxCheckAsync(string definition)
        {
            var errors = new List<string>();

            await using var conn = new SqlConnection(_connectionString);
            conn.InfoMessage += (_, e) =>
            {
                foreach (SqlError err in e.Errors)
                    if (err.Class > 0) errors.Add($"[Line {err.LineNumber}] {err.Message}");
            };

            await conn.OpenAsync();

            try
            {
                await using var setCmd = new SqlCommand("SET PARSEONLY ON;", conn);
                await setCmd.ExecuteNonQueryAsync();

                await using var parseCmd = new SqlCommand(definition, conn);
                await parseCmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
            {
                foreach (SqlError err in ex.Errors)
                    errors.Add($"[Line {err.LineNumber}] {err.Message}");
            }
            finally
            {
                try
                {
                    await using var resetCmd = new SqlCommand("SET PARSEONLY OFF;", conn);
                    await resetCmd.ExecuteNonQueryAsync();
                }
                catch { /* best-effort */ }
            }

            return errors;
        }

        // ── Parameter Signature Change Analysis ───────────────
        private List<string> AnalyzeParameterChanges(
            List<ParameterInfo> currentParams,
            string newDefinition)
        {
            var warnings = new List<string>();

            foreach (var param in currentParams)
            {
                // Heuristic: check if existing parameter names still appear in the new definition
                if (!newDefinition.Contains(param.ParameterName, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(
                        $"Parameter '{param.ParameterName}' ({param.DataType}) appears to have been removed or renamed. " +
                        "All callers of this object must be updated.");
                }
            }

            return warnings;
        }
    }
}
