namespace OpsPilotAI.Infrastructure.AI;

/// <summary>
/// Abstraction over any LLM completion backend (llama.cpp, OpenAI, Azure OpenAI, etc.).
/// Keeping this interface narrow means swapping backends requires only a new implementation,
/// not changes to the query pipeline.
/// </summary>
public interface IAiCompletionService
{
    Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
}
