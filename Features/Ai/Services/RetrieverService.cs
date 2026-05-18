using Microsoft.Extensions.Caching.Memory;
using OpsPilotAI.Features.SchemaExtractor.Services;

namespace OpsPilotAI.Features.Ai.Services
{
    public class RetrieverService
    {
        private readonly EmbeddingService _embeddingService;
        private readonly VectorDatabaseService _vectorDb;
        private readonly SchemaExtractorService _schemaExtractor;
        private readonly ILogger<RetrieverService> _logger;

        public RetrieverService(
            EmbeddingService embeddingService,
            VectorDatabaseService vectorDb,
            SchemaExtractorService schemaExtractor,
            ILogger<RetrieverService> logger)
        {
            _embeddingService = embeddingService;
            _vectorDb = vectorDb;
            _schemaExtractor = schemaExtractor;
            _logger = logger;
        }

        public async Task<List<VectorDatabaseService.VectorSearchResult>> RetrieveRelevantSchemaAsync(string query, int topK = 5)
        {
            try
            {
                _logger.LogInformation("Retrieving schema for query: {Query}", query);

                var queryVector = await _embeddingService.EmbedTextAsync(query);
                if (queryVector.Length == 0)
                {
                    _logger.LogWarning("Failed to embed query");
                    return new List<VectorDatabaseService.VectorSearchResult>();
                }

                var results = await _vectorDb.SearchAsync(queryVector, topK);
                _logger.LogInformation("Retrieved {Count} relevant tables", results.Count);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving relevant schema");
                return new List<VectorDatabaseService.VectorSearchResult>();
            }
        }

        public async Task<bool> PopulateVectorDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Populating vector database from schema");

                var initialized = await _vectorDb.InitializeCollectionAsync();
                if (!initialized)
                {
                    _logger.LogError("Failed to initialize vector database collection");
                    return false;
                }

                var schema = await _schemaExtractor.ExtractSchemaAsync();
                _logger.LogInformation("Extracted {TableCount} tables from schema", schema.Count);

                int pointId = 1;
                int successCount = 0;
                int failureCount = 0;

                foreach (var table in schema)
                {
                    try
                    {
                        _logger.LogInformation("Processing table {TableName} (ID: {PointId})", table.TableName, pointId);

                        var semanticDoc = BuildSimpleSemanticDoc(table);
                        _logger.LogDebug("Semantic doc for {TableName}: {Doc}", table.TableName, semanticDoc);

                        var vector = await _embeddingService.EmbedTextAsync(semanticDoc);

                        if (vector.Length == 0)
                        {
                            _logger.LogError("Failed to embed table {TableName} - embedding returned empty array", table.TableName);
                            failureCount++;
                            pointId++;
                            continue;
                        }

                        _logger.LogInformation("Successfully embedded {TableName} - vector length: {VectorLength}", table.TableName, vector.Length);

                        var embedding = new OpsPilotAI.Features.Ai.Models.EmbeddingModel
                        {
                            TableName = table.TableName,
                            SchemaText = semanticDoc,
                            Vector = vector,
                            Metadata = new Dictionary<string, object>
                            {
                                { "columns_count", table.Columns.Count },
                                { "relationships_count", table.Relationships.Count }
                            }
                        };

                        await _vectorDb.UpsertEmbeddingAsync(pointId.ToString(), embedding);
                        _logger.LogInformation("Successfully upserted embedding for {TableName}", table.TableName);
                        successCount++;
                    }
                    catch (Exception tableEx)
                    {
                        _logger.LogError(tableEx, "Exception while processing table {TableName}: {Message}", table.TableName, tableEx.Message);
                        failureCount++;
                    }
                    finally
                    {
                        pointId++;
                    }
                }

                _logger.LogInformation("Vector database population complete. Success: {SuccessCount}, Failures: {FailureCount}", successCount, failureCount);
                return failureCount == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error populating vector database: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
                return false;
            }
        }

        private string BuildSimpleSemanticDoc(OpsPilotAI.Features.SchemaExtractor.Models.TableSchema table)
        {
            var doc = $"Table: {table.TableName}\n";
            doc += "Columns: ";
            doc += string.Join(", ", table.Columns.Select(c => c.Name));
            return doc;
        }
    }
}
