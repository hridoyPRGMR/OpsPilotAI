using OpsPilotAI.Features.Query.Models;

namespace OpsPilotAI.Features.Query.Services;

public interface IQueryExecutionService
{
    Task<ExecutionResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default);
}
