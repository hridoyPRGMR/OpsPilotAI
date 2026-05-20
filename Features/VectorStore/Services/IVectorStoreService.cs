using OpsPilotAI.Features.VectorStore.Models;

namespace OpsPilotAI.Features.VectorStore.Services;

public interface IVectorStoreService
{
    Task EnsureCollectionAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(string id, EmbeddingModel embedding, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
