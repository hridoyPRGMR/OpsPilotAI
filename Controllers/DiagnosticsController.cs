using Microsoft.AspNetCore.Mvc;
using OpsPilotAI.Features.Query.Services;

namespace OpsPilotAI.Controllers;

/// <summary>
/// Pipeline diagnostics — exposes each pipeline stage individually for debugging.
/// Replaces AiTestController and AiController.
///
/// These endpoints are NOT feature-flagged but their route prefix clearly marks them
/// as non-production; they can be conditionally registered later when auth is added.
/// </summary>
[ApiController]
[Route("diagnostics")]
public sealed class DiagnosticsController(
    IRetrieverService retriever,
    IPromptBuilderService promptBuilder,
    ISqlValidatorService sqlValidator,
    IQueryExecutionService execution) : ControllerBase
{
    // ── Vector store ────────────────────────────────────────────────────────────

    [HttpPost("populate-vector-db")]
    public async Task<IActionResult> PopulateVectorDb(CancellationToken cancellationToken)
    {
        await retriever.PopulateVectorDatabaseAsync(cancellationToken);
        return Ok(new { message = "Vector database populated successfully." });
    }

    [HttpGet("retrieve")]
    public async Task<IActionResult> Retrieve(
        [FromQuery] string query,
        [FromQuery] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { error = "query parameter is required." });

        var results = await retriever.RetrieveRelevantSchemaAsync(query, topK, cancellationToken);
        return Ok(results);
    }

    // ── Prompt ──────────────────────────────────────────────────────────────────

    [HttpPost("prompt")]
    public async Task<IActionResult> BuildPrompt(
        [FromBody] QuestionRequest request,
        CancellationToken cancellationToken)
    {
        var schema = await retriever.RetrieveRelevantSchemaAsync(request.Question, topK: 5, cancellationToken);
        var prompt = promptBuilder.BuildSqlGenerationPrompt(request.Question, schema);

        return Ok(new
        {
            prompt,
            relevantTables = schema.Select(s => s.TableName)
        });
    }

    // ── Validation ──────────────────────────────────────────────────────────────

    [HttpPost("validate-sql")]
    public IActionResult ValidateSql([FromBody] SqlRequest request)
    {
        var (isValid, message) = sqlValidator.Validate(request.Sql);
        var withLimit = isValid ? sqlValidator.EnsureLimit(request.Sql) : request.Sql;

        return Ok(new { isValid, message, sql = withLimit });
    }

    // ── Execution ───────────────────────────────────────────────────────────────

    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] SqlRequest request,
        CancellationToken cancellationToken)
    {
        var (isValid, message) = sqlValidator.Validate(request.Sql);
        if (!isValid)
            return BadRequest(new { error = message });

        var safeSql = sqlValidator.EnsureLimit(request.Sql);
        var result  = await execution.ExecuteAsync(safeSql, cancellationToken);

        return Ok(result);
    }

    // ── Request models ──────────────────────────────────────────────────────────

    public sealed record QuestionRequest(string Question);
    public sealed record SqlRequest(string Sql);
}
