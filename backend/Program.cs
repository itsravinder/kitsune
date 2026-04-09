// ============================================================
// KITSUNE – Program.cs (v6 – fixed middleware order + CORS)
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
        Description="AI Database Intelligence System – v6" }));

// ── CORS – allow React UI on :3000 and :5173 ─────────────────
builder.Services.AddCors(opts =>
    opts.AddPolicy("KitsuneCors", p =>
        p.WithOrigins(
            "http://localhost:3000",
            "http://localhost:5173",
            builder.Configuration["Cors:AllowedOrigin"] ?? "")
         .AllowAnyHeader()
         .AllowAnyMethod()));

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
builder.Services.AddScoped<IModelService,                ModelService>();
builder.Services.AddScoped<IQueryIntentService,          QueryIntentService>();

builder.Services.AddHttpClient();
builder.Services.AddHostedService<BackupSchedulerWorker>();
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("SqlServer") ?? "",
        name: "sql-server", tags: new[] { "db" });

var app = builder.Build();

// ── Middleware pipeline – ORDER IS CRITICAL ───────────────────
// 1. CORS must be first so preflight OPTIONS requests are handled
//    before any other middleware rejects them
app.UseCors("KitsuneCors");

// 2. Custom middleware (logging, exceptions) come after CORS
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

// 3. Swagger only in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "KITSUNE API v6"));
}

// 4. Routing, auth, controllers
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// 5. Health endpoint – must explicitly require CORS policy
//    otherwise the browser preflight OPTIONS is rejected
app.MapHealthChecks("/health").RequireCors("KitsuneCors");

// ── Bootstrap: create all Kitsune system tables on startup ────
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
        log.LogInformation("KITSUNE v6 database tables ready");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to initialize tables. Check SQL Server connection.");
    }
}

app.Run();
