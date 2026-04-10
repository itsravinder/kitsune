// ============================================================
// KITSUNE – Schema & Audit Controllers
// ============================================================
using System;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Services;

namespace Kitsune.Backend.Controllers
{
    // ── Schema Extraction Controller ─────────────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class SchemaController : ControllerBase
    {
        private readonly ISchemaExtractionService _schema;
        private readonly IAuditLogService         _audit;
        private readonly ILogger<SchemaController> _log;

        public SchemaController(
            ISchemaExtractionService schema,
            IAuditLogService audit,
            ILogger<SchemaController> log)
        {
            _schema = schema; _audit = audit; _log = log;
        }

        /// <summary>GET /api/schema/sqlserver?db=MyDatabase</summary>
        [HttpGet("sqlserver")]
        public async Task<IActionResult> ExtractSqlServer([FromQuery] string? db = null)
        {
            var t0 = DateTime.UtcNow;
            var result = await _schema.ExtractSqlServerSchemaAsync(db);
            await _audit.LogAsync(AuditAction.SchemaExtract, db ?? "default", "DATABASE",
                "SUCCESS", new { db }, null, durationMs: (DateTime.UtcNow - t0).TotalMilliseconds, database: db ?? "");
            return Ok(result);
        }

        /// <summary>GET /api/schema/mongodb/{database}</summary>
        [HttpGet("mongodb/{database}")]
        public async Task<IActionResult> ExtractMongo(string database)
        {
            var result = await _schema.ExtractMongoSchemaAsync(database);
            return Ok(result);
        }

        /// <summary>GET /api/schema/table/{tableName}</summary>
        [HttpGet("table/{tableName}")]
        public async Task<IActionResult> ExtractTable(string tableName)
        {
            var result = await _schema.ExtractSingleTableAsync(tableName);
            return Ok(result);
        }

        /// <summary>GET /api/schema/ddl?db=MyDatabase  – returns DDL string for AI context</summary>
        [HttpGet("ddl")]
        public async Task<IActionResult> GetDdl([FromQuery] string? db = null)
        {
            var schema = await _schema.ExtractSqlServerSchemaAsync(db);
            return Ok(new { ddl = schema.DDLSummary, tableCount = schema.Tables.Count });
        }
    }

    // ── Audit Log Controller ──────────────────────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly IAuditLogService _audit;

        public AuditController(IAuditLogService audit) => _audit = audit;

        /// <summary>GET /api/audit?objectName=usp_GetOrders&top=50</summary>
        [HttpGet]
        public async Task<IActionResult> GetLogs(
            [FromQuery] string? objectName = null,
            [FromQuery] int top = 100)
        {
            var logs = await _audit.GetLogsAsync(objectName, Math.Min(top, 500));
            return Ok(new { total = logs.Count, logs });
        }
    }

    // ── DB Info Controller ─────────────────────────────────────
    [ApiController]
    [Route("api/db-info")]
    public class DbInfoController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<DbInfoController> _log;

        public DbInfoController(IConfiguration cfg, ILogger<DbInfoController> log)
        {
            _cfg = cfg; _log = log;
        }

        /// <summary>GET /api/db-info — returns actual server + database name</summary>
        /// <summary>
        /// GET /api/db-info?db=KitsuneDB
        /// Returns actual connected server + database.
        /// If ?db= is supplied, switches to that DB first.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDbInfo([FromQuery] string? db = null)
        {
            string cs = _cfg.GetConnectionString("SqlServer") ?? "";
            try
            {
                await using var conn = new Microsoft.Data.SqlClient.SqlConnection(cs);
                await conn.OpenAsync();

                // Switch to requested DB if supplied and different
                if (!string.IsNullOrEmpty(db) &&
                    !conn.Database.Equals(db, StringComparison.OrdinalIgnoreCase))
                {
                    conn.ChangeDatabase(db);
                }

                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT @@SERVERNAME AS srv, DB_NAME() AS db, @@VERSION AS ver;", conn);
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    return Ok(new
                    {
                        serverName   = r["srv"]?.ToString() ?? conn.DataSource,
                        databaseName = r["db"]?.ToString()  ?? conn.Database,
                        serverVersion= r["ver"]?.ToString()?.Split('\n')[0]?.Trim() ?? "",
                        dataSource   = conn.DataSource,
                        connected    = true,
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "DB info query failed");
                return Ok(new { serverName = "unknown", databaseName = db ?? "unknown", connected = false, error = ex.Message });
            }
            return Ok(new { serverName = "unknown", databaseName = db ?? "unknown", connected = false });
        }
    }

}