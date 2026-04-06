// ============================================================
// KITSUNE – Enhancement Controllers v5
// New: /api/models  /api/schema/tree  /api/connections/test-raw
//      /api/schema/objects  /api/connections/{id}/objects
// ============================================================
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Services;

namespace Kitsune.Backend.Controllers
{
    // ── Dynamic Model Loading ─────────────────────────────────
    [ApiController]
    [Route("api/[controller]")]
    public class ModelsController : ControllerBase
    {
        private readonly IModelService _models;
        public ModelsController(IModelService models) => _models = models;

        /// <summary>GET /api/models – fetch available models from Ollama dynamically</summary>
        [HttpGet]
        public async Task<IActionResult> List() =>
            Ok(await _models.GetAvailableModelsAsync());

        /// <summary>GET /api/models/{name}/check – check if a specific model is available</summary>
        [HttpGet("{name}/check")]
        public async Task<IActionResult> Check(string name) =>
            Ok(new { model = name, available = await _models.IsModelAvailableAsync(name) });
    }

    // ── Enhanced Connections Controller ───────────────────────
    [ApiController]
    [Route("api/connections")]
    public class ConnectionsController : ControllerBase
    {
        private readonly IConnectionManagerService _connMgr;
        private readonly ILogger<ConnectionsController> _log;

        public ConnectionsController(IConnectionManagerService connMgr, ILogger<ConnectionsController> log)
        { _connMgr = connMgr; _log = log; }

        [HttpGet]
        public async Task<IActionResult> List() => Ok(await _connMgr.ListProfilesAsync());

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SaveProfileRequest req)
        {
            var id = await _connMgr.SaveProfileAsync(req);
            return Ok(new { id, message = $"Profile '{req.Name}' saved." });
        }

        [HttpPost("{id:int}/test")]
        public async Task<IActionResult> Test(int id) => Ok(await _connMgr.TestProfileAsync(id));

        /// <summary>POST /api/connections/test-raw – test before saving (no ID required)</summary>
        [HttpPost("test-raw")]
        public async Task<IActionResult> TestRaw([FromBody] SaveProfileRequest req) =>
            Ok(await _connMgr.TestRawAsync(req));

        /// <summary>POST /api/connections/test-string – legacy compat</summary>
        [HttpPost("test-string")]
        public async Task<IActionResult> TestString([FromBody] TestStringRequest req) =>
            Ok(await _connMgr.TestConnectionStringAsync(req.ConnectionString, req.DatabaseType));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _connMgr.DeleteProfileAsync(id);
            return ok ? Ok(new { message = "Deleted." }) : NotFound();
        }

        /// <summary>GET /api/connections/{id}/tree – schema explorer tree</summary>
        [HttpGet("{id:int}/tree")]
        public async Task<IActionResult> GetTree(int id)
        {
            try
            {
                var tree = await _connMgr.GetSchemaTreeAsync(id);
                return Ok(tree);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Schema tree failed for connection {Id}", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>GET /api/connections/{id}/definition?name=X&type=procedure – load object definition</summary>
        [HttpGet("{id:int}/definition")]
        public async Task<IActionResult> GetDefinition(int id, [FromQuery] string name, [FromQuery] string type = "procedure")
        {
            var def = await _connMgr.GetObjectDefinitionAsync(id, name, type);
            if (def is null) return NotFound(new { error = $"No definition found for '{name}'." });
            return Ok(new { objectName = name, objectType = type, definition = def });
        }
    }

    public class TestStringRequest
    {
        public string ConnectionString { get; set; } = "";
        public string DatabaseType     { get; set; } = "SqlServer";
    }

    // ── Object Lookup Controller (dynamic object list by type) ─
    [ApiController]
    [Route("api/objects")]
    public class ObjectLookupController : ControllerBase
    {
        private readonly IDependencyValidationService _valSvc;
        private readonly IBackupVersioningService     _backupSvc;

        public ObjectLookupController(
            IDependencyValidationService valSvc,
            IBackupVersioningService backupSvc)
        { _valSvc = valSvc; _backupSvc = backupSvc; }

        /// <summary>
        /// GET /api/objects/list?type=PROCEDURE
        /// Returns list of objects by type from the connected SQL Server.
        /// Used to populate the Object Name dropdown dynamically.
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> ListObjects([FromQuery] string type = "PROCEDURE")
        {
            var sql = type.ToUpperInvariant() switch
            {
                "TABLE"     => "SELECT OBJECT_SCHEMA_NAME(object_id)+'.'+name AS FullName, name AS Name, OBJECT_SCHEMA_NAME(object_id) AS SchemaName FROM sys.tables ORDER BY name;",
                "VIEW"      => "SELECT OBJECT_SCHEMA_NAME(object_id)+'.'+name AS FullName, name AS Name, OBJECT_SCHEMA_NAME(object_id) AS SchemaName FROM sys.views ORDER BY name;",
                "FUNCTION"  => "SELECT OBJECT_SCHEMA_NAME(object_id)+'.'+name AS FullName, name AS Name, OBJECT_SCHEMA_NAME(object_id) AS SchemaName FROM sys.objects WHERE type IN('FN','IF','TF') ORDER BY name;",
                _           => "SELECT OBJECT_SCHEMA_NAME(object_id)+'.'+name AS FullName, name AS Name, OBJECT_SCHEMA_NAME(object_id) AS SchemaName FROM sys.procedures ORDER BY name;",
            };

            var objects = new List<object>();
            try
            {
                using var conn = new Microsoft.Data.SqlClient.SqlConnection(
                    ((Microsoft.Extensions.Configuration.IConfiguration)HttpContext.RequestServices
                        .GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))!)
                        .GetConnectionString("SqlServer"));
                await conn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                await using var r   = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    objects.Add(new
                    {
                        fullName   = r["FullName"].ToString(),
                        name       = r["Name"].ToString(),
                        schemaName = r["SchemaName"].ToString(),
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }

            return Ok(new { type, count = objects.Count, objects });
        }

        /// <summary>
        /// GET /api/objects/definition?name=usp_GetOrders
        /// Returns the SQL definition of an object.
        /// </summary>
        [HttpGet("definition")]
        public async Task<IActionResult> GetDefinition([FromQuery] string name)
        {
            var def = await _backupSvc.GetCurrentDefinitionAsync(name);
            if (def is null) return NotFound(new { error = $"No definition found for '{name}'." });
            return Ok(new { objectName = name, definition = def });
        }

        /// <summary>
        /// GET /api/objects/exists?name=usp_GetOrders
        /// Quick existence check.
        /// </summary>
        [HttpGet("exists")]
        public async Task<IActionResult> Exists([FromQuery] string name)
        {
            var exists = await _valSvc.ObjectExistsAsync(name);
            return Ok(new { name, exists });
        }
    }
}
