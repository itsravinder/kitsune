// ============================================================
// KITSUNE – Query Optimizer Service
// Analyzes query plans, missing indexes, and rewrites queries
// ============================================================
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    {
        public string  Query          { get; set; } = "";
        public string  PlanXml        { get; set; } = "";
        public double  EstimatedCost  { get; set; }
        public int     EstimatedRows  { get; set; }
        public List<MissingIndexHint> MissingIndexes { get; set; } = new();
        public List<string>           Warnings       { get; set; } = new();
        public List<string>           Suggestions    { get; set; } = new();
        public string  OverallRisk    { get; set; } = "LOW";
    }

    {
        public string TableName         { get; set; } = "";
        public string EqualityColumns   { get; set; } = "";
        public string InequalityColumns { get; set; } = "";
        public string IncludedColumns   { get; set; } = "";
        public double ImprovementFactor { get; set; }
        public string CreateStatement   { get; set; } = "";
    }

    {
        public string SqlQuery     { get; set; } = "";
        public bool   GetPlan      { get; set; } = true;
        public bool   GetIndexes   { get; set; } = true;
    }

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
            var result = new QueryPlanResult { Query = request.SqlQuery };

            await using var conn = new SqlConnection(_conn);

            conn.InfoMessage += (_, e) =>
            {
                foreach (SqlError msg in e.Errors)
                    result.Warnings.Add(msg.Message);
            };

            await conn.OpenAsync();

            // ── Get estimated execution plan XML ───────────────
            if (request.GetPlan)
            {
                try
                {
                    await new SqlCommand("SET SHOWPLAN_XML ON;", conn).ExecuteNonQueryAsync();
                    await using var planCmd    = new SqlCommand(request.SqlQuery, conn);
                    await using var planReader = await planCmd.ExecuteReaderAsync();
                    if (await planReader.ReadAsync())
                        result.PlanXml = planReader.GetString(0);
                    await planReader.CloseAsync();
                    await new SqlCommand("SET SHOWPLAN_XML OFF;", conn).ExecuteNonQueryAsync();

                    // Parse cost from XML (simplified)
                    result.EstimatedCost = ExtractCostFromPlan(result.PlanXml);
                }
                catch (SqlException ex)
                {
                    result.Warnings.Add($"Plan extraction error: {ex.Message}");
                }
            }

            // ── Check missing indexes from DMV ─────────────────
            if (request.GetIndexes)
            {
                result.MissingIndexes = await GetMissingIndexesFromDmvAsync();
                foreach (var idx in result.MissingIndexes)
                    idx.CreateStatement = await GenerateIndexScriptAsync(idx);
            }

            // ── Heuristic analysis ─────────────────────────────
            result.Suggestions = AnalyzeQueryHeuristics(request.SqlQuery);
            result.OverallRisk = result.MissingIndexes.Count > 3 ? "HIGH"
                               : result.MissingIndexes.Count > 0 ? "MEDIUM" : "LOW";

            return result;
        }

        // ── DMV: missing index recommendations ────────────────
        public async Task<List<MissingIndexHint>> GetMissingIndexesFromDmvAsync()
        {
            const string sql = @"
                SELECT TOP 20
                    OBJECT_NAME(mid.object_id)                  AS TableName,
                    mid.equality_columns                        AS EqualityColumns,
                    mid.inequality_columns                      AS InequalityColumns,
                    mid.included_columns                        AS IncludedColumns,
                    migs.avg_total_user_cost * migs.avg_user_impact
                        * (migs.user_seeks + migs.user_scans)   AS ImprovementFactor
                FROM sys.dm_db_missing_index_details  mid
                INNER JOIN sys.dm_db_missing_index_groups      mig
                    ON mig.index_handle = mid.index_handle
                INNER JOIN sys.dm_db_missing_index_group_stats migs
                    ON migs.group_handle = mig.index_group_handle
                WHERE mid.database_id = DB_ID()
                ORDER BY ImprovementFactor DESC;";

            var results = new List<MissingIndexHint>();

            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                results.Add(new MissingIndexHint
                {
                    TableName         = r["TableName"]?.ToString()         ?? "",
                    EqualityColumns   = r["EqualityColumns"]?.ToString()   ?? "",
                    InequalityColumns = r["InequalityColumns"]?.ToString() ?? "",
                    IncludedColumns   = r["IncludedColumns"]?.ToString()   ?? "",
                    ImprovementFactor = Convert.ToDouble(r["ImprovementFactor"]),
                });
            }

            return results;
        }

        public Task<string> GenerateIndexScriptAsync(MissingIndexHint hint)
        {
            var sb = new StringBuilder();
            sb.Append($"CREATE NONCLUSTERED INDEX [IX_{hint.TableName}");

            var keyCols = new List<string>();
            if (!string.IsNullOrEmpty(hint.EqualityColumns))
                keyCols.AddRange(hint.EqualityColumns.Split(','));
            if (!string.IsNullOrEmpty(hint.InequalityColumns))
                keyCols.AddRange(hint.InequalityColumns.Split(','));

            sb.Append($"_{string.Join("_", keyCols).Replace("[","").Replace("]","").Replace(" ","").Replace(",","_")}]");
            sb.AppendLine($"\nON [dbo].[{hint.TableName}]");

            var allKeyCols = new List<string>();
            if (!string.IsNullOrEmpty(hint.EqualityColumns))
                allKeyCols.Add(hint.EqualityColumns);
            if (!string.IsNullOrEmpty(hint.InequalityColumns))
                allKeyCols.Add(hint.InequalityColumns);

            sb.Append($"({string.Join(", ", allKeyCols)})");

            if (!string.IsNullOrEmpty(hint.IncludedColumns))
                sb.Append($"\nINCLUDE ({hint.IncludedColumns})");

            sb.Append(";");
            return Task.FromResult(sb.ToString());
        }

        private static double ExtractCostFromPlan(string planXml)
        {
            if (string.IsNullOrEmpty(planXml)) return 0;
            var idx = planXml.IndexOf("StatementSubTreeCost=\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return 0;
            idx += "StatementSubTreeCost=\"".Length;
            var end = planXml.IndexOf('"', idx);
            if (end < 0) return 0;
            return double.TryParse(planXml[idx..end],
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var cost) ? cost : 0;
        }

        private static List<string> AnalyzeQueryHeuristics(string sql)
        {
            var s    = sql.ToUpperInvariant();
            var tips = new List<string>();

            if (s.Contains("SELECT *"))
                tips.Add("Avoid SELECT * — specify only required columns to reduce I/O and network traffic.");
            if (s.Contains("NOLOCK") || s.Contains("WITH (NOLOCK)"))
                tips.Add("NOLOCK can cause dirty reads. Consider READ COMMITTED SNAPSHOT isolation instead.");
            if (!s.Contains("WHERE") && (s.Contains("UPDATE") || s.Contains("DELETE")))
                tips.Add("UPDATE/DELETE without WHERE clause will affect ALL rows. Add a WHERE condition.");
            if (s.Contains("CURSOR"))
                tips.Add("Cursors are slow. Consider set-based operations or window functions instead.");
            if (s.Contains("DISTINCT") && s.Contains("GROUP BY"))
                tips.Add("Using both DISTINCT and GROUP BY is redundant. Remove DISTINCT.");
            if (s.Contains("NOT IN") && s.Contains("SELECT"))
                tips.Add("NOT IN with a subquery can be slow on NULLable columns. Consider NOT EXISTS instead.");
            if (s.Contains("LIKE '%"))
                tips.Add("Leading wildcard LIKE '%...' prevents index seeks. Consider full-text search.");
            if (s.Contains("CONVERT") || s.Contains("CAST"))
                tips.Add("CAST/CONVERT in WHERE clauses prevents index usage. Compare same data types.");
            if (s.Contains("ORDER BY") && !s.Contains("TOP") && !s.Contains("FETCH"))
                tips.Add("ORDER BY without TOP/FETCH forces a full sort. Add pagination (OFFSET/FETCH NEXT).");

            if (tips.Count == 0)
                tips.Add("No obvious anti-patterns detected. Run EXPLAIN/query plan for deeper analysis.");

            return tips;
        }
    }
}
