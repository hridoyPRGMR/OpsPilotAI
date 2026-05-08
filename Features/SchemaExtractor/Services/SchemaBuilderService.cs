using System.Linq;
using System.Text;
using OpsPilotAI.Features.SchemaExtractor.Models;

namespace OpsPilotAI.Features.SchemaExtractor.Services
{
    public class SchemaBuilderService
    {
        public string BuildSemanticDocument(TableSchema table, Dictionary<string, List<RelationshipSchema>> graph)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Table: {table.TableName}");

            if (!string.IsNullOrWhiteSpace(table.Description))
            {
                sb.AppendLine($"Description: {table.Description}");
            }

            sb.AppendLine("Columns:");
            foreach (var column in table.Columns)
            {
                var qualifiers = new List<string>();
                if (column.IsPrimaryKey) qualifiers.Add("primary key");
                if (column.IsForeignKey) qualifiers.Add("foreign key");
                if (!column.IsNullable) qualifiers.Add("not null");

                var qualifierText = qualifiers.Count > 0 ? $" ({string.Join(", ", qualifiers)})" : string.Empty;
                sb.AppendLine($"- {column.Name} ({column.DataType}){qualifierText}");
            }

            var relationships = GetTableRelationships(table.TableName, graph).ToList();
            if (relationships.Any())
            {
                sb.AppendLine("Relationships:");
                foreach (var rel in relationships)
                {
                    sb.AppendLine($"- {rel.FromTable}.{rel.FromColumn} -> {rel.ToTable}.{rel.ToColumn}");
                }
            }

            var keywords = BuildBusinessKeywords(table, relationships);
            if (keywords.Any())
            {
                sb.AppendLine("Business Keywords:");
                sb.AppendLine($"- {string.Join(", ", keywords)}");
            }

            return sb.ToString().TrimEnd();
        }

        public IEnumerable<string> BuildSemanticDocuments(IEnumerable<TableSchema> tables, Dictionary<string, List<RelationshipSchema>> graph)
        {
            foreach (var table in tables)
            {
                yield return BuildSemanticDocument(table, graph);
            }
        }

        private static IEnumerable<RelationshipSchema> GetTableRelationships(string tableName, Dictionary<string, List<RelationshipSchema>> graph)
        {
            if (!graph.TryGetValue(tableName, out var edges))
            {
                yield break;
            }

            foreach (var relationship in edges)
            {
                yield return relationship;
            }
        }

        private static IEnumerable<string> BuildBusinessKeywords(TableSchema table, IEnumerable<RelationshipSchema> relationships)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                table.TableName
            };

            foreach (var column in table.Columns)
            {
                var cleanName = column.Name.Replace("_id", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("_", " ");
                if (!string.IsNullOrWhiteSpace(cleanName))
                {
                    keywords.Add(cleanName);
                }
            }

            foreach (var relationship in relationships)
            {
                keywords.Add(relationship.ToTable);
                keywords.Add(relationship.FromTable);
            }

            return keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .OrderBy(k => k)
                .ToList();
        }
    }
}