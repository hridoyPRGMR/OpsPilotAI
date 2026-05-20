using System.Text;
using OpsPilotAI.Features.VectorStore.Models;

namespace OpsPilotAI.Features.Query.Services;

/// <summary>
/// Builds structured prompts for the SQL generation LLM.
/// Registered as Singleton: stateless, called on every query.
///
/// Prompt engineering notes:
///   - System instruction block clearly separates rules from schema context
///   - Schema is capped at topK=5 entries (already enforced by retriever)
///   - "Generate only the SQL query:" marker at the end steers the model
///     to output clean SQL without prose, reducing post-processing burden
/// </summary>
public sealed class PromptBuilderService : IPromptBuilderService
{
    private const string SystemInstruction = """
        You are an expert PostgreSQL developer.
        Your task is to generate a single, valid SQL SELECT query that answers the user's question.

        RULES (strictly enforced):
        1. Generate ONLY a SELECT query — never UPDATE, DELETE, INSERT, DROP, ALTER, TRUNCATE, or EXEC.
        2. ALWAYS include a LIMIT clause (maximum 100 rows).
        3. Use only tables and columns that appear in the schema provided below.
        4. Return ONLY the raw SQL query string on a single line. Do NOT include any explanations, markdown, code fences, or whitespace formatting characters like newlines (\n) or carriage returns (\r).
        5. Use standard PostgreSQL syntax.
        """;

    public string BuildSqlGenerationPrompt(string question, IReadOnlyList<VectorSearchResult> relevantSchema)
    {
        var sb = new StringBuilder();

        sb.AppendLine(SystemInstruction);
        sb.AppendLine();
        sb.AppendLine("### RELEVANT SCHEMA ###");

        foreach (var schema in relevantSchema.Take(5))
        {
            sb.AppendLine(schema.SchemaText);
            sb.AppendLine();
        }

        sb.AppendLine("### USER QUESTION ###");
        sb.AppendLine(question);
        sb.AppendLine();
        sb.AppendLine("### RESPONSE ###");
        sb.Append("SQL query:");

        return sb.ToString();
    }
}
