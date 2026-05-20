namespace OpsPilotAI.Features.VectorStore.Models;

/// <summary>
/// Represents a single result from a vector similarity search.
/// Extracted from VectorDatabaseService (was previously a nested class) so it can be
/// referenced without creating a dependency on the service implementation.
/// </summary>
public sealed record VectorSearchResult
{
    public required string TableName { get; init; }
    public required string SchemaText { get; init; }
    public float Score { get; init; }
}
