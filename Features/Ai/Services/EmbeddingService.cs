using System.Net.Http.Json;
using OpsPilotAI.Features.Ai.Models;

namespace OpsPilotAI.Features.Ai.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmbeddingService> _logger;
        private readonly string _embeddingModel = "nomic-embed-text";
        private readonly string _ollamaBaseUrl = "http://localhost:11434";

        public EmbeddingService(HttpClient httpClient, ILogger<EmbeddingService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<float[]> EmbedTextAsync(string text)
        {
            try
            {
                var request = new
                {
                    model = _embeddingModel,
                    prompt = text
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_ollamaBaseUrl}/api/embeddings",
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ollama embedding failed: {StatusCode}", response.StatusCode);
                    return Array.Empty<float>();
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
                return result?.Embedding ?? Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error embedding text");
                return Array.Empty<float>();
            }
        }

        private class OllamaEmbeddingResponse
        {
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }
    }
}
