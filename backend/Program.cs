// ============================================================
// KITSUNE – Program.cs (v5 – all services + model loader)
// ============================================================
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Middleware;
using Kitsune.Backend.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders().AddConsole().AddDebug();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title="KITSUNE API", Version="v1",
        Description="AI Database Intelligence System – v5" }));

builder.Services.AddCors(opts =>
    opts.AddPolicy("KitsuneCors", p =>
        p.WithOrigins(
            "http://localhost:3000", "http://localhost:5173",
            builder.Configuration["Cors:AllowedOrigin"] ?? "")
         .AllowAnyHeader().AllowAnyMethod()));

// ── All Domain Services ───────────────────────────────────────
builder.Services.AddScoped<IDependencyValidationService, DependencyValidationService>();
builder.Services.AddScoped<IBackupVersioningService,     BackupVersioningService>();
builder.Services.AddScoped<IPreviewExecutionService,     PreviewExecutionService>();
builder.Services.AddScoped<ISchemaExtractionService,     SchemaExtractionService>();
builder.Services.AddScoped<IAuditLogService,             AuditLogService>();
builder.Services.AddScoped<IApplyService,                ApplyService>();
builder.Services.AddScoped<IChangeSummaryService,        ChangeSummaryService>();
builder.Services.AddScoped<IConnectionManagerService,    ConnectionManagerService>();
builder.Services.AddScoped<IQueryOptimizerService,       QueryOptimizerService>();
builder.Services.AddScoped<IMongoQueryService,           MongoQueryService>();
builder.Services.AddScoped<IScheduledBackupService,      ScheduledBackupService>();
builder.Services.AddScoped<IUserPreferencesService,      UserPreferencesService>();
builder.Services.AddScoped<ISqlScriptRunnerService,      SqlScriptRunnerService>();
builder.Services.AddScoped<IDataExportService,           DataExportService>();
builder.Services.AddScoped<INotificationService,         NotificationService>();
builder.Services.AddScoped<IModelService,                ModelService>(); // NEW: dynamic models
builder.Services.AddScoped<IQueryIntentService,          QueryIntentService>(); // v6: intent detection

builder.Services.AddHttpClient();
builder.Services.AddHostedService<BackupSchedulerWorker>();
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("SqlServer") ?? "",
        name: "sql-server", tags: new[] { "db" });

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "KITSUNE API v5"));
}

app.UseCors("KitsuneCors");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// ── Bootstrap all tables ──────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var svc = scope.ServiceProvider;
    var log = svc.GetRequiredService<ILogger<Program>>();
    try
    {
        await svc.GetRequiredService<IBackupVersioningService>().EnsureVersionTableAsync();
        await svc.GetRequiredService<IAuditLogService>().EnsureTableAsync();
        await svc.GetRequiredService<IConnectionManagerService>().EnsureTableAsync();
        await svc.GetRequiredService<IScheduledBackupService>().EnsureTableAsync();
        await svc.GetRequiredService<IUserPreferencesService>().EnsureTableAsync();
        log.LogInformation("KITSUNE v5 database tables ready");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to initialize tables. Check SQL Server connection.");
    }
}

app.Run();
