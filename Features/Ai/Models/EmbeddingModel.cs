namespace OpsPilotAI.Features.Ai.Models
{
    public class EmbeddingModel
    {
        public string TableName { get; set; } = string.Empty;
        public string SchemaText { get; set; } = string.Empty;
        public float[] Vector { get; set; } = Array.Empty<float>();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
