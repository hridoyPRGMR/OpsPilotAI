using System.Text;
using Dapper;
using Npgsql;
using OpsPilotAI.Features.SchemaExtractor.Dtos;
using OpsPilotAI.Features.SchemaExtractor.Models;

namespace OpsPilotAI.Features.SchemaExtractor.Services
{
    public class SchemaExtractorService(NpgsqlDataSource _dataSource)
    {

        public async Task<List<string>> GetTablesAsync()
        {
            await using var connection = await _dataSource.OpenConnectionAsync();

            const string sql = """
                SELECT table_name
                FROM information_schema.tables
                WHERE table_schema = 'public'
                AND table_type = 'BASE TABLE';
            """;

            var tables = await connection.QueryAsync<string>(sql);

            return tables.ToList();
        }

        public async Task<List<ColumnSchema>> GetColumnsAsync(
            string tableName)
        {
            await using var connection = await _dataSource.OpenConnectionAsync();

            const string sql = """
                SELECT
                    c.column_name,
                    c.data_type,
                    c.is_nullable,
                    EXISTS (
                        SELECT 1
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage kcu
                            ON tc.constraint_name = kcu.constraint_name
                            AND tc.table_schema = kcu.table_schema
                        WHERE tc.table_schema = c.table_schema
                        AND tc.table_name = c.table_name
                        AND tc.constraint_type = 'PRIMARY KEY'
                        AND kcu.column_name = c.column_name
                    ) AS is_primary_key,
                    EXISTS (
                        SELECT 1
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage kcu
                            ON tc.constraint_name = kcu.constraint_name
                            AND tc.table_schema = kcu.table_schema
                        WHERE tc.table_schema = c.table_schema
                        AND tc.table_name = c.table_name
                        AND tc.constraint_type = 'FOREIGN KEY'
                        AND kcu.column_name = c.column_name
                    ) AS is_foreign_key
                FROM information_schema.columns c
                WHERE c.table_name = @TableName
                ORDER BY c.ordinal_position;
                """;

            var result = await connection.QueryAsync<ColumnQueryResult>(
                sql,
                new { TableName = tableName });

            return [.. result.Select(x => new ColumnSchema
            {
                Name = x.Column_Name,
                DataType = x.Data_Type,
                IsNullable = x.Is_Nullable == "YES",
                IsPrimaryKey = x.Is_Primary_Key,
                IsForeignKey = x.Is_Foreign_Key
            })];
        }

        public async Task<List<RelationshipSchema>> GetRelationshipsAsync()
        {
            await using var connection = await _dataSource.OpenConnectionAsync();

            const string sql = """
                SELECT
                    tc.table_name AS from_table,
                    kcu.column_name AS from_column,
                    ccu.table_name AS to_table,
                    ccu.column_name AS to_column
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name
                JOIN information_schema.constraint_column_usage ccu
                    ON ccu.constraint_name = tc.constraint_name
                WHERE tc.constraint_type = 'FOREIGN KEY';
                """;

            var result = await connection.QueryAsync<RelationshipQueryResult>(sql);

            return result.Select(x => new RelationshipSchema
            {
                FromTable = x.From_Table,
                FromColumn = x.From_Column,
                ToTable = x.To_Table,
                ToColumn = x.To_Column
            }).ToList();
        }

        public async Task<List<TableSchema>> ExtractSchemaAsync()
        {
            var tables = await GetTablesAsync();

            var relationships = await GetRelationshipsAsync();

            var result = new List<TableSchema>();

            foreach (var table in tables)
            {
                var columns = await GetColumnsAsync(table);

                var tableRelationships = relationships
                    .Where(x =>
                        x.FromTable == table ||
                        x.ToTable == table)
                    .ToList();

                result.Add(new TableSchema
                {
                    TableName = table,
                    Columns = columns,
                    Relationships = tableRelationships
                });
            }

            return result;
        }


        public string BuildSemanticDocument(TableSchema table)
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
                sb.AppendLine(
                    $"- {column.Name} ({column.DataType})");
            }

            sb.AppendLine("Relationships:");

            foreach (var rel in table.Relationships)
            {
                sb.AppendLine(
                    $"{rel.FromTable}.{rel.FromColumn} -> {rel.ToTable}.{rel.ToColumn}");
            }

            return sb.ToString();
        }
    }
}