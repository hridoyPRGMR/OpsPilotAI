using Microsoft.AspNetCore.Mvc;
using OpsPilotAI.Features.Query.Dtos;
using OpsPilotAI.Features.Query.Services;

namespace OpsPilotAI.Controllers;

/// <summary>
/// Main production endpoint.
/// POST /api/query — accepts a natural language question, returns SQL + results.
///
/// PipelineException is caught by GlobalExceptionHandlingMiddleware and returned
/// as 422 Unprocessable Entity, so the controller stays clean.
/// </summary>
[ApiController]
[Route("api")]
public sealed class QueryController(
    IQueryOrchestrationService orchestration,
    ILogger<QueryController> logger) : ControllerBase
{
    [HttpPost("query")]
    [ProducesResponseType<QueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<QueryResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Query(
        [FromBody] QueryRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Query request: {Question}", request.Question);

        var result = await orchestration.ExecuteAsync(request.Question, cancellationToken);

        return Ok(new QueryResponse(
            Success:         result.Success,
            Question:        request.Question,
            Sql:             result.ValidatedSql,
            Results:         result.Rows,
            RowCount:        result.RowCount,
            ExecutionTimeMs: result.ExecutionTimeMs
        ));
    }
}
