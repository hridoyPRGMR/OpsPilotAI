using OpsPilotAI.Features.VectorStore.Models;

namespace OpsPilotAI.Features.Query.Services;

public interface IRetrieverService
{
    Task<IReadOnlyList<VectorSearchResult>> RetrieveRelevantSchemaAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);

    Task PopulateVectorDatabaseAsync(CancellationToken cancellationToken = default);
}
