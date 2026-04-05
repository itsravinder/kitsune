// ============================================================
// KITSUNE – Program.cs (.NET 8 / minimal hosting)
// ============================================================
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────
builder.Logging
    .ClearProviders()
    .AddConsole()
    .AddDebug();

// ── Services ─────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "KITSUNE API", Version = "v1" });
});

// CORS – allow the React dev server and any configured UI origin
builder.Services.AddCors(opts =>
    opts.AddPolicy("KitsuneCors", policy =>
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                builder.Configuration["Cors:AllowedOrigin"] ?? "")
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ── KITSUNE Domain Services ───────────────────────────────────
builder.Services.AddScoped<IDependencyValidationService, DependencyValidationService>();
builder.Services.AddScoped<IBackupVersioningService,     BackupVersioningService>();
builder.Services.AddScoped<IPreviewExecutionService,     PreviewExecutionService>();
builder.Services.AddScoped<ISchemaExtractionService,     SchemaExtractionService>();
builder.Services.AddScoped<IAuditLogService,             AuditLogService>();
builder.Services.AddScoped<IApplyService,                ApplyService>();
builder.Services.AddScoped<IChangeSummaryService,        ChangeSummaryService>();
builder.Services.AddScoped<IConnectionManagerService,    ConnectionManagerService>();
builder.Services.AddScoped<IQueryOptimizerService,       QueryOptimizerService>();
builder.Services.AddHttpClient();

// ── Health checks ─────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("SqlServer") ?? "",
        name: "sql-server",
        tags: new[] { "db" });

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "KITSUNE v1"));
}

app.UseCors("KitsuneCors");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// ── Ensure ObjectVersions table exists on startup ────────────
using (var scope = app.Services.CreateScope())
{
    var backupSvc = scope.ServiceProvider.GetRequiredService<IBackupVersioningService>();
    var auditSvc  = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
    await backupSvc.EnsureVersionTableAsync();
    await auditSvc.EnsureTableAsync();
    var connMgr = scope.ServiceProvider.GetRequiredService<IConnectionManagerService>();
    await connMgr.EnsureTableAsync();
}

app.Run();
