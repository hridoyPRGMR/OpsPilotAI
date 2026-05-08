using Dapper;
using Npgsql;

namespace OpsPilotAI.Features.Ai.Services
{
    public class ExecutionService
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly ILogger<ExecutionService> _logger;

        public ExecutionService(NpgsqlDataSource dataSource, ILogger<ExecutionService> logger)
        {
            _dataSource = dataSource;
            _logger = logger;
        }

        public async Task<ExecutionResult> ExecuteQueryAsync(string sql)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                await using var connection = await _dataSource.OpenConnectionAsync();

                var result = await connection.QueryAsync<dynamic>(sql);
                sw.Stop();

                var rows = result.ToList();

                _logger.LogInformation("Query executed in {Elapsed}ms, returned {RowCount} rows",
                    sw.ElapsedMilliseconds, rows.Count);

                return new ExecutionResult
                {
                    Success = true,
                    Rows = rows.Cast<object>().ToList(),
                    RowCount = rows.Count,
                    ExecutionTimeMs = sw.ElapsedMilliseconds,
                    Message = "Query executed successfully"
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Query execution failed");

                return new ExecutionResult
                {
                    Success = false,
                    Rows = new List<object>(),
                    RowCount = 0,
                    ExecutionTimeMs = sw.ElapsedMilliseconds,
                    Message = $"Execution error: {ex.Message}"
                };
            }
        }

        public class ExecutionResult
        {
            public bool Success { get; set; }
            public List<object> Rows { get; set; } = new();
            public int RowCount { get; set; }
            public long ExecutionTimeMs { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}
