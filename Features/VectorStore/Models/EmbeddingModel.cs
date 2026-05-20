namespace OpsPilotAI.Features.VectorStore.Models;

public sealed record EmbeddingModel
{
    public required string TableName { get; init; }
    public required string SchemaText { get; init; }
    public required float[] Vector { get; init; }
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}
