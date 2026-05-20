namespace OpsPilotAI.Features.Query.Dtos;

/// <summary>
/// The public API response shape for POST /api/query.
/// Kept separate from TextToSqlResult so the internal pipeline model and
/// the public contract can evolve independently.
/// </summary>
public sealed record QueryResponse(
    bool Success,
    string Question,
    string Sql,
    IReadOnlyList<object> Results,
    int RowCount,
    long ExecutionTimeMs,
    string? Error = null);
