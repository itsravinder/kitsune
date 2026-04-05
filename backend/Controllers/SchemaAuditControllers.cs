// ============================================================
// KITSUNE – Schema & Audit Controllers
// ============================================================
using System;
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
}
