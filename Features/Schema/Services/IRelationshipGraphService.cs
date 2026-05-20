using OpsPilotAI.Features.Schema.Models;

namespace OpsPilotAI.Features.Schema.Services;

public interface IRelationshipGraphService
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<RelationshipSchema>>> GetGraphAsync(
        CancellationToken cancellationToken = default);
}
