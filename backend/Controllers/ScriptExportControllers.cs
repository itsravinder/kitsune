// ============================================================
// KITSUNE – Script Runner & Export Controllers
// ============================================================
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Kitsune.Backend.Services;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Controllers
{
    // ── Script Runner Controller ──────────────────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class ScriptController : ControllerBase
    {
        private readonly ISqlScriptRunnerService _runner;
        public ScriptController(ISqlScriptRunnerService runner) => _runner = runner;

        /// <summary>
        /// POST /api/script/run
        /// Executes a multi-statement SQL script with GO batch splitting.
        /// Set dryRun=true for syntax-only validation (PARSEONLY).
        /// Set useTransaction=true to wrap all batches in one transaction.
        /// </summary>
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] ScriptRunRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.SqlScript))
                return BadRequest(new { error = "SqlScript is required." });

            var result = await _runner.RunAsync(req);
            return Ok(result);
        }

        /// <summary>
        /// POST /api/script/validate
        /// Syntax-validates a SQL script without executing it (PARSEONLY).
        /// </summary>
        [HttpPost("validate")]
        public async Task<IActionResult> Validate([FromBody] ParseRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.SqlScript))
                return BadRequest(new { error = "SqlScript is required." });

            var result = await _runner.ParseOnlyAsync(req.SqlScript);
            return Ok(result);
        }

        /// <summary>
        /// POST /api/script/split
        /// Returns the GO-split batches without executing them.
        /// Useful for previewing how a script will be divided.
        /// </summary>
        [HttpPost("split")]
        public IActionResult Split([FromBody] ParseRequest req)
        {
            var batches = _runner.SplitBatches(req.SqlScript ?? "");
            return Ok(new { count = batches.Count, batches });
        }
    }

    public class ParseRequest
    {
        public string? SqlScript { get; set; }
    }

    // ── Export Controller ─────────────────────────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class ExportController : ControllerBase
    {
        private readonly IDataExportService _export;
        public ExportController(IDataExportService export) => _export = export;

        /// <summary>
        /// POST /api/export
        /// Executes a SQL query and returns results as CSV, JSON, or TSV.
        /// The response is a file download.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Export([FromBody] ExportRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.SqlQuery))
                return BadRequest(new { error = "SqlQuery is required." });

            var result = await _export.ExportAsync(req);

            if (!result.Success)
                return StatusCode(500, new { error = result.Error });

            return File(result.Data, result.ContentType, result.FileName);
        }

        /// <summary>
        /// GET /api/export/formats
        /// Returns supported export formats.
        /// </summary>
        [HttpGet("formats")]
        public IActionResult Formats() =>
            Ok(new
            {
                formats = new[]
                {
                    new { id = "csv",  name = "CSV (Comma Separated)",  contentType = "text/csv" },
                    new { id = "tsv",  name = "TSV (Tab Separated)",     contentType = "text/tab-separated-values" },
                    new { id = "json", name = "JSON Array",              contentType = "application/json" },
                }
            });
    }
}
