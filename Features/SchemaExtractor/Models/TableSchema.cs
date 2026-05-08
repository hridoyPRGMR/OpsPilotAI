namespace OpsPilotAI.Features.SchemaExtractor.Models
{
    public class TableSchema
    {
        public string TableName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public List<ColumnSchema> Columns { get; set; } = new();

        public List<RelationshipSchema> Relationships { get; set; } = new();
    }
}