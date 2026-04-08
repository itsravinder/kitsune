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
        // ── Full comparison with AI summary ───────────────────
        public async Task<ChangeSummaryResult> CompareVersionsAsync(
            string objectName, string oldScript, string newScript,
            int oldVersion, int newVersion, string model = "auto")
        // ── Pure text diff (Myers-inspired line diff) ─────────
        public ChangeSummaryResult ComputeDiff(
            string objectName, string oldScript, string newScript,
            int oldVersion, int newVersion)
        // ── LCS-based line diff ───────────────────────────────
        private static List<DiffLine> ComputeLineDiff(string[] oldLines, string[] newLines)
        private static string ComputeRiskLevel(List<DiffLine> diff, int totalLines)
        private static string GenerateHeuristicSummary(ChangeSummaryResult r)
    }
}
