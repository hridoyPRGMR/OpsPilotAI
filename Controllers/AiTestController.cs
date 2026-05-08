using Microsoft.AspNetCore.Mvc;
using OpsPilotAI.Features.Ai.Services;
using OpsPilotAI.Features.SchemaExtractor.Services;
using Features.Ai.Services;

namespace OpsPilotAI.Controllers
{
    [ApiController, Route("aitest")]
    public class AiTestController(
        RetrieverService _retriever,
        PromptBuilderService _promptBuilder,
        SqlValidatorService _sqlValidator,
        ExecutionService _execution,
        AiService _aiService
        ) : ControllerBase
    {
        [HttpPost("populate-vector-db")]
        public async Task<IActionResult> PopulateVectorDb()
        {
            var success = await _retriever.PopulateVectorDatabaseAsync();
            return Ok(new { success, message = success ? "Vector DB populated" : "Failed to populate vector DB" });
        }

        [HttpGet("retrieve")]
        public async Task<IActionResult> TestRetrieve([FromQuery] string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return BadRequest("Query parameter is required");
            }

            var results = await _retriever.RetrieveRelevantSchemaAsync(query, topK: 5);
            return Ok(results);
        }

        [HttpPost("prompt")]
        public async Task<IActionResult> TestPrompt([FromBody] PromptRequest request)
        {
            if (string.IsNullOrEmpty(request?.Question))
            {
                return BadRequest("Question is required");
            }

            var relevantSchema = await _retriever.RetrieveRelevantSchemaAsync(request.Question, topK: 5);
            var prompt = _promptBuilder.BuildSqlGenerationPrompt(request.Question, relevantSchema);

            return Ok(new { prompt, relevantTables = relevantSchema.Select(s => s.TableName).ToList() });
        }

        [HttpPost("generate-sql")]
        public async Task<IActionResult> TestGenerateSql([FromBody] PromptRequest request)
        {
            if (string.IsNullOrEmpty(request?.Question))
            {
                return BadRequest("Question is required");
            }

            var relevantSchema = await _retriever.RetrieveRelevantSchemaAsync(request.Question, topK: 5);
            var prompt = _promptBuilder.BuildSqlGenerationPrompt(request.Question, relevantSchema);
            var sql = await _aiService.GenerateSqlAsync(prompt);

            return Ok(new { sql, prompt });
        }

        [HttpPost("validate-sql")]
        public async Task<IActionResult> TestValidateSql([FromBody] SqlValidationRequest request)
        {
            if (string.IsNullOrEmpty(request?.Sql))
            {
                return BadRequest("SQL is required");
            }

            var (isValid, message) = _sqlValidator.ValidateSql(request.Sql);
            var validatedSql = isValid ? _sqlValidator.EnsureLimitClause(request.Sql) : request.Sql;

            return Ok(new { isValid, message, validatedSql });
        }

        [HttpPost("execute")]
        public async Task<IActionResult> TestExecute([FromBody] SqlValidationRequest request)
        {
            if (string.IsNullOrEmpty(request?.Sql))
            {
                return BadRequest("SQL is required");
            }

            var (isValid, message) = _sqlValidator.ValidateSql(request.Sql);
            if (!isValid)
            {
                return BadRequest(new { isValid, message });
            }

            var validatedSql = _sqlValidator.EnsureLimitClause(request.Sql);
            var result = await _execution.ExecuteQueryAsync(validatedSql);

            return Ok(result);
        }

        public class PromptRequest
        {
            public string Question { get; set; } = string.Empty;
        }

        public class SqlValidationRequest
        {
            public string Sql { get; set; } = string.Empty;
        }
    }
}
