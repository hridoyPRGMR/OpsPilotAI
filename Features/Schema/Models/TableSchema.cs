namespace OpsPilotAI.Features.Schema.Models;

public sealed record TableSchema
{
    public required string TableName { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<ColumnSchema> Columns { get; init; } = [];
    public IReadOnlyList<RelationshipSchema> Relationships { get; init; } = [];
}
