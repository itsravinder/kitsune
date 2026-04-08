// ============================================================
// KITSUNE – Change Summary Service
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
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
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
            _http         = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _log          = log;
        }

        public async Task<ChangeSummaryResult> CompareVersionsAsync(
            string objectName, string oldScript, string newScript,
            int oldVersion, int newVersion, string model = "auto")
        {
            var result = ComputeDiff(objectName, oldScript, newScript, oldVersion, newVersion);

            try
            {
                var payload = new
                {
                    object_name = objectName,
                    old_script  = oldScript,
                    new_script  = newScript,
                    diff        = result.DiffLines,
                    model,
                };
                var content  = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"{_aiServiceUrl}/summarize-change", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc  = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("summary", out var s))
                        result.AiSummary = s.GetString() ?? result.HeuristicSummary;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "AI summarise failed, using heuristic summary");
            }

            return result;
        }

        public ChangeSummaryResult ComputeDiff(
            string objectName, string oldScript, string newScript,
            int oldVersion, int newVersion)
        {
            var oldLines = oldScript.Split('\n');
            var newLines = newScript.Split('\n');
            var diff     = ComputeLineDiff(oldLines, newLines);

            var result = new ChangeSummaryResult
            {
                ObjectName   = objectName,
                OldVersion   = oldVersion,
                NewVersion   = newVersion,
                DiffLines    = diff,
                AddedLines   = diff.Count(d => d.Type == "add"),
                RemovedLines = diff.Count(d => d.Type == "remove"),
                RiskLevel    = ComputeRiskLevel(diff, oldLines.Length),
            };

            result.HeuristicSummary = GenerateHeuristicSummary(result);
            result.AiSummary        = result.HeuristicSummary;
            return result;
        }

        private static List<DiffLine> ComputeLineDiff(string[] oldLines, string[] newLines)
        {
            var diff = new List<DiffLine>();
            int maxLen = Math.Max(oldLines.Length, newLines.Length);

            for (int i = 0; i < maxLen; i++)
            {
                string? oldLine = i < oldLines.Length ? oldLines[i] : null;
                string? newLine = i < newLines.Length ? newLines[i] : null;

                if (oldLine == null && newLine != null)
                    diff.Add(new DiffLine { Type = "add",    Content = newLine,  LineNumber = i + 1 });
                else if (oldLine != null && newLine == null)
                    diff.Add(new DiffLine { Type = "remove", Content = oldLine,  LineNumber = i + 1 });
                else if (oldLine != newLine)
                {
                    diff.Add(new DiffLine { Type = "remove", Content = oldLine!, LineNumber = i + 1 });
                    diff.Add(new DiffLine { Type = "add",    Content = newLine!, LineNumber = i + 1 });
                }
                else
                    diff.Add(new DiffLine { Type = "context", Content = oldLine!, LineNumber = i + 1 });
            }
            return diff;
        }

        private static string ComputeRiskLevel(List<DiffLine> diff, int totalLines)
        {
            int changes = diff.Count(d => d.Type is "add" or "remove");
            double pct  = totalLines > 0 ? (double)changes / totalLines : 0;

            var allContent = string.Join(" ", diff.Select(d => d.Content)).ToUpperInvariant();
            if (allContent.Contains("DROP") || allContent.Contains("TRUNCATE")) return "CRITICAL";
            if (pct > 0.5 || allContent.Contains("DELETE")) return "HIGH";
            if (pct > 0.2) return "MEDIUM";
            return "LOW";
        }

        private static string GenerateHeuristicSummary(ChangeSummaryResult r)
        {
            var sb = new StringBuilder();
            sb.Append($"v{r.OldVersion} → v{r.NewVersion}: ");
            sb.Append($"+{r.AddedLines} lines, -{r.RemovedLines} lines. ");
            sb.Append($"Risk: {r.RiskLevel}.");
            return sb.ToString();
        }
    }
}
