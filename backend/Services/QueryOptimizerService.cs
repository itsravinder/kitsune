// ============================================================
// KITSUNE – Query Optimizer Service
// ============================================================
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface IQueryOptimizerService
    {
        Task<QueryPlanResult> AnalyzeAsync(OptimizeRequest request);
        Task<List<MissingIndexHint>> GetMissingIndexesFromDmvAsync();
        Task<string> GenerateIndexScriptAsync(MissingIndexHint hint);
    }

    public class QueryOptimizerService : IQueryOptimizerService
    {
        private readonly string _conn;
        private readonly ILogger<QueryOptimizerService> _log;

        public QueryOptimizerService(IConfiguration cfg, ILogger<QueryOptimizerService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        public async Task<QueryPlanResult> AnalyzeAsync(OptimizeRequest request)
        {
            var result = new QueryPlanResult();
            try
            {
                await using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();

                // Get estimated query plan XML
                await using var planCmd = new SqlCommand("SET SHOWPLAN_XML ON;", conn);
                await planCmd.ExecuteNonQueryAsync();

                await using var qCmd = new SqlCommand(request.SqlQuery, conn) { CommandTimeout = 30 };
                var planXml = string.Empty;
                await using var r = await qCmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) planXml = r[0].ToString() ?? "";

                await using var offCmd = new SqlCommand("SET SHOWPLAN_XML OFF;", conn);
                await offCmd.ExecuteNonQueryAsync();

                result.PlanXml       = planXml;
                result.EstimatedCost = ExtractCostFromPlan(planXml);
                result.Suggestions   = AnalyzeQueryHeuristics(request.SqlQuery);
                result.MissingIndexes = await GetMissingIndexesFromDmvAsync();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Query optimizer analysis failed");
                result.Suggestions   = AnalyzeQueryHeuristics(request.SqlQuery);
                result.MissingIndexes = new List<MissingIndexHint>();
            }
            return result;
        }

        public async Task<List<MissingIndexHint>> GetMissingIndexesFromDmvAsync()
        {
            const string sql = @"
                SELECT TOP 10
                    mid.object_id,
                    OBJECT_NAME(mid.object_id)           AS TableName,
                    OBJECT_SCHEMA_NAME(mid.object_id)    AS SchemaName,
                    mid.equality_columns,
                    mid.inequality_columns,
                    mid.included_columns,
                    migs.avg_total_user_cost * migs.avg_user_impact * (migs.user_seeks + migs.user_scans) AS ImpactFactor
                FROM sys.dm_db_missing_index_details   mid
                INNER JOIN sys.dm_db_missing_index_groups mig  ON mig.index_handle = mid.index_handle
                INNER JOIN sys.dm_db_missing_index_group_stats migs ON migs.group_handle = mig.index_group_handle
                WHERE mid.database_id = DB_ID()
                ORDER BY ImpactFactor DESC;";

            var hints = new List<MissingIndexHint>();
            try
            {
                await using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var r   = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    hints.Add(new MissingIndexHint
                    {
                        TableName         = r["TableName"].ToString()!,
                        SchemaName        = r["SchemaName"].ToString()!,
                        EqualityColumns   = r["equality_columns"]?.ToString(),
                        InequalityColumns = r["inequality_columns"]?.ToString(),
                        IncludedColumns   = r["included_columns"]?.ToString(),
                        ImpactFactor      = Convert.ToDouble(r["ImpactFactor"]),
                    });
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "DMV query failed");
            }
            return hints;
        }

        public Task<string> GenerateIndexScriptAsync(MissingIndexHint hint)
        {
            var cols    = new List<string>();
            var include = new List<string>();

            if (!string.IsNullOrEmpty(hint.EqualityColumns))   cols.AddRange(hint.EqualityColumns.Split(','));
            if (!string.IsNullOrEmpty(hint.InequalityColumns)) cols.AddRange(hint.InequalityColumns.Split(','));
            if (!string.IsNullOrEmpty(hint.IncludedColumns))   include.AddRange(hint.IncludedColumns.Split(','));

            var colList = string.Join(", ", cols).Trim();
            var inclStr = include.Count > 0 ? $"\nINCLUDE ({string.Join(", ", include).Trim()})" : "";
            var idxName = $"IX_{hint.TableName}_{Regex.Replace(colList, @"[^a-zA-Z0-9]", "_")}";

            var script = $@"CREATE NONCLUSTERED INDEX [{idxName}]
ON [{hint.SchemaName}].[{hint.TableName}] ({colList}){inclStr};";

            return Task.FromResult(script);
        }

        private static double ExtractCostFromPlan(string planXml)
        {
            if (string.IsNullOrEmpty(planXml)) return 0;
            var m = Regex.Match(planXml, @"StatementSubTreeCost=""([\d\.]+)""");
            return m.Success ? double.Parse(m.Groups[1].Value) : 0;
        }

        private static List<string> AnalyzeQueryHeuristics(string sql)
        {
            var hints  = new List<string>();
            var upper  = sql.ToUpperInvariant();

            if (!upper.Contains("WHERE") && (upper.Contains("SELECT") || upper.Contains("UPDATE") || upper.Contains("DELETE")))
                hints.Add("No WHERE clause – query may scan the entire table.");
            if (upper.Contains("SELECT *"))
                hints.Add("Avoid SELECT * – specify only required columns.");
            if (Regex.IsMatch(upper, @"\bLIKE\s+['\""]%"))
                hints.Add("Leading wildcard in LIKE prevents index seek.");
            if (Regex.IsMatch(upper, @"\bNOLOCK\b"))
                hints.Add("NOLOCK may return dirty reads – use with caution.");
            if (!upper.Contains("TOP") && !upper.Contains("ROWCOUNT") && upper.Contains("SELECT"))
                hints.Add("Consider adding TOP or OFFSET/FETCH to limit result set size.");

            return hints;
        }
    }
}
