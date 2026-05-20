using OpsPilotAI.Features.Query.Models;

namespace OpsPilotAI.Features.Query.Services;

public interface IQueryOrchestrationService
{
    Task<TextToSqlResult> ExecuteAsync(string question, CancellationToken cancellationToken = default);
}
