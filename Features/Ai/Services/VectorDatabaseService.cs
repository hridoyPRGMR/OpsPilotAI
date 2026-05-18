using Dapper;
using Npgsql;
using OpsPilotAI.Features.Ai.Models;

namespace OpsPilotAI.Features.Ai.Services
{
    public class VectorDatabaseService
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly ILogger<VectorDatabaseService> _logger;
        private const string TableName = "schema_embeddings";

        public VectorDatabaseService(NpgsqlDataSource dataSource, ILogger<VectorDatabaseService> logger)
        {
            _dataSource = dataSource;
            _logger = logger;
        }

        public async Task<bool> InitializeCollectionAsync()
        {
            try
            {
                await using var connection = await _dataSource.OpenConnectionAsync();

                var tableExists = await connection.QueryFirstOrDefaultAsync<int>(
                    $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '{TableName}')");

                if (tableExists == 1)
                {
                    _logger.LogInformation("Table {Name} already exists", TableName);
                    return true;
                }

                var createTableSql = $"""
                    CREATE TABLE IF NOT EXISTS {TableName} (
                        id TEXT PRIMARY KEY,
                        table_name TEXT NOT NULL,
                        schema_text TEXT NOT NULL,
                        embedding vector(768) NOT NULL,
                        metadata JSONB,
                        created_at TIMESTAMP DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS idx_schema_embeddings_embedding 
                        ON {TableName} USING ivfflat (embedding vector_cosine_ops)
                        WITH (lists = 100);
                    """;

                await connection.ExecuteAsync(createTableSql);
                _logger.LogInformation("Created table {Name}", TableName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing table");
                return false;
            }
        }

        public async Task<bool> UpsertEmbeddingAsync(string pointId, EmbeddingModel embedding)
        {
            try
            {
                await using var connection = await _dataSource.OpenConnectionAsync();

                // 1. Keep parameters lowercase and cleanly defined
                var upsertSql = $"""
                    INSERT INTO {TableName} (id, table_name, schema_text, embedding, metadata)
                    VALUES (@id, @table_name, @schema_text, @embedding::vector, @metadata::jsonb)
                    ON CONFLICT (id) DO UPDATE SET
                        table_name = EXCLUDED.table_name,
                        schema_text = EXCLUDED.schema_text,
                        embedding = EXCLUDED.embedding,
                        metadata = EXCLUDED.metadata;
                    """;

                // 2. Let Npgsql handle the float[] native translation if pgvector is registered,
                // or safely pass the float[] array. If your column is strictly expecting text parsing, 
                // keep string.Join but map it to a strictly matching parameter.
                var metadataJson = System.Text.Json.JsonSerializer.Serialize(embedding.Metadata);

                _logger.LogDebug("Upserting embedding for {TableName} with vector length {Length}", embedding.TableName, embedding.Vector.Length);

                // 3. Keep parameter properties perfectly matching the SQL variables
                var result = await connection.ExecuteAsync(
                    upsertSql,
                    new
                    {
                        id = pointId,
                        table_name = embedding.TableName,
                        schema_text = embedding.SchemaText,
                        embedding = embedding.Vector, // Pass the float array directly or use embeddingText
                        metadata = metadataJson
                    });

                // 4. IMPORTANT NOTE ON UPSERTS: 
                // On PostgreSQL, an "UPDATE" that changes nothing due to matching data can return 0 rows affected.
                // If you are verifying a true database write success, check if result >= 0 instead of strict > 0.
                if (result >= 0)
                {
                    _logger.LogInformation("Successfully processed upsert for {TableName} (ID: {PointId}). Rows affected: {Result}", embedding.TableName, pointId, result);
                    return true;
                }

                _logger.LogWarning("Upsert for {TableName} returned unexpected row count: {RowCount}", embedding.TableName, result);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting embedding for {TableName}: {Message}\n{StackTrace}", embedding?.TableName ?? "unknown", ex.Message, ex.StackTrace);
                return false;
            }
        }

        public async Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, int topK = 5)
        {
            try
            {
                await using var connection = await _dataSource.OpenConnectionAsync();

                var queryVectorText = $"[{string.Join(",", queryVector)}]";

                var searchSql = $"""
                    SELECT 
                        table_name,
                        schema_text,
                        1 - (embedding <=> @QueryVector::vector) AS score
                    FROM {TableName}
                    ORDER BY embedding <=> @QueryVector::vector
                    LIMIT @TopK;
                    """;

                _logger.LogDebug("Searching vectors with query vector length {Length}, topK {TopK}", queryVector.Length, topK);

                var results = await connection.QueryAsync<VectorSearchResult>(
                    searchSql,
                    new { QueryVector = queryVectorText, TopK = topK });

                _logger.LogInformation("Vector search returned {ResultCount} results", results.Count());
                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching vectors: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
                return new List<VectorSearchResult>();
            }
        }

        public class VectorSearchResult
        {
            public string TableName { get; set; } = string.Empty;
            public string SchemaText { get; set; } = string.Empty;
            public float Score { get; set; }
        }
    }
}

