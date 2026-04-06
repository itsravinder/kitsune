// ============================================================
// KITSUNE – Request Logging Middleware
// Logs method, path, status code, and duration for every request
// ============================================================
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kitsune.Backend.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _log;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> log)
        {
            _next = next;
            _log  = log;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip health checks and swagger noise
            var path = context.Request.Path.Value ?? "";
            if (path.StartsWith("/health") || path.StartsWith("/swagger"))
            {
                await _next(context);
                return;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                var status  = context.Response.StatusCode;
                var method  = context.Request.Method;
                var elapsed = sw.Elapsed.TotalMilliseconds;

                var level = status >= 500 ? LogLevel.Error
                          : status >= 400 ? LogLevel.Warning
                          : LogLevel.Information;

                _log.Log(level,
                    "[KITSUNE] {Method} {Path} → {Status} in {ElapsedMs:0.0}ms",
                    method, path, status, elapsed);
            }
        }
    }
}
