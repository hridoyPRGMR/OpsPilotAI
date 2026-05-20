using Microsoft.Extensions.Caching.Memory;
using OpsPilotAI.Features.Schema.Models;

namespace OpsPilotAI.Features.Schema.Services;

/// <summary>
/// Builds a bidirectional relationship graph from explicit FK constraints plus inferred
/// relationships (columns ending in _id pointing to matching table names).
///
/// Results are cached for 10 minutes because schema changes require a restart anyway.
/// Registered as Scoped (IMemoryCache is Singleton, so this is fine).
/// </summary>
public sealed class RelationshipGraphService(
    ISchemaExtractorService schemaExtractor,
    IMemoryCache cache,
    ILogger<RelationshipGraphService> logger) : IRelationshipGraphService
{
    private const string CacheKey = "schema_relationship_graph";

    public Task<IReadOnlyDictionary<string, IReadOnlyList<RelationshipSchema>>> GetGraphAsync(
        CancellationToken cancellationToken = default)
    {
        return cache.GetOrCreateAsync<IReadOnlyDictionary<string, IReadOnlyList<RelationshipSchema>>>(
            CacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                logger.LogInformation("Rebuilding relationship graph from PostgreSQL metadata.");
                return await BuildGraphAsync(cancellationToken);
            })!;
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<RelationshipSchema>>> BuildGraphAsync(
        CancellationToken cancellationToken)
    {
        var schema = await schemaExtractor.ExtractSchemaAsync(cancellationToken);
        var relationships = await schemaExtractor.GetRelationshipsAsync(cancellationToken);

        var graph = schema.ToDictionary(
            t => t.TableName,
            _ => (IReadOnlyList<RelationshipSchema>)new List<RelationshipSchema>(),
            StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Explicit FK constraints (forward + reverse)
        foreach (var rel in relationships)
        {
            AddEdge(graph, seen, rel);
            AddEdge(graph, seen, rel with
            {
                FromTable  = rel.ToTable,
                FromColumn = rel.ToColumn,
                ToTable    = rel.FromTable,
                ToColumn   = rel.FromColumn
            });
        }

        // Inferred relationships from _id column naming convention
        var columnsByTable = schema.ToDictionary(
            t => t.TableName,
            t => t.Columns,
            StringComparer.OrdinalIgnoreCase);

        var tableNames = schema.Select(t => t.TableName).ToList();

        foreach (var table in schema)
        {
            foreach (var col in table.Columns.Where(c =>
                c.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase)))
            {
                // Skip if an explicit FK already covers this column
                if (relationships.Any(r =>
                    r.FromTable.Equals(table.TableName, StringComparison.OrdinalIgnoreCase) &&
                    r.FromColumn.Equals(col.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var target = InferTableName(col.Name, tableNames);
                if (target is null || !columnsByTable.TryGetValue(target, out var targetCols))
                    continue;

                var pk = targetCols.FirstOrDefault(c => c.IsPrimaryKey)?.Name ?? "id";
                var inferred = new RelationshipSchema
                {
                    FromTable  = table.TableName,
                    FromColumn = col.Name,
                    ToTable    = target,
                    ToColumn   = pk,
                    IsInferred = true
                };

                AddEdge(graph, seen, inferred);
                AddEdge(graph, seen, inferred with
                {
                    FromTable  = inferred.ToTable,
                    FromColumn = inferred.ToColumn,
                    ToTable    = inferred.FromTable,
                    ToColumn   = inferred.FromColumn
                });
            }
        }

        return graph;
    }

    private static void AddEdge(
        Dictionary<string, IReadOnlyList<RelationshipSchema>> graph,
        HashSet<string> seen,
        RelationshipSchema rel)
    {
        var key = $"{rel.FromTable}.{rel.FromColumn}->{rel.ToTable}.{rel.ToColumn}";
        if (!seen.Add(key)) return;

        if (!graph.ContainsKey(rel.FromTable))
            graph[rel.FromTable] = new List<RelationshipSchema>();

        ((List<RelationshipSchema>)graph[rel.FromTable]).Add(rel);
    }

    private static string? InferTableName(string fkColumn, IReadOnlyList<string> tables)
    {
        var baseName = fkColumn[..^3]; // strip "_id"
        string[] candidates = [baseName, baseName + "s", baseName + "es", baseName + "ies"];

        return tables.FirstOrDefault(t =>
            candidates.Any(c => t.Equals(c, StringComparison.OrdinalIgnoreCase)));
    }
}
