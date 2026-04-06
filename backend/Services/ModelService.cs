// ============================================================
// KITSUNE – Dynamic Model Loading Service
// Fetches available models from Ollama GET /api/tags
// Never hardcodes model names – always live from Ollama
// ============================================================
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kitsune.Backend.Services
{
    public class OllamaModel
    {
        public string Id           { get; set; } = "";
        public string Name         { get; set; } = "";
        public string DisplayName  { get; set; } = "";
        public string Family       { get; set; } = "";
        public long   SizeBytes    { get; set; }
        public string SizeFormatted{ get; set; } = "";
        public string ModifiedAt   { get; set; } = "";
        public bool   Available    { get; set; } = true;
        public string Type         { get; set; } = "local"; // local|cloud
        public string BestFor      { get; set; } = "general";
    }

    public interface IModelService
    {
        Task<List<OllamaModel>> GetAvailableModelsAsync();
        Task<bool>              IsModelAvailableAsync(string modelName);
    }

    public class ModelService : IModelService
    {
        private readonly string     _ollamaBaseUrl;
        private readonly HttpClient _http;
        private readonly ILogger<ModelService> _log;

        // Built-in fallback models shown when Ollama is unreachable
        private static readonly List<OllamaModel> FallbackModels = new()
        {
            new() { Id="auto",        Name="auto",                       DisplayName="Auto-Route",           Type="system", Available=true,  BestFor="all" },
            new() { Id="sqlcoder",    Name="defog/sqlcoder",             DisplayName="SQLCoder (Local)",     Type="local",  Available=false, BestFor="sql" },
            new() { Id="qwen3-coder", Name="qwen3-coder:480b-cloud",    DisplayName="Qwen3 480B (Cloud)",   Type="cloud",  Available=false, BestFor="complex" },
        };

        public ModelService(IConfiguration cfg, HttpClient http, ILogger<ModelService> log)
        {
            _ollamaBaseUrl = cfg["Ollama:BaseUrl"] ?? "http://localhost:11434";
            _http          = http;
            _log           = log;
        }

        public async Task<List<OllamaModel>> GetAvailableModelsAsync()
        {
            var models = new List<OllamaModel>
            {
                new() { Id="auto", Name="auto", DisplayName="Auto-Route", Type="system", Available=true, BestFor="all" }
            };

            try
            {
                var resp = await _http.GetAsync($"{_ollamaBaseUrl}/api/tags");
                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogWarning("Ollama returned {Status}", resp.StatusCode);
                    models.AddRange(FallbackModels.GetRange(1, FallbackModels.Count - 1));
                    return models;
                }

                var json   = await resp.Content.ReadAsStringAsync();
                var doc    = JsonDocument.Parse(json);
                var tags   = doc.RootElement.GetProperty("models");

                foreach (var model in tags.EnumerateArray())
                {
                    var rawName = model.GetProperty("name").GetString() ?? "";
                    var size    = model.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0L;
                    var modAt   = model.TryGetProperty("modified_at", out var ma) ? ma.GetString() ?? "" : "";
                    var family  = model.TryGetProperty("details", out var det)
                                  && det.TryGetProperty("family", out var fam)
                                  ? fam.GetString() ?? "" : "";

                    models.Add(new OllamaModel
                    {
                        Id            = rawName,
                        Name          = rawName,
                        DisplayName   = FormatDisplayName(rawName),
                        Family        = family,
                        SizeBytes     = size,
                        SizeFormatted = FormatSize(size),
                        ModifiedAt    = modAt,
                        Available     = true,
                        Type          = DetectType(rawName),
                        BestFor       = DetectBestFor(rawName),
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Cannot reach Ollama at {Url} – using fallback model list", _ollamaBaseUrl);
                models.AddRange(FallbackModels.GetRange(1, FallbackModels.Count - 1));
            }

            return models;
        }

        public async Task<bool> IsModelAvailableAsync(string modelName)
        {
            var models = await GetAvailableModelsAsync();
            return models.Exists(m => m.Name == modelName || m.Id == modelName);
        }

        // ── Helpers ───────────────────────────────────────────
        private static string FormatDisplayName(string name)
        {
            // e.g. "defog/sqlcoder:latest" → "SQLCoder"
            var part = name.Split('/').Last().Split(':')[0];
            return part switch
            {
                "sqlcoder"   => "SQLCoder",
                "llama3"     => "Llama 3",
                "llama3.1"   => "Llama 3.1",
                "llama3.2"   => "Llama 3.2",
                "mistral"    => "Mistral",
                "codellama"  => "Code Llama",
                "qwen2.5"    => "Qwen 2.5",
                _            => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(part),
            };
        }

        private static string DetectType(string name)
        {
            if (name.Contains("cloud")) return "cloud";
            return "local";
        }

        private static string DetectBestFor(string name)
        {
            var lower = name.ToLowerInvariant();
            if (lower.Contains("sql") || lower.Contains("coder")) return "sql";
            if (lower.Contains("llama") || lower.Contains("mistral")) return "general";
            if (lower.Contains("qwen")) return "complex";
            return "general";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "";
            if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:0.0}GB";
            if (bytes >= 1_000_000)     return $"{bytes / 1_000_000.0:0.0}MB";
            return $"{bytes / 1_000.0:0.0}KB";
        }
    }
}
