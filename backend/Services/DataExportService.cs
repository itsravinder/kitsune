// ============================================================
// KITSUNE – Data Export Service
// Exports query results to CSV, JSON, or TSV
// ============================================================
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface IDataExportService
    {
        Task<ExportResult> ExportAsync(ExportRequest request);
    }

    public class DataExportService : IDataExportService
    {
        private readonly string _conn;
        private readonly ILogger<DataExportService> _log;

        public DataExportService(IConfiguration cfg, ILogger<DataExportService> log)
        public async Task<ExportResult> ExportAsync(ExportRequest request)
        // ── CSV / TSV builder ─────────────────────────────────
        private static byte[] BuildDelimited(
            List<string> cols, List<object?[]> rows, char sep, bool headers)
        private static string EscapeField(string val, char sep)
        // ── JSON builder ──────────────────────────────────────
        private static byte[] BuildJson(List<string> cols, List<object?[]> rows)
    }
}
