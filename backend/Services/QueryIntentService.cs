// ============================================================
// KITSUNE – Query Intent Service
// Detects READ (SELECT) vs WRITE (INSERT/UPDATE/DELETE/DDL)
// Returns confidence score + risk level + syntax validation
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kitsune.Backend.Services
{
    public enum QueryMode { Read, Write, Unknown }

    public class QueryIntent
    {
        public QueryMode Mode            { get; set; } = QueryMode.Unknown;
        public string    ModeLabel       { get; set; } = "Unknown";
        public string    ModeColor       { get; set; } = "gray";   // green | amber | red
        public string    PrimaryStatement { get; set; } = "";      // SELECT | INSERT | ALTER …
        public int       ConfidenceScore { get; set; } = 0;        // 0-100
        public string    RiskLevel       { get; set; } = "LOW";    // LOW | MEDIUM | HIGH | CRITICAL
        public List<string> DetectedOperations { get; set; } = new();
        public List<string> SyntaxErrors       { get; set; } = new();
        public List<string> Warnings           { get; set; } = new();
        public bool      IsValid               { get; set; } = true;
        public bool      RequiresValidation    { get; set; } = false;
        public string    SafetyMessage         { get; set; } = "";
        public List<string> TabsToShow         { get; set; } = new();
        public string    DefaultTab            { get; set; } = "results";
    }

    public class IntentRequest
    {
        public string Sql        { get; set; } = "";
        public string DbType     { get; set; } = "SqlServer";
        public bool   ParseOnly  { get; set; } = true;
    }

    public interface IQueryIntentService
    {
        Task<QueryIntent> AnalyzeAsync(IntentRequest request);
        QueryIntent       AnalyzeHeuristic(string sql);
    }

    public class QueryIntentService : IQueryIntentService
    {
        private readonly string _conn;
        private readonly ILogger<QueryIntentService> _log;

        // ── Write-mode statement patterns ─────────────────────
        private static readonly string[] WritePatterns =
        {
            @"\bINSERT\s+INTO\b", @"\bUPDATE\b.+\bSET\b",
            @"\bDELETE\s+FROM\b", @"\bDELETE\b",
            @"\bCREATE\s+(TABLE|VIEW|PROCEDURE|FUNCTION|INDEX|TRIGGER|DATABASE|SCHEMA)\b",
            @"\bALTER\s+(TABLE|VIEW|PROCEDURE|FUNCTION|DATABASE|SCHEMA|COLUMN)\b",
            @"\bDROP\s+(TABLE|VIEW|PROCEDURE|FUNCTION|INDEX|TRIGGER|DATABASE)\b",
            @"\bTRUNCATE\b", @"\bMERGE\b", @"\bEXEC(UTE)?\s+\w",
            @"\bSP_\w+\b", @"\bUSP_\w+\b",
        };

        // ── Read-only patterns ─────────────────────────────────
        private static readonly string[] ReadPatterns =
        {
            @"^\s*SELECT\b", @"^\s*WITH\s+\w+\s+AS",
            @"^\s*;?\s*SELECT\b",
        };

        // ── Destructive (CRITICAL risk) ────────────────────────
        private static readonly string[] CriticalPatterns =
        {
            @"\bDROP\s+(TABLE|DATABASE)\b",
            @"\bTRUNCATE\b",
            @"\bDELETE\s+FROM\b\s+\w+\s*;?\s*$",   // DELETE without WHERE
            @"\bUPDATE\b.+(?!WHERE)",                 // UPDATE without WHERE
        };

        public QueryIntentService(IConfiguration cfg, ILogger<QueryIntentService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        public async Task<QueryIntent> AnalyzeAsync(IntentRequest request)
        {
            // 1. Heuristic detection (fast, always runs)
            var intent = AnalyzeHeuristic(request.Sql);

            // 2. SQL Server syntax check (optional, only for SqlServer)
            if (request.ParseOnly && request.DbType == "SqlServer" && !string.IsNullOrWhiteSpace(request.Sql))
            {
                var errors = await SyntaxCheckAsync(request.Sql);
                intent.SyntaxErrors.AddRange(errors);
                if (errors.Count > 0)
                {
                    intent.IsValid       = false;
                    intent.ConfidenceScore = Math.Max(0, intent.ConfidenceScore - 30);
                }
            }

            return intent;
        }

        public QueryIntent AnalyzeHeuristic(string sql)
        {
            var intent = new QueryIntent();
            if (string.IsNullOrWhiteSpace(sql))
            {
                intent.Mode       = QueryMode.Unknown;
                intent.ModeLabel  = "No query";
                intent.ModeColor  = "gray";
                intent.TabsToShow = new List<string> { "results", "explain", "schema", "export", "preferences" };
                intent.DefaultTab = "results";
                return intent;
            }

            var upper = Regex.Replace(sql.ToUpperInvariant().Trim(), @"\s+", " ");
            var ops   = new List<string>();

            // Detect all operations present
            foreach (var p in WritePatterns)
                if (Regex.IsMatch(upper, p, RegexOptions.IgnoreCase))
                {
                    var m = Regex.Match(upper, p, RegexOptions.IgnoreCase);
                    ops.Add(m.Value.Trim().Split(' ')[0]);
                }

            bool isRead  = ReadPatterns.Any(p => Regex.IsMatch(upper, p, RegexOptions.IgnoreCase)) && ops.Count == 0;
            bool isWrite = ops.Count > 0;

            // Check for critical destructive patterns
            bool isCritical = CriticalPatterns.Any(p =>
                Regex.IsMatch(upper, p, RegexOptions.IgnoreCase | RegexOptions.Singleline));

            // Detect missing WHERE on UPDATE/DELETE
            bool deleteWithoutWhere = Regex.IsMatch(upper, @"\bDELETE\s+FROM\s+\w+\s*$") ||
                                      (upper.Contains("DELETE") && !upper.Contains("WHERE"));
            bool updateWithoutWhere = Regex.IsMatch(upper, @"\bUPDATE\b") && !upper.Contains("WHERE");

            if (deleteWithoutWhere || updateWithoutWhere)
            {
                isCritical = true;
                intent.Warnings.Add(deleteWithoutWhere
                    ? "DELETE without WHERE will affect ALL rows!"
                    : "UPDATE without WHERE will affect ALL rows!");
            }

            // Populate intent
            if (isRead)
            {
                intent.Mode              = QueryMode.Read;
                intent.ModeLabel         = "Read Mode (Safe)";
                intent.ModeColor         = "green";
                intent.PrimaryStatement  = "SELECT";
                intent.ConfidenceScore   = 92;
                intent.RiskLevel         = "LOW";
                intent.RequiresValidation = false;
                intent.SafetyMessage     = "Read-only query. Safe to execute.";
                intent.TabsToShow        = new List<string>
                    { "results", "explain", "schema", "depmap", "export", "preferences" };
                intent.DefaultTab        = "results";
            }
            else if (isWrite)
            {
                intent.Mode              = QueryMode.Write;
                intent.ModeLabel         = "Change Mode (Risky)";
                intent.ModeColor         = isCritical ? "red" : "amber";
                intent.PrimaryStatement  = ops.FirstOrDefault() ?? "WRITE";
                intent.DetectedOperations = ops.Distinct().ToList();
                intent.RequiresValidation = true;
                intent.SafetyMessage     = isCritical
                    ? "CRITICAL: This operation is destructive and irreversible."
                    : "Write operation detected. Validation required before execution.";
                intent.RiskLevel         = isCritical ? "CRITICAL"
                    : (ops.Any(o => o is "DROP" or "ALTER") ? "HIGH" : "MEDIUM");
                intent.ConfidenceScore   = isCritical ? 20 : 65;
                intent.TabsToShow        = new List<string>
                    { "validation", "diff", "depmap", "risk", "history", "script", "audit",
                      "results", "schema", "explain", "optimizer", "schedules" };
                intent.DefaultTab        = "validation";
            }
            else
            {
                intent.Mode       = QueryMode.Unknown;
                intent.ModeLabel  = "Unknown";
                intent.ModeColor  = "gray";
                intent.RiskLevel  = "LOW";
                intent.ConfidenceScore = 50;
                intent.TabsToShow = new List<string>
                    { "results", "validation", "explain", "schema", "export" };
                intent.DefaultTab = "results";
            }

            // Compute final confidence from all signals
            intent.ConfidenceScore = ComputeConfidence(sql, intent);

            return intent;
        }

        // ── Confidence scoring ────────────────────────────────
        private static int ComputeConfidence(string sql, QueryIntent intent)
        {
            int score = intent.ConfidenceScore;

            // Positive signals
            if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) score += 5;
            if (Regex.IsMatch(sql, @"\bWHERE\b", RegexOptions.IgnoreCase))               score += 5;
            if (Regex.IsMatch(sql, @"\bTOP\b|\bLIMIT\b", RegexOptions.IgnoreCase))       score += 3;
            if (Regex.IsMatch(sql, @"\bBEGIN\s+TRAN", RegexOptions.IgnoreCase))          score += 8;

            // Negative signals
            if (intent.Warnings.Count > 0) score -= 15 * intent.Warnings.Count;
            if (intent.RiskLevel == "CRITICAL") score -= 40;
            if (intent.RiskLevel == "HIGH")     score -= 20;
            if (intent.SyntaxErrors.Count > 0)  score -= 30;

            return Math.Clamp(score, 0, 100);
        }

        // ── PARSEONLY syntax check ─────────────────────────────
        private async Task<List<string>> SyntaxCheckAsync(string sql)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(_conn)) return errors;
            try
            {
                await using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                await new SqlCommand("SET PARSEONLY ON;", conn).ExecuteNonQueryAsync();
                try
                {
                    await new SqlCommand(sql, conn) { CommandTimeout = 5 }.ExecuteNonQueryAsync();
                }
                catch (SqlException ex)
                {
                    foreach (SqlError e in ex.Errors)
                        errors.Add($"Line {e.LineNumber}: {e.Message}");
                }
                finally
                {
                    try { await new SqlCommand("SET PARSEONLY OFF;", conn).ExecuteNonQueryAsync(); } catch { }
                }
            }
            catch { /* ignore connection errors in syntax check */ }
            return errors;
        }
    }
}
