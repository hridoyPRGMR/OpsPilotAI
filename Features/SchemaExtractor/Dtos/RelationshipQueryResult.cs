namespace OpsPilotAI.Features.SchemaExtractor.Dtos
{
    public class RelationshipQueryResult
    {
        public string From_Table { get; set; } = default!;
        public string From_Column { get; set; } = default!;
        public string To_Table { get; set; } = default!;
        public string To_Column { get; set; } = default!;
    }
}