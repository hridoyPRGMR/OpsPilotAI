using Microsoft.Extensions.Caching.Memory;
using OpsPilotAI.Features.SchemaExtractor.Models;

namespace OpsPilotAI.Features.SchemaExtractor.Services
{
    public class RelationshipGraphService
    {
        private const string GraphCacheKey = "RelationshipGraph";

        private readonly SchemaExtractorService _schemaExtractor;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RelationshipGraphService> _logger;

        public RelationshipGraphService(
            SchemaExtractorService schemaExtractor,
            IMemoryCache cache,
            ILogger<RelationshipGraphService> logger)
        {
            _schemaExtractor = schemaExtractor;
            _cache = cache;
            _logger = logger;
        }

        public Task<Dictionary<string, List<RelationshipSchema>>> GetGraphAsync()
        {
            return _cache.GetOrCreateAsync<Dictionary<string, List<RelationshipSchema>>>(GraphCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                _logger.LogInformation("Building schema relationship graph from PostgreSQL metadata.");

                var tables = await _schemaExtractor.GetTablesAsync();
                var relationships = await _schemaExtractor.GetRelationshipsAsync();
                var columnsByTable = new Dictionary<string, List<ColumnSchema>>(StringComparer.OrdinalIgnoreCase);

                foreach (var table in tables)
                {
                    columnsByTable[table] = await _schemaExtractor.GetColumnsAsync(table);
                }

                return BuildGraph(tables, relationships, columnsByTable);
            })!;
        }

        private Dictionary<string, List<RelationshipSchema>> BuildGraph(
            List<string> tables,
            List<RelationshipSchema> relationships,
            Dictionary<string, List<ColumnSchema>> columnsByTable)
        {
            var graph = tables
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(table => table,
                    _ => new List<RelationshipSchema>(),
                    StringComparer.OrdinalIgnoreCase);

            var relationshipSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var relationship in relationships)
            {
                if (!graph.ContainsKey(relationship.FromTable))
                {
                    graph[relationship.FromTable] = new List<RelationshipSchema>();
                }

                if (!graph.ContainsKey(relationship.ToTable))
                {
                    graph[relationship.ToTable] = new List<RelationshipSchema>();
                }

                var forwardKey = RelationshipKey(relationship.FromTable, relationship.FromColumn, relationship.ToTable, relationship.ToColumn);
                if (relationshipSet.Add(forwardKey))
                {
                    graph[relationship.FromTable].Add(relationship);
                }

                var reverse = new RelationshipSchema
                {
                    FromTable = relationship.ToTable,
                    FromColumn = relationship.ToColumn,
                    ToTable = relationship.FromTable,
                    ToColumn = relationship.FromColumn,
                    IsInferred = false
                };

                var reverseKey = RelationshipKey(reverse.FromTable, reverse.FromColumn, reverse.ToTable, reverse.ToColumn);
                if (relationshipSet.Add(reverseKey))
                {
                    graph[reverse.FromTable].Add(reverse);
                }
            }

            foreach (var table in tables)
            {
                if (!columnsByTable.TryGetValue(table, out var columns))
                {
                    continue;
                }

                foreach (var column in columns.Where(c => c.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase)))
                {
                    if (relationships.Any(r => r.FromTable.Equals(table, StringComparison.OrdinalIgnoreCase)
                        && r.FromColumn.Equals(column.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var inferredTarget = InferTableName(column.Name, tables);
                    if (inferredTarget == null || !columnsByTable.TryGetValue(inferredTarget, out var targetColumns))
                    {
                        continue;
                    }

                    var targetPrimaryKey = targetColumns.FirstOrDefault(c => c.IsPrimaryKey)?.Name ?? "id";
                    var inferred = new RelationshipSchema
                    {
                        FromTable = table,
                        FromColumn = column.Name,
                        ToTable = inferredTarget,
                        ToColumn = targetPrimaryKey,
                        IsInferred = true
                    };

                    var inferredKey = RelationshipKey(inferred.FromTable, inferred.FromColumn, inferred.ToTable, inferred.ToColumn);
                    if (relationshipSet.Add(inferredKey))
                    {
                        graph[table].Add(inferred);
                    }

                    var reverseInferred = new RelationshipSchema
                    {
                        FromTable = inferredTarget,
                        FromColumn = targetPrimaryKey,
                        ToTable = table,
                        ToColumn = column.Name,
                        IsInferred = true
                    };

                    var reverseInferredKey = RelationshipKey(reverseInferred.FromTable, reverseInferred.FromColumn, reverseInferred.ToTable, reverseInferred.ToColumn);
                    if (relationshipSet.Add(reverseInferredKey))
                    {
                        graph[inferredTarget].Add(reverseInferred);
                    }
                }
            }

            return graph;
        }

        private static string RelationshipKey(string fromTable, string fromColumn, string toTable, string toColumn)
            => $"{fromTable}.{fromColumn}->{toTable}.{toColumn}";

        private static string? InferTableName(string foreignKeyName, List<string> candidateTables)
        {
            if (!foreignKeyName.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var baseName = foreignKeyName[..^3];
            var candidates = new[]
            {
                baseName,
                baseName + "s",
                baseName + "es",
                baseName + "ies" // e.g. company_id -> companies
            };

            return candidateTables
                .FirstOrDefault(table => candidates.Any(candidate =>
                    table.Equals(candidate, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
