using System.Diagnostics;
using Dapper;
using Npgsql;
using OpsPilotAI.Features.Query.Models;

namespace OpsPilotAI.Features.Query.Services;

/// <summary>
/// Executes validated SQL against PostgreSQL via Dapper.
///
/// Key improvements:
///   - CancellationToken propagated to Dapper via CommandDefinition
///   - Uses Stopwatch.GetTimestamp() for high-resolution timing (avoids DateTime.UtcNow subtraction)
///   - ExecutionResult extracted as a top-level record, not a nested class
/// </summary>
public sealed class QueryExecutionService(
    NpgsqlDataSource dataSource,
    ILogger<QueryExecutionService> logger) : IQueryExecutionService
{
    public async Task<ExecutionResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        var start = Stopwatch.GetTimestamp();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = (await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)))
            .Cast<object>()
            .ToList();

        var elapsed = Stopwatch.GetElapsedTime(start).Milliseconds;

        logger.LogInformation("Query executed in {ElapsedMs}ms — {RowCount} rows returned",
            elapsed, rows.Count);

        return new ExecutionResult
        {
            Success        = true,
            Rows           = rows,
            RowCount       = rows.Count,
            ExecutionTimeMs = elapsed,
            Message        = "Query executed successfully."
        };
    }
}
