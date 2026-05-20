namespace OpsPilotAI.Infrastructure.AI;

/// <summary>
/// Abstraction over any text embedding backend.
/// Decoupled from llama.cpp specifics so the vector pipeline can work with any embedding provider.
/// </summary>
public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
