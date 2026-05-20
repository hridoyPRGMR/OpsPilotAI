using System.Text;
using OpsPilotAI.Features.Schema.Models;

namespace OpsPilotAI.Features.Schema.Services;

/// <summary>
/// Builds rich AI-readable semantic documents from table schemas.
/// Registered as Singleton: holds no mutable state and is called on every populate request.
/// </summary>
public sealed class SchemaBuilderService : ISchemaBuilderService
{
    public string BuildSemanticDocument(
        TableSchema table,
        IReadOnlyDictionary<string, IReadOnlyList<RelationshipSchema>> graph)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Table: {table.TableName}");

        if (!string.IsNullOrWhiteSpace(table.Description))
            sb.AppendLine($"Description: {table.Description}");

        sb.AppendLine("Columns:");
        foreach (var col in table.Columns)
        {
            var qualifiers = BuildQualifiers(col);
            var suffix = qualifiers.Count > 0 ? $" ({string.Join(", ", qualifiers)})" : string.Empty;
            sb.AppendLine($"  - {col.Name} ({col.DataType}){suffix}");
        }

        var relationships = graph.TryGetValue(table.TableName, out var edges)
            ? edges
            : (IReadOnlyList<RelationshipSchema>)[];

        if (relationships.Count > 0)
        {
            sb.AppendLine("Relationships:");
            foreach (var rel in relationships)
                sb.AppendLine($"  - {rel.FromTable}.{rel.FromColumn} -> {rel.ToTable}.{rel.ToColumn}");
        }

        var keywords = BuildKeywords(table, relationships);
        if (keywords.Count > 0)
        {
            sb.AppendLine("Business Keywords:");
            sb.AppendLine($"  - {string.Join(", ", keywords)}");
        }

        return sb.ToString().TrimEnd();
    }

    public IEnumerable<string> BuildSemanticDocuments(
        IEnumerable<TableSchema> tables,
        IReadOnlyDictionary<string, IReadOnlyList<RelationshipSchema>> graph)
    {
        foreach (var table in tables)
            yield return BuildSemanticDocument(table, graph);
    }

    private static List<string> BuildQualifiers(ColumnSchema col)
    {
        var q = new List<string>(3);
        if (col.IsPrimaryKey) q.Add("primary key");
        if (col.IsForeignKey) q.Add("foreign key");
        if (!col.IsNullable) q.Add("not null");
        return q;
    }

    private static List<string> BuildKeywords(
        TableSchema table,
        IReadOnlyList<RelationshipSchema> relationships)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { table.TableName };

        foreach (var col in table.Columns)
        {
            var clean = col.Name
                .Replace("_id", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("_", " ");
            if (!string.IsNullOrWhiteSpace(clean))
                keywords.Add(clean);
        }

        foreach (var rel in relationships)
        {
            keywords.Add(rel.ToTable);
            keywords.Add(rel.FromTable);
        }

        return keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .OrderBy(k => k)
            .ToList();
    }
}
