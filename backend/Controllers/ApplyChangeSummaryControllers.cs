// ============================================================
// KITSUNE – Apply / ChangeSummary / ConnectionManager Controllers
// ============================================================
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Services;

namespace Kitsune.Backend.Controllers
{
    // ── Apply Controller ─────────────────────────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class ApplyController : ControllerBase
    {
        private readonly IApplyService _apply;
        private readonly ILogger<ApplyController> _log;

        public ApplyController(IApplyService apply, ILogger<ApplyController> log)
        { _apply = apply; _log = log; }

        /// <summary>
        /// POST /api/apply
        /// Applies a SQL script to the live database.
        /// Pre-flight: validation → auto-backup → execute → audit log.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApplyResponse), 200)]
        public async Task<IActionResult> Apply([FromBody] ApplyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.SqlScript))
                return BadRequest(new { error = "SqlScript is required." });

            _log.LogWarning("APPLY requested: {Object} ({Type})", req.ObjectName, req.ObjectType);
            var result = await _apply.ApplyAsync(req);
            return result.Success ? Ok(result) : StatusCode(500, result);
        }
    }

    // ── Change Summary Controller ─────────────────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class ChangeSummaryController : ControllerBase
    {
        private readonly IChangeSummaryService  _diff;
        private readonly IBackupVersioningService _versions;

        public ChangeSummaryController(
            IChangeSummaryService diff,
            IBackupVersioningService versions)
        { _diff = diff; _versions = versions; }

        /// <summary>
        /// POST /api/changesummary/compare
        /// Compare two arbitrary script strings.
        /// </summary>
        [HttpPost("compare")]
        public async Task<IActionResult> Compare([FromBody] CompareScriptsRequest req)
        {
            var result = await _diff.CompareVersionsAsync(
                req.ObjectName, req.OldScript, req.NewScript,
                req.OldVersion, req.NewVersion, req.Model);
            return Ok(result);
        }

        /// <summary>
        /// GET /api/changesummary/{objectName}/{versionA}/{versionB}
        /// Compare two stored versions by version number.
        /// </summary>
        [HttpGet("{objectName}/{versionA:int}/{versionB:int}")]
        public async Task<IActionResult> CompareVersions(
            string objectName, int versionA, int versionB)
        {
            var allVersions = await _versions.GetVersionsAsync(objectName);
            var vA = allVersions.Find(v => v.VersionNumber == versionA);
            var vB = allVersions.Find(v => v.VersionNumber == versionB);

            if (vA is null || vB is null)
                return NotFound(new { error = "One or both versions not found." });

            var result = _diff.ComputeDiff(
                objectName, vA.ScriptContent, vB.ScriptContent, versionA, versionB);
            return Ok(result);
        }
    }

    public class CompareScriptsRequest
    {
        public string ObjectName { get; set; } = "";
        public string OldScript  { get; set; } = "";
        public string NewScript  { get; set; } = "";
        public int    OldVersion { get; set; } = 0;
        public int    NewVersion { get; set; } = 1;
        public string Model      { get; set; } = "auto";
    }

    // ── Connection Manager Controller ─────────────────────────
    [ApiController]
    [Route("api/connections")]
    public class ConnectionsController : ControllerBase
    {
        private readonly IConnectionManagerService _connMgr;

        public ConnectionsController(IConnectionManagerService connMgr) => _connMgr = connMgr;

        /// <summary>GET /api/connections – list all profiles</summary>
        [HttpGet]
        public async Task<IActionResult> List() =>
            Ok(await _connMgr.ListProfilesAsync());

        /// <summary>POST /api/connections – save new profile</summary>
        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SaveProfileRequest req)
        {
            var id = await _connMgr.SaveProfileAsync(req);
            return Ok(new { id, message = $"Profile '{req.Name}' saved." });
        }

        /// <summary>POST /api/connections/{id}/test – test connectivity</summary>
        [HttpPost("{id:int}/test")]
        public async Task<IActionResult> Test(int id) =>
            Ok(await _connMgr.TestProfileAsync(id));

        /// <summary>POST /api/connections/test-string – test raw connection string</summary>
        [HttpPost("test-string")]
        public async Task<IActionResult> TestString([FromBody] TestStringRequest req) =>
            Ok(await _connMgr.TestConnectionStringAsync(req.ConnectionString, req.DatabaseType));

        /// <summary>DELETE /api/connections/{id}</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _connMgr.DeleteProfileAsync(id);
            return ok ? Ok(new { message = "Profile deleted." }) : NotFound();
        }
    }

    public class TestStringRequest
    {
        public string ConnectionString { get; set; } = "";
        public string DatabaseType     { get; set; } = "SqlServer";
    }
}
