using System.Net.Http.Json;
using OpsPilotAI.Features.Ai.Models;

namespace OpsPilotAI.Features.Ai.Services
{
    public class VectorDatabaseService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VectorDatabaseService> _logger;
        private readonly string _qdrantBaseUrl = "http://localhost:6333";
        private readonly string _collectionName = "schema_embeddings";

        public VectorDatabaseService(HttpClient httpClient, ILogger<VectorDatabaseService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> InitializeCollectionAsync()
        {
            try
            {
                var collections = await GetCollectionsAsync();
                if (collections.Contains(_collectionName))
                {
                    _logger.LogInformation("Collection {Name} already exists", _collectionName);
                    return true;
                }

                var request = new
                {
                    vectors = new
                    {
                        size = 768,
                        distance = "Cosine"
                    }
                };

                var response = await _httpClient.PutAsJsonAsync(
                    $"{_qdrantBaseUrl}/collections/{_collectionName}",
                    request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Created collection {Name}", _collectionName);
                    return true;
                }

                _logger.LogError("Failed to create collection: {StatusCode}", response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing collection");
                return false;
            }
        }

        public async Task<bool> UpsertEmbeddingAsync(string pointId, EmbeddingModel embedding)
        {
            try
            {
                var point = new
                {
                    id = pointId,
                    vector = embedding.Vector,
                    payload = new
                    {
                        table_name = embedding.TableName,
                        schema_text = embedding.SchemaText,
                        metadata = embedding.Metadata
                    }
                };

                var request = new { points = new[] { point } };

                var response = await _httpClient.PutAsJsonAsync(
                    $"{_qdrantBaseUrl}/collections/{_collectionName}/points",
                    request);

                return response.IsSuccessStatusCode;
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
                var request = new
                {
                    vector = queryVector,
                    limit = topK,
                    with_payload = true
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_qdrantBaseUrl}/collections/{_collectionName}/points/search",
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Search failed: {StatusCode}", response.StatusCode);
                    return new List<VectorSearchResult>();
                }

                var result = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>();
                return result?.Result?.Select(r => new VectorSearchResult
                {
                    TableName = r.Payload?.table_name ?? string.Empty,
                    SchemaText = r.Payload?.schema_text ?? string.Empty,
                    Score = r.Score
                }).ToList() ?? new List<VectorSearchResult>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching vectors");
                return new List<VectorSearchResult>();
            }
        }

        private async Task<List<string>> GetCollectionsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_qdrantBaseUrl}/collections");
                if (!response.IsSuccessStatusCode)
                {
                    return new List<string>();
                }

                var result = await response.Content.ReadFromJsonAsync<QdrantCollectionsResponse>();
                return result?.Collections?.Select(c => c.name).ToList() ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public class VectorSearchResult
        {
            public string TableName { get; set; } = string.Empty;
            public string SchemaText { get; set; } = string.Empty;
            public float Score { get; set; }
        }

        private class QdrantSearchResponse
        {
            public List<SearchPoint> Result { get; set; } = new();
        }

        private class SearchPoint
        {
            public float Score { get; set; }
            public SearchPayload Payload { get; set; }
        }

        private class SearchPayload
        {
            public string table_name { get; set; }
            public string schema_text { get; set; }
        }

        private class QdrantCollectionsResponse
        {
            public List<CollectionInfo> Collections { get; set; } = new();
        }

        private class CollectionInfo
        {
            public string name { get; set; }
        }
    }
}
