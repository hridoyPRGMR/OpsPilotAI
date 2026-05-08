using System.Text;
using OpsPilotAI.Features.Ai.Services;

namespace OpsPilotAI.Features.Ai.Services
{
    public class PromptBuilderService
    {
        private readonly ILogger<PromptBuilderService> _logger;

        public PromptBuilderService(ILogger<PromptBuilderService> logger)
        {
            _logger = logger;
        }

        public string BuildSqlGenerationPrompt(
            string userQuestion,
            List<VectorDatabaseService.VectorSearchResult> relevantSchema)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are an expert SQL developer for PostgreSQL.");
            sb.AppendLine("Your task is to generate a single, valid SQL query based on the user's question.");
            sb.AppendLine();

            sb.AppendLine("### IMPORTANT RULES ###");
            sb.AppendLine("1. Generate ONLY SELECT queries");
            sb.AppendLine("2. ALWAYS include LIMIT clause (max 100)");
            sb.AppendLine("3. Do NOT generate UPDATE, DELETE, DROP, or ALTER statements");
            sb.AppendLine("4. Return ONLY the SQL query, no explanation");
            sb.AppendLine("5. Use PostgreSQL syntax");
            sb.AppendLine();

            sb.AppendLine("### RELEVANT SCHEMA ###");
            foreach (var schema in relevantSchema.Take(5))
            {
                sb.AppendLine(schema.SchemaText);
                sb.AppendLine();
            }

            sb.AppendLine("### USER QUESTION ###");
            sb.AppendLine(userQuestion);
            sb.AppendLine();

            sb.AppendLine("### RESPONSE ###");
            sb.AppendLine("Generate only the SQL query:");

            _logger.LogInformation("Built SQL generation prompt");

            return sb.ToString();
        }
    }
}
