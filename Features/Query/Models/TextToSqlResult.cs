namespace OpsPilotAI.Features.Query.Models;

/// <summary>
/// Full pipeline result returned by QueryOrchestrationService.
/// Extracted from the nested class so controllers and tests can reference it directly.
/// </summary>
public sealed record TextToSqlResult
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<string> RelevantTables { get; init; } = [];
    public string GeneratedSql { get; init; } = string.Empty;
    public string ValidatedSql { get; init; } = string.Empty;
    public IReadOnlyList<object> Rows { get; init; } = [];
    public int RowCount { get; init; }
    public long ExecutionTimeMs { get; init; }
}
