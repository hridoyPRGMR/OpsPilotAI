using Microsoft.AspNetCore.Mvc;
using OpsPilotAI.Features.SchemaExtractor.Services;

namespace OpsPilotAI.Controllers
{
    [ApiController, Route("api/test")]
    public class TestController(SchemaExtractorService _schemaExtractor) : ControllerBase
    {

        [HttpGet("test/tables")]
        public async Task<IActionResult> TestTables()
        {
            var tables = await _schemaExtractor.GetTablesAsync();
            return Ok(tables);
        }

        [HttpGet("test/columns/{table}")]
        public async Task<IActionResult> TestColumns(string table)
        {
            var columns = await _schemaExtractor.GetColumnsAsync(table);
            return Ok(columns);
        }

        [HttpGet("test/relations")]
        public async Task<IActionResult> TestRelations()
        {
            var relations = await _schemaExtractor.GetRelationshipsAsync();
            return Ok(relations);
        }

        [HttpGet("test/schema")]
        public async Task<IActionResult> TestSchema()
        {
            var schema = await _schemaExtractor.ExtractSchemaAsync();
            return Ok(schema);
        }

        // [HttpGet("test/semantic")]
        // public async Task<IActionResult> TestSemantic()
        // {
        //     var schema = await _schemaExtractor.ExtractSchemaAsync();

        //     var docs = schema.Select(_schemaBuilder.BuildSemanticDocument);

        //     return Ok(docs);
        // }
    }
}