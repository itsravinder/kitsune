// ============================================================
// KITSUNE – API Controllers
// Routes: /api/validate  /api/backup  /api/versions  /api/preview
// ============================================================
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Models;
using Kitsune.Backend.Services;

namespace Kitsune.Backend.Controllers
{
    // ── [1] Dependency Validation Controller ─────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class ValidateController : ControllerBase
    {
        private readonly IDependencyValidationService _validationSvc;
        private readonly ILogger<ValidateController> _logger;

        public ValidateController(
            IDependencyValidationService validationSvc,
            ILogger<ValidateController> logger)
        {
            _validationSvc = validationSvc;
            _logger        = logger;
        }

        /// <summary>
        /// POST /api/validate
        /// Validates a proposed change and returns affected objects + status.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ValidateResponse), 200)]
        [ProducesResponseType(typeof(ProblemDetails),   400)]
        public async Task<IActionResult> Validate([FromBody] ValidateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ObjectName))
                return BadRequest(new { error = "ObjectName is required." });

            _logger.LogInformation("Validating object: {Name} ({Type})",
                request.ObjectName, request.ObjectType);

            var result = await _validationSvc.ValidateAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// GET /api/validate/dependencies/{objectName}
        /// Returns the full dependency tree for an object.
        /// </summary>
        [HttpGet("dependencies/{objectName}")]
        [ProducesResponseType(typeof(List<AffectedObject>), 200)]
        public async Task<IActionResult> GetDependencies(string objectName)
        {
            var deps = await _validationSvc.GetDependencyTreeAsync(objectName);
            return Ok(new { objectName, dependencyCount = deps.Count, dependencies = deps });
        }

        /// <summary>
        /// GET /api/validate/parameters/{objectName}
        /// Returns parameter list for a stored procedure or function.
        /// </summary>
        [HttpGet("parameters/{objectName}")]
        [ProducesResponseType(typeof(List<ParameterInfo>), 200)]
        public async Task<IActionResult> GetParameters(string objectName)
        {
            var @params = await _validationSvc.GetParametersAsync(objectName);
            return Ok(new { objectName, parameters = @params });
        }

        /// <summary>
        /// GET /api/validate/exists/{objectName}
        /// Quick existence check for an object.
        /// </summary>
        [HttpGet("exists/{objectName}")]
        public async Task<IActionResult> ObjectExists(string objectName)
        {
            var exists = await _validationSvc.ObjectExistsAsync(objectName);
            return Ok(new { objectName, exists });
        }
    }


    // ── [2] Backup + Versioning Controller ───────────────────
    [ApiController]
    [Route("api")]
    public class BackupController : ControllerBase
    {
        private readonly IBackupVersioningService _backupSvc;
        private readonly ILogger<BackupController> _logger;

        public BackupController(
            IBackupVersioningService backupSvc,
            ILogger<BackupController> logger)
        {
            _backupSvc = backupSvc;
            _logger    = logger;
        }

        /// <summary>
        /// POST /api/backup
        /// Extracts the current definition of an object and stores it as a new version.
        /// </summary>
        [HttpPost("backup")]
        [ProducesResponseType(typeof(BackupResponse), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        public async Task<IActionResult> Backup([FromBody] BackupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ObjectName))
                return BadRequest(new { error = "ObjectName is required." });

            _logger.LogInformation("Backing up: {Name}", request.ObjectName);

            var result = await _backupSvc.BackupAsync(request);
            return result.Success ? Ok(result) : StatusCode(500, result);
        }

        /// <summary>
        /// GET /api/versions/{objectName}
        /// Retrieves the last 3 versions for an object.
        /// </summary>
        [HttpGet("versions/{objectName}")]
        [ProducesResponseType(typeof(List<ObjectVersion>), 200)]
        public async Task<IActionResult> GetVersions(string objectName)
        {
            var versions = await _backupSvc.GetVersionsAsync(objectName);
            return Ok(new
            {
                objectName,
                totalVersions = versions.Count,
                versions
            });
        }

        /// <summary>
        /// GET /api/versions/{objectName}/definition
        /// Returns the current live definition for an object.
        /// </summary>
        [HttpGet("versions/{objectName}/definition")]
        public async Task<IActionResult> GetCurrentDefinition(string objectName)
        {
            var def = await _backupSvc.GetCurrentDefinitionAsync(objectName);
            if (def is null)
                return NotFound(new { error = $"No SQL definition found for '{objectName}'." });

            return Ok(new { objectName, definition = def });
        }

        /// <summary>
        /// POST /api/rollback
        /// Replaces the current object definition with the selected version.
        /// Automatically backs up the current state before rolling back.
        /// </summary>
        [HttpPost("rollback")]
        [ProducesResponseType(typeof(RollbackResponse), 200)]
        [ProducesResponseType(typeof(ProblemDetails),   400)]
        public async Task<IActionResult> Rollback([FromBody] RollbackRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ObjectName))
                return BadRequest(new { error = "ObjectName is required." });
            if (request.VersionNumber <= 0)
                return BadRequest(new { error = "VersionNumber must be a positive integer." });

            _logger.LogWarning("ROLLBACK requested: {Name} → version {V}",
                request.ObjectName, request.VersionNumber);

            var result = await _backupSvc.RollbackAsync(request);
            return result.Success ? Ok(result) : StatusCode(500, result);
        }
    }


    // ── [3] Preview Execution Controller ─────────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class PreviewController : ControllerBase
    {
        private readonly IPreviewExecutionService _previewSvc;
        private readonly ILogger<PreviewController> _logger;

        public PreviewController(
            IPreviewExecutionService previewSvc,
            ILogger<PreviewController> logger)
        {
            _previewSvc = previewSvc;
            _logger     = logger;
        }

        /// <summary>
        /// POST /api/preview
        /// Executes a SQL query or stored procedure inside BEGIN TRAN / ROLLBACK.
        /// No data is ever persisted. Returns result set, errors, and execution time.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(PreviewResponse), 200)]
        [ProducesResponseType(typeof(ProblemDetails),  400)]
        public async Task<IActionResult> Preview([FromBody] PreviewRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SqlQuery))
                return BadRequest(new { error = "SqlQuery is required." });

            _logger.LogInformation("Safe-mode preview: IsStoredProc={Sp}, Timeout={T}s",
                request.IsStoredProc, request.TimeoutSeconds);

            var result = await _previewSvc.PreviewAsync(request);
            return Ok(result);
        }
    }
}
