using OpsPilotAI.Features.VectorStore.Models;

namespace OpsPilotAI.Features.Query.Services;

public interface IPromptBuilderService
{
    string BuildSqlGenerationPrompt(string question, IReadOnlyList<VectorSearchResult> relevantSchema);
}
