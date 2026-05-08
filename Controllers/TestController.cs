using Microsoft.AspNetCore.Mvc;
using OpsPilotAI.Features.SchemaExtractor.Services;
using OpsPilotAI.Features.Ai.Services;
using Features.Ai.Services;

namespace OpsPilotAI.Controllers
{
    [ApiController, Route("test")]
    public class TestController(
        SchemaExtractorService _schemaExtractor,
        SchemaBuilderService _schemaBuilder,
        RelationshipGraphService _relationshipGraph
        ) : ControllerBase
    {

        [HttpGet("tables")]
        public async Task<IActionResult> TestTables()
        {
            var tables = await _schemaExtractor.GetTablesAsync();
            return Ok(tables);
        }

        [HttpGet("columns/{table}")]
        public async Task<IActionResult> TestColumns(string table)
        {
            var columns = await _schemaExtractor.GetColumnsAsync(table);
            return Ok(columns);
        }

        [HttpGet("relationships")]
        public async Task<IActionResult> TestRelations()
        {
            var relations = await _schemaExtractor.GetRelationshipsAsync();
            return Ok(relations);
        }

        [HttpGet("schema")]
        public async Task<IActionResult> TestSchema()
        {
            var schema = await _schemaExtractor.ExtractSchemaAsync();
            return Ok(schema);
        }

        [HttpGet("semantic")]
        public async Task<IActionResult> TestSemantic()
        {
            var schema = await _schemaExtractor.ExtractSchemaAsync();
            var graph = await _relationshipGraph.GetGraphAsync();

            var docs = _schemaBuilder.BuildSemanticDocuments(schema, graph);

            return Ok(docs);
        }

        [HttpGet("graph")]
        public async Task<IActionResult> TestGraph()
        {
            var graph = await _relationshipGraph.GetGraphAsync();
            return Ok(graph);
        }
    }
}