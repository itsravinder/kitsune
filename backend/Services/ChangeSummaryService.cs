// ============================================================
// KITSUNE – Change Summary Service
// Generates human-readable diffs between object versions
// using text diffing + optional AI summarisation
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kitsune.Backend.Services
{
    public class DiffLine
    {
        public string Type    { get; set; } = ""; // added | removed | unchanged
        public int    LineNum { get; set; }
        public string Content { get; set; } = "";
    }

    public class ChangeSummaryResult
    {
        public string          ObjectName   { get; set; } = "";
        public int             OldVersion   { get; set; }
        public int             NewVersion   { get; set; }
        public List<DiffLine>  Diff         { get; set; } = new();
        public int             LinesAdded   { get; set; }
        public int             LinesRemoved { get; set; }
        public int             LinesChanged { get; set; }
        public string          AiSummary    { get; set; } = "";
        public List<string>    KeyChanges   { get; set; } = new();
        public string          RiskLevel    { get; set; } = "LOW";
    }

    public interface IChangeSummaryService
    {
        Task<ChangeSummaryResult> CompareVersionsAsync(
            string objectName, string oldScript, string newScript,
            int oldVersion, int newVersion, string model = "auto");

        ChangeSummaryResult ComputeDiff(
            string objectName, string oldScript, string newScript,
            int oldVersion, int newVersion);
    }

    public class ChangeSummaryService : IChangeSummaryService
    {
        private readonly string     _aiServiceUrl;
        private readonly HttpClient _http;
        private readonly ILogger<ChangeSummaryService> _log;

        public ChangeSummaryService(IConfiguration cfg, ILogger<ChangeSummaryService> log)
        {
            _aiServiceUrl = cfg["AiService:BaseUrl"] ?? "http://localhost:8000";
            _http         = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            _log          = log;
        }

        // ── Full comparison with AI summary ───────────────────
        public async Task<ChangeSummaryResult> CompareVersionsAsync(
            string objectName, string oldScript, string newScript,
            int oldVersion, int newVersion, string model = "auto")
        {
            var result = ComputeDiff(objectName, oldScript, newScript, oldVersion, newVersion);

            // AI summary via Python service
            try
            {
                var payload = new
                {
                    old_script  = oldScript,
                    new_script  = newScript,
                    object_name = objectName,
                    model
                };

                var json     = JsonSerializer.Serialize(payload);
                var content  = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"{_aiServiceUrl}/summarize-change", content);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    var doc  = JsonDocument.Parse(body);
                    result.AiSummary  = doc.RootElement.GetProperty("summary").GetString()  ?? "";
                    result.KeyChanges = doc.RootElement.GetProperty("key_changes")
                        .EnumerateArray()
                        .Select(x => x.GetString() ?? "")
                        .ToList();
                    result.RiskLevel  = doc.RootElement.GetProperty("risk_level").GetString() ?? "LOW";
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "AI change summary failed, using heuristic only");
                result.AiSummary = GenerateHeuristicSummary(result);
            }

            return result;
        }

        // ── Pure text diff (Myers-inspired line diff) ─────────
        public ChangeSummaryResult ComputeDiff(
            string objectName, string oldScript, string newScript,
            int oldVersion, int newVersion)
        {
            var oldLines = oldScript.Split('\n');
            var newLines = newScript.Split('\n');

            var diff = ComputeLineDiff(oldLines, newLines);

            var result = new ChangeSummaryResult
            {
                ObjectName   = objectName,
                OldVersion   = oldVersion,
                NewVersion   = newVersion,
                Diff         = diff,
                LinesAdded   = diff.Count(d => d.Type == "added"),
                LinesRemoved = diff.Count(d => d.Type == "removed"),
                LinesChanged = 0,
            };

            // Heuristic risk: large deletes or structural keywords changed
            result.RiskLevel = ComputeRiskLevel(diff, oldLines.Length);
            result.AiSummary = GenerateHeuristicSummary(result);

            return result;
        }

        // ── LCS-based line diff ───────────────────────────────
        private static List<DiffLine> ComputeLineDiff(string[] oldLines, string[] newLines)
        {
            int m = oldLines.Length;
            int n = newLines.Length;

            // Build LCS table
            var lcs = new int[m + 1, n + 1];
            for (int i = 1; i <= m; i++)
                for (int j = 1; j <= n; j++)
                    lcs[i, j] = oldLines[i - 1].Trim() == newLines[j - 1].Trim()
                        ? lcs[i - 1, j - 1] + 1
                        : Math.Max(lcs[i - 1, j], lcs[i, j - 1]);

            // Backtrack
            var diff  = new List<DiffLine>();
            int oi = m, ni = n;

            while (oi > 0 || ni > 0)
            {
                if (oi > 0 && ni > 0 && oldLines[oi - 1].Trim() == newLines[ni - 1].Trim())
                {
                    diff.Add(new DiffLine { Type = "unchanged", LineNum = ni, Content = newLines[ni - 1] });
                    oi--; ni--;
                }
                else if (ni > 0 && (oi == 0 || lcs[oi, ni - 1] >= lcs[oi - 1, ni]))
                {
                    diff.Add(new DiffLine { Type = "added",    LineNum = ni, Content = newLines[ni - 1] });
                    ni--;
                }
                else
                {
                    diff.Add(new DiffLine { Type = "removed",  LineNum = oi, Content = oldLines[oi - 1] });
                    oi--;
                }
            }

            diff.Reverse();
            return diff;
        }

        private static string ComputeRiskLevel(List<DiffLine> diff, int totalLines)
        {
            int removed = diff.Count(d => d.Type == "removed");
            double pct  = totalLines > 0 ? (double)removed / totalLines : 0;

            bool hasDestructive = diff
                .Where(d => d.Type == "removed")
                .Any(d =>
                    d.Content.Contains("DROP",       StringComparison.OrdinalIgnoreCase) ||
                    d.Content.Contains("TRUNCATE",   StringComparison.OrdinalIgnoreCase) ||
                    d.Content.Contains("DELETE FROM", StringComparison.OrdinalIgnoreCase));

            if (hasDestructive || pct > 0.6)  return "HIGH";
            if (pct > 0.3)                    return "MEDIUM";
            return "LOW";
        }

        private static string GenerateHeuristicSummary(ChangeSummaryResult r)
        {
            if (r.LinesAdded == 0 && r.LinesRemoved == 0)
                return "No significant changes detected between versions.";

            return $"Changes between v{r.OldVersion} and v{r.NewVersion}: " +
                   $"{r.LinesAdded} line(s) added, {r.LinesRemoved} line(s) removed. " +
                   $"Risk level: {r.RiskLevel}.";
        }
    }
}
