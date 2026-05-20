using OpsPilotAI.Features.Schema.Models;

namespace OpsPilotAI.Features.Schema.Services;

public interface ISchemaBuilderService
{
    string BuildSemanticDocument(TableSchema table, IReadOnlyDictionary<string, IReadOnlyList<RelationshipSchema>> graph);

    IEnumerable<string> BuildSemanticDocuments(
        IEnumerable<TableSchema> tables,
        IReadOnlyDictionary<string, IReadOnlyList<RelationshipSchema>> graph);
}
