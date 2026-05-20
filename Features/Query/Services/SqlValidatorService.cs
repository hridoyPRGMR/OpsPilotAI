using System.Text.RegularExpressions;

namespace OpsPilotAI.Features.Query.Services;

/// <summary>
/// Validates that generated SQL is a safe, read-only SELECT query.
///
/// Key improvements:
///   - FrozenSet for O(1) lookups on the forbidden keyword list (was a mutable List&lt;string&gt;)
///   - [GeneratedRegex] pre-compiles patterns at build time (.NET 7+ source generator)
///   - Registered as Singleton: stateless, zero allocation per request for the keyword set
///   - EnsureLimit replaces EnsureLimitClause (shorter, consistent naming)
/// </summary>
public sealed partial class SqlValidatorService : ISqlValidatorService
{
    private static readonly System.Collections.Frozen.FrozenSet<string> ForbiddenKeywords =
        System.Collections.Frozen.FrozenSet.ToFrozenSet(
        [
            "DROP", "DELETE", "UPDATE", "INSERT", "ALTER", "TRUNCATE",
            "EXEC", "EXECUTE", "PRAGMA", "VACUUM", "ANALYZE",
            "REINDEX", "GRANT", "REVOKE", "CREATE", "MERGE"
        ],
        StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\bLIMIT\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LimitRegex();

    [GeneratedRegex(@"\b(?:DROP|DELETE|UPDATE|INSERT|ALTER|TRUNCATE|EXEC(?:UTE)?|PRAGMA|VACUUM|ANALYZE|REINDEX|GRANT|REVOKE|CREATE|MERGE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ForbiddenRegex();

    public (bool IsValid, string Message) Validate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return (false, "SQL query is empty.");

        sql = sql.Trim();

        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return (false, "Only SELECT queries are permitted.");

        if (ForbiddenRegex().IsMatch(sql))
            return (false, "Query contains a forbidden keyword.");

        return (true, "Valid");
    }

    public string EnsureLimit(string sql, int limit = 100)
    {
        if (LimitRegex().IsMatch(sql))
            return sql;

        return $"{sql.TrimEnd(';', ' ')} LIMIT {limit};";
    }
}
