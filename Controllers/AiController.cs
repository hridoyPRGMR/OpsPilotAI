using Features.Ai.Services;
using Microsoft.AspNetCore.Mvc;

namespace Controllers
{
    [ApiController]
    [Route("api/ai")]
    public class AiController : ControllerBase
    {
        private readonly AiService _aiService;

        public AiController(AiService aiService)
        {
            _aiService = aiService;
        }

        [HttpGet]
        public async Task<IActionResult> Ask(string prompt)
        {
            var result = await _aiService.AskAsync(prompt);
            Console.WriteLine();
            return Ok(result);
        }
    }
}