using Microsoft.AspNetCore.Mvc;
using OpsPilotAI.Features.Schema.Services;

namespace OpsPilotAI.Controllers;

/// <summary>
/// Read-only schema inspection endpoints.
/// Renamed from TestController — these are diagnostic, not test-only.
/// Useful during development and for verifying schema extraction is correct.
/// </summary>
[ApiController]
[Route("schema")]
public sealed class SchemaController(
    ISchemaExtractorService schemaExtractor,
    ISchemaBuilderService schemaBuilder,
    IRelationshipGraphService relationshipGraph) : ControllerBase
{
    [HttpGet("tables")]
    public async Task<IActionResult> GetTables(CancellationToken cancellationToken)
    {
        var schema = await schemaExtractor.ExtractSchemaAsync(cancellationToken);
        return Ok(schema.Select(t => t.TableName));
    }

    [HttpGet("columns/{table}")]
    public async Task<IActionResult> GetColumns(string table, CancellationToken cancellationToken)
    {
        var schema = await schemaExtractor.ExtractSchemaAsync(cancellationToken);
        var tableSchema = schema.FirstOrDefault(t =>
            t.TableName.Equals(table, StringComparison.OrdinalIgnoreCase));

        if (tableSchema is null)
            return NotFound(new { error = $"Table '{table}' not found." });

        return Ok(tableSchema.Columns);
    }

    [HttpGet("relationships")]
    public async Task<IActionResult> GetRelationships(CancellationToken cancellationToken)
    {
        var relationships = await schemaExtractor.GetRelationshipsAsync(cancellationToken);
        return Ok(relationships);
    }

    [HttpGet("full")]
    public async Task<IActionResult> GetFullSchema(CancellationToken cancellationToken)
    {
        var schema = await schemaExtractor.ExtractSchemaAsync(cancellationToken);
        return Ok(schema);
    }

    [HttpGet("semantic")]
    public async Task<IActionResult> GetSemanticDocuments(CancellationToken cancellationToken)
    {
        var schema = await schemaExtractor.ExtractSchemaAsync(cancellationToken);
        var graph  = await relationshipGraph.GetGraphAsync(cancellationToken);
        var docs   = schemaBuilder.BuildSemanticDocuments(schema, graph);
        return Ok(docs);
    }

    [HttpGet("graph")]
    public async Task<IActionResult> GetGraph(CancellationToken cancellationToken)
    {
        var graph = await relationshipGraph.GetGraphAsync(cancellationToken);
        return Ok(graph);
    }
}
