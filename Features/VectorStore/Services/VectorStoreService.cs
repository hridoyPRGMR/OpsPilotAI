using System.Globalization;
using System.Text.Json;
using Dapper;
using Npgsql;
using OpsPilotAI.Features.VectorStore.Models;

namespace OpsPilotAI.Features.VectorStore.Services;

/// <summary>
/// pgvector-backed implementation of IVectorStoreService.
///
/// Key improvements over the original VectorDatabaseService:
///   - EnsureCollectionAsync replaces InitializeCollectionAsync (cleaner name, idempotent)
///   - UpsertAsync throws on failure instead of returning bool
///   - CancellationToken propagated throughout
///   - Parameterized table-existence check (was string-interpolated, bad habit even for consts)
///   - Result types extracted to VectorSearchResult record (no more nested classes)
///   - Vector serialization extracted to a shared helper
/// </summary>
public sealed class VectorStoreService(
    NpgsqlDataSource dataSource,
    ILogger<VectorStoreService> logger) : IVectorStoreService
{
    private const string Table = "schema_embeddings";

    public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        const string ddl = $"""
            CREATE EXTENSION IF NOT EXISTS vector;

            CREATE TABLE IF NOT EXISTS {Table} (
                id          TEXT        PRIMARY KEY,
                table_name  TEXT        NOT NULL,
                schema_text TEXT        NOT NULL,
                embedding   vector(768) NOT NULL,
                metadata    JSONB,
                created_at  TIMESTAMP   DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_schema_embeddings_vector
                ON {Table} USING ivfflat (embedding vector_cosine_ops)
                WITH (lists = 100);

            CREATE INDEX IF NOT EXISTS idx_schema_embeddings_table
                ON {Table} (table_name);
            """;

        await conn.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: cancellationToken));
        logger.LogInformation("Vector store collection '{Table}' is ready.", Table);
    }

    public async Task UpsertAsync(string id, EmbeddingModel embedding,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = $"""
            INSERT INTO {Table} (id, table_name, schema_text, embedding, metadata)
            VALUES (@id, @tableName, @schemaText, @embedding::vector, @metadata::jsonb)
            ON CONFLICT (id) DO UPDATE SET
                table_name  = EXCLUDED.table_name,
                schema_text = EXCLUDED.schema_text,
                embedding   = EXCLUDED.embedding,
                metadata    = EXCLUDED.metadata;
            """;

        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                id,
                tableName  = embedding.TableName,
                schemaText = embedding.SchemaText,
                embedding  = ToVectorLiteral(embedding.Vector),
                metadata   = JsonSerializer.Serialize(embedding.Metadata)
            },
            cancellationToken: cancellationToken));

        logger.LogDebug("Upserted embedding for table '{TableName}' (id={Id})",
            embedding.TableName, id);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = $"""
            SELECT
                table_name  AS "TableName",
                schema_text AS "SchemaText",
                1 - (embedding <=> @queryVector::vector) AS "Score"
            FROM {Table}
            ORDER BY embedding <=> @queryVector::vector
            LIMIT @topK;
            """;

        var results = await conn.QueryAsync<VectorSearchResult>(new CommandDefinition(
            sql,
            new { queryVector = ToVectorLiteral(queryVector), topK },
            cancellationToken: cancellationToken));

        var list = results.ToList();
        logger.LogDebug("Vector search returned {Count} results (topK={TopK})", list.Count, topK);
        return list;
    }

    private static string ToVectorLiteral(float[] vector) =>
        $"[{string.Join(",", vector.Select(v => v.ToString("G", CultureInfo.InvariantCulture)))}]";
}
