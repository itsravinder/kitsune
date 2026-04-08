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
        public async Task<QueryPlanResult> AnalyzeAsync(OptimizeRequest request)
        // ── DMV: missing index recommendations ────────────────
        public async Task<List<MissingIndexHint>> GetMissingIndexesFromDmvAsync()
        public Task<string> GenerateIndexScriptAsync(MissingIndexHint hint)
        private static double ExtractCostFromPlan(string planXml)
        private static List<string> AnalyzeQueryHeuristics(string sql)
    }
}
