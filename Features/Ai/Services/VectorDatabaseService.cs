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

                var upsertSql = $"""
                    INSERT INTO {TableName} (id, table_name, schema_text, embedding, metadata)
                    VALUES (@Id, @TableName, @SchemaText, @Embedding, @Metadata)
                    ON CONFLICT (id) DO UPDATE SET
                        table_name = EXCLUDED.table_name,
                        schema_text = EXCLUDED.schema_text,
                        embedding = EXCLUDED.embedding,
                        metadata = EXCLUDED.metadata;
                    """;

                var embeddingText = string.Join(",", embedding.Vector);

                var result = await connection.ExecuteAsync(
                    upsertSql,
                    new
                    {
                        Id = pointId,
                        TableName = embedding.TableName,
                        SchemaText = embedding.SchemaText,
                        Embedding = embeddingText,
                        Metadata = System.Text.Json.JsonSerializer.Serialize(embedding.Metadata)
                    });

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting embedding");
                return false;
            }
        }

        public async Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, int topK = 5)
        {
            try
            {
                await using var connection = await _dataSource.OpenConnectionAsync();

                var queryVectorText = string.Join(",", queryVector);

                var searchSql = $"""
                    SELECT 
                        table_name,
                        schema_text,
                        1 - (embedding <=> '[{queryVectorText}]'::vector) AS score
                    FROM {TableName}
                    ORDER BY embedding <=> '[{queryVectorText}]'::vector
                    LIMIT @TopK;
                    """;

                var results = await connection.QueryAsync<VectorSearchResult>(
                    searchSql,
                    new { TopK = topK });

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching vectors");
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

