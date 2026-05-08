namespace OpsPilotAI.Features.SchemaExtractor.Dtos
{
    public class ColumnQueryResult
    {
        public string Column_Name { get; set; } = default!;
        public string Data_Type { get; set; } = default!;
        public string Is_Nullable { get; set; } = default!;
        public bool Is_Primary_Key { get; set; }
        public bool Is_Foreign_Key { get; set; }
    }
}