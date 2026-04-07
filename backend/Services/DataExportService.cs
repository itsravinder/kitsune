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
    {
        public string SqlQuery     { get; set; } = "";
        public string Format       { get; set; } = "csv";  // csv | json | tsv
        public bool   IncludeHeaders { get; set; } = true;
        public int    MaxRows      { get; set; } = 10_000;
        public string FileName     { get; set; } = "kitsune-export";
    }

    {
        public bool   Success      { get; set; }
        public string ContentType  { get; set; } = "text/csv";
        public string FileName     { get; set; } = "export.csv";
        public byte[] Data         { get; set; } = Array.Empty<byte>();
        public int    RowCount     { get; set; }
        public double ExecutionMs  { get; set; }
        public string? Error       { get; set; }
    }

    public interface IDataExportService
    {
        Task<ExportResult> ExportAsync(ExportRequest request);
    }

    public class DataExportService : IDataExportService
    {
        private readonly string _conn;
        private readonly ILogger<DataExportService> _log;

        public DataExportService(IConfiguration cfg, ILogger<DataExportService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        public async Task<ExportResult> ExportAsync(ExportRequest request)
        {
            var sw     = Stopwatch.StartNew();
            var result = new ExportResult { FileName = $"{request.FileName}.{request.Format}" };

            // Set content type
            result.ContentType = request.Format.ToLower() switch
            {
                "json" => "application/json",
                "tsv"  => "text/tab-separated-values",
                _      => "text/csv",
            };

            try
            {
                await using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(request.SqlQuery, conn)
                {
                    CommandTimeout = 60
                };

                // Use read-only, forward-only reader for efficiency
                await using var reader = await cmd.ExecuteReaderAsync(
                    CommandBehavior.SequentialAccess | CommandBehavior.SingleResult);

                // Read column names
                var cols = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                    cols.Add(reader.GetName(i));

                // Read rows into memory (capped at MaxRows)
                var rows = new List<object?[]>();
                while (await reader.ReadAsync() && rows.Count < request.MaxRows)
                {
                    var row = new object?[cols.Count];
                    for (int i = 0; i < cols.Count; i++)
                        row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }

                result.RowCount = rows.Count;
                sw.Stop();
                result.ExecutionMs = sw.Elapsed.TotalMilliseconds;

                result.Data = request.Format.ToLower() switch
                {
                    "json" => BuildJson(cols, rows),
                    "tsv"  => BuildDelimited(cols, rows, '\t', request.IncludeHeaders),
                    _      => BuildDelimited(cols, rows, ',',  request.IncludeHeaders),
                };

                result.Success = true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Export failed for query");
                result.Success = false;
                result.Error   = ex.Message;
                result.Data    = Encoding.UTF8.GetBytes($"Export error: {ex.Message}");
            }

            return result;
        }

        // ── CSV / TSV builder ─────────────────────────────────
        private static byte[] BuildDelimited(
            List<string> cols, List<object?[]> rows, char sep, bool headers)
        {
            var sb = new StringBuilder();

            if (headers)
            {
                for (int i = 0; i < cols.Count; i++)
                {
                    if (i > 0) sb.Append(sep);
                    sb.Append(EscapeField(cols[i], sep));
                }
                sb.AppendLine();
            }

            foreach (var row in rows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (i > 0) sb.Append(sep);
                    sb.Append(EscapeField(row[i]?.ToString() ?? "", sep));
                }
                sb.AppendLine();
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string EscapeField(string val, char sep)
        {
            if (val.Contains(sep) || val.Contains('"') || val.Contains('\n') || val.Contains('\r'))
                return $"\"{val.Replace("\"", "\"\"")}\"";
            return val;
        }

        // ── JSON builder ──────────────────────────────────────
        private static byte[] BuildJson(List<string> cols, List<object?[]> rows)
        {
            var list = new List<Dictionary<string, object?>>();
            foreach (var row in rows)
            {
                var dict = new Dictionary<string, object?>();
                for (int i = 0; i < cols.Count; i++)
                    dict[cols[i]] = row[i];
                list.Add(dict);
            }
            return JsonSerializer.SerializeToUtf8Bytes(list,
                new JsonSerializerOptions { WriteIndented = false });
        }
    }
}
