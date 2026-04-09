// ============================================================
// KITSUNE – Data Export Service
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
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        public async Task<ExportResult> ExportAsync(ExportRequest request)
        {
            var result = new ExportResult { Format = request.Format };
            var sw     = Stopwatch.StartNew();
            try
            {
                var cols = new List<string>();
                var rows = new List<object?[]>();

                await using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(request.SqlQuery, conn)
                {
                    CommandTimeout = 60,
                };
                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

                for (int i = 0; i < reader.FieldCount; i++)
                    cols.Add(reader.GetName(i));

                int maxRows = request.MaxRows > 0 ? request.MaxRows : 10000;
                while (await reader.ReadAsync() && rows.Count < maxRows)
                {
                    var row = new object?[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                        row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }

                result.FileData  = request.Format == "json"
                    ? BuildJson(cols, rows)
                    : BuildDelimited(cols, rows, request.Format == "tsv" ? '\t' : ',', request.IncludeHeaders);
                result.RowCount  = rows.Count;
                result.Success   = true;
                result.Message   = $"Exported {rows.Count} rows as {request.Format.ToUpperInvariant()}.";
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Export failed");
                result.Success = false;
                result.Message = $"Export error: {ex.Message}";
            }
            sw.Stop();
            result.ExecutionMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static byte[] BuildDelimited(List<string> cols, List<object?[]> rows, char sep, bool headers)
        {
            var sb = new StringBuilder();
            if (headers)
                sb.AppendLine(string.Join(sep, cols.ConvertAll(c => EscapeField(c, sep))));
            foreach (var row in rows)
            {
                var parts = new string[row.Length];
                for (int i = 0; i < row.Length; i++)
                    parts[i] = EscapeField(row[i]?.ToString() ?? "", sep);
                sb.AppendLine(string.Join(sep, parts));
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string EscapeField(string val, char sep)
        {
            if (val.Contains(sep) || val.Contains('"') || val.Contains('\n'))
                return "\"" + val.Replace("\"", "\"\"") + "\"";
            return val;
        }

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
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(list,
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
