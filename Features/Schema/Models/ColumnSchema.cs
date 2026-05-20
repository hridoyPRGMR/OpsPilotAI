namespace OpsPilotAI.Features.Schema.Models;

public sealed record ColumnSchema
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public bool IsNullable { get; init; }
    public bool IsPrimaryKey { get; init; }
    public bool IsForeignKey { get; init; }
}
