namespace OpsPilotAI.Features.SchemaExtractor.Models
{
    public class RelationshipSchema
    {
        public string FromTable { get; set; } = string.Empty;

        public string FromColumn { get; set; } = string.Empty;

        public string ToTable { get; set; } = string.Empty;

        public string ToColumn { get; set; } = string.Empty;
    }
}