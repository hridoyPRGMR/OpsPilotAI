using OpsPilotAI.Features.Schema.Models;

namespace OpsPilotAI.Features.Schema.Services;

public interface ISchemaExtractorService
{
    Task<IReadOnlyList<TableSchema>> ExtractSchemaAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RelationshipSchema>> GetRelationshipsAsync(CancellationToken cancellationToken = default);
}
