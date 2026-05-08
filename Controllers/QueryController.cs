using Microsoft.AspNetCore.Mvc;
using OpsPilotAI.Features.Ai.Services;

namespace OpsPilotAI.Controllers
{
    [ApiController, Route("api")]
    public class QueryController(QueryOrchestrationService _orchestration) : ControllerBase
    {
        [HttpPost("query")]
        public async Task<IActionResult> Query([FromBody] QueryRequest request)
        {
            if (string.IsNullOrEmpty(request?.Question))
            {
                return BadRequest(new { error = "Question is required" });
            }

            var result = await _orchestration.ExecuteAsync(request.Question);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        public class QueryRequest
        {
            public string Question { get; set; } = string.Empty;
        }
    }
}
