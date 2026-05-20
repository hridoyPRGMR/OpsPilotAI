namespace OpsPilotAI.Features.Schema.Models;

public sealed record RelationshipSchema
{
    public required string FromTable { get; init; }
    public required string FromColumn { get; init; }
    public required string ToTable { get; init; }
    public required string ToColumn { get; init; }
    public bool IsInferred { get; init; }
}
