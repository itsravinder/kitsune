// ============================================================
// KITSUNE – Query Optimizer Controller
// ============================================================
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Kitsune.Backend.Services;

namespace Kitsune.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OptimizerController : ControllerBase
    {
        private readonly IQueryOptimizerService _opt;
        public OptimizerController(IQueryOptimizerService opt) => _opt = opt;

        /// <summary>POST /api/optimizer/analyze – analyze a query for plan + missing indexes</summary>
        [HttpPost("analyze")]
        public async Task<IActionResult> Analyze([FromBody] OptimizeRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.SqlQuery))
                return BadRequest(new { error = "SqlQuery is required." });
            var result = await _opt.AnalyzeAsync(req);
            return Ok(result);
        }

        /// <summary>GET /api/optimizer/missing-indexes – read DMV for server-wide missing index hints</summary>
        [HttpGet("missing-indexes")]
        public async Task<IActionResult> MissingIndexes()
        {
            var hints = await _opt.GetMissingIndexesFromDmvAsync();
            return Ok(new { count = hints.Count, hints });
        }
    }
}
