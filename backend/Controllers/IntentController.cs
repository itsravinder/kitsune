// ============================================================
// KITSUNE – Query Intent Controller
// POST /api/intent  – detect mode, confidence, tabs to show
// ============================================================
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Kitsune.Backend.Services;

namespace Kitsune.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IntentController : ControllerBase
    {
        private readonly IQueryIntentService _intent;
        public IntentController(IQueryIntentService intent) => _intent = intent;

        /// <summary>
        /// POST /api/intent
        /// Analyzes a SQL query and returns mode (Read/Write), confidence score,
        /// risk level, syntax errors, and which tabs to show in the UI.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Analyze([FromBody] IntentRequest req)
        {
            var result = await _intent.AnalyzeAsync(req);
            return Ok(result);
        }

        /// <summary>
        /// POST /api/intent/heuristic
        /// Fast client-side-compatible analysis (no DB round trip).
        /// </summary>
        [HttpPost("heuristic")]
        public IActionResult Heuristic([FromBody] IntentRequest req)
        {
            var result = _intent.AnalyzeHeuristic(req.Sql ?? "");
            return Ok(result);
        }
    }
}
