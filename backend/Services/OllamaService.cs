// ============================================================
// KITSUNE – OllamaService
// HTTP client for Ollama /api/generate endpoint.
//
// Key behaviours:
//  - Uses /api/generate (NOT /api/chat — 404 on older Ollama)
//  - Sends a flat "prompt" string (NOT messages[])
//  - Reads response["response"] (NOT response["message"]["content"])
//  - 2 retries with 1-second pause between attempts
//  - Fallback: primary model → fallback model → structured error
//  - Logs: selected model, prompt length, response length, elapsed ms
//  - json_mode (format:"json") ONLY for cloud/instruction-tuned models
// ============================================================
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kitsune.Backend.Services
{
    public class OllamaRequest
    {
        public string  model       { get; set; } = "";
        public string  prompt      { get; set; } = "";
        public bool    stream      { get; set; } = false;
        public string? format      { get; set; }          // null | "json"
        public OllamaOptions? options { get; set; }
    }

    public class OllamaOptions
    {
        public double temperature { get; set; } = 0.05;
        public int    num_predict { get; set; } = 2048;
    }

    public class OllamaResult
    {
        public string  Response    { get; set; } = "";
        public int     TokensUsed  { get; set; }
        public string  ModelUsed   { get; set; } = "";
        public bool    FallbackUsed{ get; set; }
        public bool    Success      { get; set; }
        public string? Error        { get; set; }
        public double  ElapsedMs   { get; set; }
    }

    public interface IOllamaService
    {
        Task<OllamaResult> GenerateAsync(
            string prompt,
            string primaryModel,
            string? fallbackModel = null,
            bool   jsonMode       = false,
            int    timeoutSeconds = 120);
    }

    public class OllamaService : IOllamaService
    {
        private readonly string     _baseUrl;
        private readonly HttpClient _http;
        private readonly ILogger<OllamaService> _log;
        private static readonly JsonSerializerOptions _jsonOpts =
            new() { PropertyNameCaseInsensitive = true };

        public OllamaService(IConfiguration cfg, IHttpClientFactory httpFactory, ILogger<OllamaService> log)
        {
            _baseUrl = cfg["Ollama:BaseUrl"] ?? "http://localhost:11434";
            _http    = httpFactory.CreateClient("Ollama");
            _log     = log;
        }

        public async Task<OllamaResult> GenerateAsync(
            string prompt,
            string primaryModel,
            string? fallbackModel = null,
            bool   jsonMode       = false,
            int    timeoutSeconds = 120)
        {
            // Try primary, then fallback
            var candidates = new[] { (primaryModel, false), (fallbackModel, true) }
                .Where(x => !string.IsNullOrEmpty(x.Item1))
                .ToArray();

            string? lastError = null;

            foreach (var (model, isFallback) in candidates)
            {
                if (model is null) continue;

                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    try
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();

                        _log.LogInformation(
                            "[OLLAMA] model={Model} fallback={Fb} attempt={A} promptLen={Len} jsonMode={Json}",
                            model, isFallback, attempt, prompt.Length, jsonMode);

                        var req = new OllamaRequest
                        {
                            model   = model,
                            prompt  = prompt,
                            stream  = false,
                            format  = jsonMode ? "json" : null,
                            options = new OllamaOptions
                            {
                                temperature = IsLocalModel(model) ? 0.05 : 0.1,
                                num_predict = 2048,
                            },
                        };

                        var reqJson = JsonSerializer.Serialize(req,
                            new JsonSerializerOptions { DefaultIgnoreCondition =
                                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

                        using var cts = new System.Threading.CancellationTokenSource(
                            TimeSpan.FromSeconds(timeoutSeconds));

                        using var httpReq = new HttpRequestMessage(HttpMethod.Post,
                            $"{_baseUrl}/api/generate")
                        {
                            Content = new StringContent(reqJson, Encoding.UTF8, "application/json"),
                        };

                        using var resp = await _http.SendAsync(httpReq, cts.Token);
                        sw.Stop();

                        if (!resp.IsSuccessStatusCode)
                        {
                            var body = await resp.Content.ReadAsStringAsync();
                            lastError = $"HTTP {(int)resp.StatusCode}: {body[..Math.Min(200, body.Length)]}";
                            _log.LogWarning("[OLLAMA] {Err}", lastError);
                            break; // non-200 won't be fixed by retry → try fallback
                        }

                        var json = await resp.Content.ReadAsStringAsync();
                        var doc  = JsonDocument.Parse(json);

                        // /api/generate returns {"response":"...","eval_count":N}
                        var text   = doc.RootElement.TryGetProperty("response", out var r)
                                     ? r.GetString() ?? ""
                                     : "";
                        var tokens = doc.RootElement.TryGetProperty("eval_count", out var ec)
                                     ? ec.GetInt32() : 0;

                        _log.LogInformation(
                            "[OLLAMA] OK model={Model} tokens={T} ms={Ms} responseLen={L}",
                            model, tokens, sw.Elapsed.TotalMilliseconds.ToString("F0"), text.Length);

                        return new OllamaResult
                        {
                            Response     = text.Trim(),
                            TokensUsed   = tokens,
                            ModelUsed    = model,
                            FallbackUsed = isFallback,
                            Success      = true,
                            ElapsedMs    = sw.Elapsed.TotalMilliseconds,
                        };
                    }
                    catch (OperationCanceledException)
                    {
                        lastError = $"Timeout after {timeoutSeconds}s";
                        _log.LogWarning("[OLLAMA] Timeout model={Model} attempt={A}", model, attempt);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                        _log.LogWarning(ex, "[OLLAMA] Error model={Model} attempt={A}", model, attempt);
                    }

                    if (attempt < 2)
                        await Task.Delay(1000); // 1-second pause before retry
                }
            }

            // All models failed
            _log.LogError("[OLLAMA] All models failed. Last error: {Err}", lastError);
            return new OllamaResult
            {
                Success  = false,
                Error    = lastError ?? "All models failed",
                ModelUsed = primaryModel,
            };
        }

        // Local models: SQLCoder-style, low temperature, plain SQL output
        // Cloud models: can handle JSON mode and instruction-following
        private static bool IsLocalModel(string modelName)
        {
            var lower = modelName.ToLowerInvariant();
            return !lower.Contains("cloud")
                && !lower.Contains("gpt-4")
                && !lower.Contains("claude");
        }
    }
}
