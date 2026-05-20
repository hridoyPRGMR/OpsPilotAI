using Dapper;
using Npgsql;
using OpsPilotAI.Features.Schema.Dtos;
using OpsPilotAI.Features.Schema.Models;

namespace OpsPilotAI.Features.Schema.Services;

/// <summary>
/// Extracts PostgreSQL schema metadata using a single batched query per operation.
///
/// Key improvement over the original:
///   The original ExtractSchemaAsync made 1 + N database round-trips (one for tables,
///   then one per table for columns). A 50-table database = 51 queries at startup.
///   This implementation fetches all tables and all columns in a single JOIN query,
///   then groups in memory — 2 total queries regardless of schema size.
/// </summary>
public sealed class SchemaExtractorService(NpgsqlDataSource dataSource) : ISchemaExtractorService
{
    public async Task<IReadOnlyList<TableSchema>> ExtractSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                t.table_name,
                c.column_name,
                c.data_type,
                c.is_nullable,
                EXISTS (
                    SELECT 1 FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu
                        ON tc.constraint_name = kcu.constraint_name
                       AND tc.table_schema  = kcu.table_schema
                    WHERE tc.table_schema  = c.table_schema
                      AND tc.table_name    = c.table_name
                      AND tc.constraint_type = 'PRIMARY KEY'
                      AND kcu.column_name  = c.column_name
                ) AS is_primary_key,
                EXISTS (
                    SELECT 1 FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu
                        ON tc.constraint_name = kcu.constraint_name
                       AND tc.table_schema  = kcu.table_schema
                    WHERE tc.table_schema  = c.table_schema
                      AND tc.table_name    = c.table_name
                      AND tc.constraint_type = 'FOREIGN KEY'
                      AND kcu.column_name  = c.column_name
                ) AS is_foreign_key
            FROM information_schema.tables t
            LEFT JOIN information_schema.columns c
                ON t.table_name  = c.table_name
               AND t.table_schema = c.table_schema
            WHERE t.table_schema = 'public'
              AND t.table_type   = 'BASE TABLE'
            ORDER BY t.table_name, c.ordinal_position;
            """;

        var rows = await connection.QueryAsync<TableColumnQueryResult>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows
            .GroupBy(r => r.Table_Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TableSchema
            {
                TableName = g.Key,
                Columns = g
                    .Where(r => r.Column_Name is not null)
                    .Select(r => new ColumnSchema
                    {
                        Name        = r.Column_Name!,
                        DataType    = r.Data_Type!,
                        IsNullable  = r.Is_Nullable == "YES",
                        IsPrimaryKey = r.Is_Primary_Key,
                        IsForeignKey = r.Is_Foreign_Key
                    })
                    .ToList()
            })
            .ToList();
    }

    public async Task<IReadOnlyList<RelationshipSchema>> GetRelationshipsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                tc.table_name  AS from_table,
                kcu.column_name AS from_column,
                ccu.table_name  AS to_table,
                ccu.column_name AS to_column
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
            JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name = tc.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY';
            """;

        var rows = await connection.QueryAsync<RelationshipQueryResult>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.Select(r => new RelationshipSchema
        {
            FromTable  = r.From_Table,
            FromColumn = r.From_Column,
            ToTable    = r.To_Table,
            ToColumn   = r.To_Column
        }).ToList();
    }
}
