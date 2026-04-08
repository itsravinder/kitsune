// ============================================================
// KITSUNE – SQL Script Runner Service
// Executes multi-statement SQL scripts with GO batch separators
// Supports: live run, dry-run (PARSEONLY), transaction wrap
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface ISqlScriptRunnerService
    {
        Task<ScriptRunResult> RunAsync(ScriptRunRequest request);
        Task<ScriptRunResult> ParseOnlyAsync(string script);
        List<string>          SplitBatches(string script);
    }

    public class SqlScriptRunnerService : ISqlScriptRunnerService
    {
        private readonly string _conn;
        private readonly ILogger<SqlScriptRunnerService> _log;

        // Matches GO on its own line (optional count, e.g. GO 3)
        private static readonly Regex _goBatch = new(
            @"^\s*GO(?:\s+(?<count>\d+))?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        public SqlScriptRunnerService(IConfiguration cfg, ILogger<SqlScriptRunnerService> log)
        // ── Main entry point ──────────────────────────────────
        public async Task<ScriptRunResult> RunAsync(ScriptRunRequest request)
        // ── PARSEONLY – syntax check, no execution ─────────────
        public async Task<ScriptRunResult> ParseOnlyAsync(string script)
        // ── Split script on GO ────────────────────────────────
        public List<string> SplitBatches(string script)
        private static string AppendDatabase(string connStr, string dbName)
    }
}
