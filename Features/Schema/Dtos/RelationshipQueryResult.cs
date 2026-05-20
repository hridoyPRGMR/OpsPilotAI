namespace OpsPilotAI.Features.Schema.Dtos;

internal sealed class RelationshipQueryResult
{
    public string From_Table { get; set; } = default!;
    public string From_Column { get; set; } = default!;
    public string To_Table { get; set; } = default!;
    public string To_Column { get; set; } = default!;
}
