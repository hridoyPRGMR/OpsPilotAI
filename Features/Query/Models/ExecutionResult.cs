namespace OpsPilotAI.Features.Query.Models;

/// <summary>
/// Extracted from the nested class inside ExecutionService.
/// Plain record — no service dependency required to reference this type.
/// </summary>
public sealed record ExecutionResult
{
    public required bool Success { get; init; }
    public IReadOnlyList<object> Rows { get; init; } = [];
    public int RowCount { get; init; }
    public long ExecutionTimeMs { get; init; }
    public string Message { get; init; } = string.Empty;
}
