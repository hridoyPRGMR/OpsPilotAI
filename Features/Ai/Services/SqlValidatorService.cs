using System.Text.RegularExpressions;

namespace OpsPilotAI.Features.Ai.Services
{
    public class SqlValidatorService
    {
        private readonly ILogger<SqlValidatorService> _logger;
        private readonly List<string> _forbiddenKeywords = new()
        {
            "DROP", "DELETE", "UPDATE", "ALTER", "TRUNCATE", "EXEC", "EXECUTE",
            "PRAGMA", "VACUUM", "ANALYZE", "REINDEX", "GRANT", "REVOKE"
        };

        public SqlValidatorService(ILogger<SqlValidatorService> logger)
        {
            _logger = logger;
        }

        public (bool IsValid, string Message) ValidateSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return (false, "SQL query is empty");
            }

            sql = sql.Trim();

            if (!IsSelectQuery(sql))
            {
                return (false, "Only SELECT queries are allowed");
            }

            if (ContainsForbiddenKeywords(sql))
            {
                return (false, "Query contains forbidden keywords");
            }

            if (!ContainsLimitClause(sql))
            {
                _logger.LogWarning("Query missing LIMIT clause, will auto-add");
            }

            _logger.LogInformation("SQL validation passed");
            return (true, "Valid");
        }

        public string EnsureLimitClause(string sql)
        {
            if (ContainsLimitClause(sql))
            {
                return sql;
            }

            sql = sql.TrimEnd(';').TrimEnd();
            return $"{sql} LIMIT 100;";
        }

        private bool IsSelectQuery(string sql)
        {
            var trimmed = sql.TrimStart();
            return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
        }

        private bool ContainsForbiddenKeywords(string sql)
        {
            return _forbiddenKeywords.Any(keyword =>
                Regex.IsMatch(sql, $@"\b{keyword}\b", RegexOptions.IgnoreCase));
        }

        private bool ContainsLimitClause(string sql)
        {
            return Regex.IsMatch(sql, @"\bLIMIT\b", RegexOptions.IgnoreCase);
        }
    }
}
