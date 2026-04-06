// ============================================================
// KITSUNE – MongoDB / Scheduler / Preferences / Health Controllers
// ============================================================
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Services;

namespace Kitsune.Backend.Controllers
{
    // ── MongoDB Controller ────────────────────────────────────
    [ApiController]
    [Route("api/mongo")]
    public class MongoController : ControllerBase
    {
        private readonly IMongoQueryService _mongo;
        public MongoController(IMongoQueryService mongo) => _mongo = mongo;

        /// <summary>POST /api/mongo/query – execute find/aggregate/count/distinct</summary>
        [HttpPost("query")]
        public async Task<IActionResult> Query([FromBody] MongoQueryRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.DatabaseName) || string.IsNullOrWhiteSpace(req.CollectionName))
                return BadRequest(new { error = "DatabaseName and CollectionName are required." });
            var result = await _mongo.ExecuteAsync(req);
            return Ok(result);
        }

        /// <summary>GET /api/mongo/databases – list all databases</summary>
        [HttpGet("databases")]
        public async Task<IActionResult> Databases() =>
            Ok(new { databases = await _mongo.ListDatabasesAsync() });

        /// <summary>GET /api/mongo/databases/{db}/collections</summary>
        [HttpGet("databases/{db}/collections")]
        public async Task<IActionResult> Collections(string db) =>
            Ok(new { database = db, collections = await _mongo.ListCollectionsAsync(db) });
    }

    // ── Backup Scheduler Controller ───────────────────────────
    [ApiController]
    [Route("api/schedules")]
    public class SchedulesController : ControllerBase
    {
        private readonly IScheduledBackupService _sched;
        public SchedulesController(IScheduledBackupService sched) => _sched = sched;

        /// <summary>GET /api/schedules – list all backup schedules</summary>
        [HttpGet]
        public async Task<IActionResult> List() =>
            Ok(await _sched.ListSchedulesAsync());

        /// <summary>POST /api/schedules – add a new backup schedule</summary>
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ScheduleRequest req)
        {
            var id = await _sched.AddScheduleAsync(req);
            return Ok(new { id, message = $"Schedule created for '{req.ObjectName}' every {req.IntervalMinutes}min." });
        }

        /// <summary>PATCH /api/schedules/{id}/toggle – enable or disable</summary>
        [HttpPatch("{id:int}/toggle")]
        public async Task<IActionResult> Toggle(int id, [FromQuery] bool enabled)
        {
            var ok = await _sched.ToggleScheduleAsync(id, enabled);
            return ok ? Ok(new { message = $"Schedule {(enabled?"enabled":"disabled")}." }) : NotFound();
        }

        /// <summary>DELETE /api/schedules/{id}</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _sched.DeleteScheduleAsync(id);
            return ok ? Ok(new { message = "Schedule deleted." }) : NotFound();
        }
    }

    // ── User Preferences Controller ───────────────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class PreferencesController : ControllerBase
    {
        private readonly IUserPreferencesService _prefs;
        public PreferencesController(IUserPreferencesService prefs) => _prefs = prefs;

        /// <summary>GET /api/preferences – load current preferences</summary>
        [HttpGet]
        public async Task<IActionResult> Get() =>
            Ok(await _prefs.GetAsync());

        /// <summary>PUT /api/preferences – save preferences</summary>
        [HttpPut]
        public async Task<IActionResult> Save([FromBody] UserPreferences prefs)
        {
            await _prefs.SaveAsync(prefs);
            return Ok(new { message = "Preferences saved." });
        }
    }

    // ── System Health Dashboard Controller ────────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class HealthDashboardController : ControllerBase
    {
        private readonly IDependencyValidationService _val;
        private readonly IBackupVersioningService     _backup;
        private readonly IAuditLogService             _audit;
        private readonly IScheduledBackupService      _sched;
        private readonly ILogger<HealthDashboardController> _log;

        public HealthDashboardController(
            IDependencyValidationService val,
            IBackupVersioningService backup,
            IAuditLogService audit,
            IScheduledBackupService sched,
            ILogger<HealthDashboardController> log)
        {
            _val = val; _backup = backup; _audit = audit;
            _sched = sched; _log = log;
        }

        /// <summary>GET /api/healthdashboard – full system status snapshot</summary>
        [HttpGet]
        public async Task<IActionResult> GetDashboard()
        {
            var recentLogs = await _audit.GetLogsAsync(null, 20);
            var schedules  = await _sched.ListSchedulesAsync();

            var stats = new
            {
                timestamp       = DateTime.UtcNow,
                recentActivity  = recentLogs.Count,
                activeSchedules = schedules.Count,
                recentLogs      = recentLogs,
                schedules       = schedules,
                systemStatus    = "OK",
            };

            return Ok(stats);
        }
    }
}
