// ============================================================
// KITSUNE – Global Exception Middleware
// Catches all unhandled exceptions, returns consistent JSON
// ============================================================
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Kitsune.Backend.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _log;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> log)
        {
            _next = next;
            _log  = log;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (SqlException ex)
            {
                _log.LogError(ex, "SQL Server error on {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                await WriteErrorAsync(context, HttpStatusCode.BadGateway,
                    "DATABASE_ERROR", $"SQL Server error: {ex.Message}", ex.Number.ToString());
            }
            catch (UnauthorizedAccessException ex)
            {
                _log.LogWarning(ex, "Unauthorized on {Path}", context.Request.Path);
                await WriteErrorAsync(context, HttpStatusCode.Unauthorized,
                    "UNAUTHORIZED", ex.Message);
            }
            catch (ArgumentException ex)
            {
                _log.LogWarning(ex, "Bad argument on {Path}", context.Request.Path);
                await WriteErrorAsync(context, HttpStatusCode.BadRequest,
                    "INVALID_ARGUMENT", ex.Message);
            }
            catch (TimeoutException ex)
            {
                _log.LogError(ex, "Timeout on {Path}", context.Request.Path);
                await WriteErrorAsync(context, HttpStatusCode.GatewayTimeout,
                    "TIMEOUT", "The operation timed out. Try with a shorter query or higher timeout.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled exception on {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                await WriteErrorAsync(context, HttpStatusCode.InternalServerError,
                    "INTERNAL_ERROR",
                    context.RequestServices.GetService(typeof(Microsoft.Extensions.Hosting.IHostEnvironment))
                        is Microsoft.Extensions.Hosting.IHostEnvironment env && env.IsDevelopment()
                        ? ex.ToString()
                        : "An unexpected error occurred. Check server logs.");
            }
        }

        private static async Task WriteErrorAsync(
            HttpContext ctx, HttpStatusCode status,
            string code, string message, string? detail = null)
        {
            ctx.Response.StatusCode  = (int)status;
            ctx.Response.ContentType = "application/json";

            var payload = new
            {
                error     = code,
                message,
                detail,
                timestamp = DateTime.UtcNow,
                path      = ctx.Request.Path.Value,
            };

            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
