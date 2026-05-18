using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace OpsPilotAI.Features.Ai.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmbeddingService> _logger;
        private readonly string _embeddingModel;
        private readonly string _embeddingBaseUrl;

        public EmbeddingService(HttpClient httpClient, ILogger<EmbeddingService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;

            var llamaSection = configuration.GetSection("Llama");
            _embeddingModel = llamaSection.GetValue<string>("EmbeddingModel") ?? throw new InvalidOperationException("Llama embedding model is not configured.");
            _embeddingBaseUrl = llamaSection.GetValue<string>("EmbeddingBaseUrl") ?? throw new InvalidOperationException("Llama embedding base URL is not configured.");
        }

        public async Task<float[]> EmbedTextAsync(string text)
        {
            try
            {
                var request = new
                {
                    model = _embeddingModel,
                    input = text
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_embeddingBaseUrl}/embeddings",
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("llama.cpp embedding failed: {StatusCode}", response.StatusCode);
                    return Array.Empty<float>();
                }

                var responseText = await response.Content.ReadAsStringAsync();
                var embedding = ParseEmbeddingResponse(responseText);
                if (embedding.Length == 0)
                {
                    _logger.LogWarning("Unexpected embedding response: {Response}", responseText);
                }

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error embedding text");
                return Array.Empty<float>();
            }
        }

        private float[] ParseEmbeddingResponse(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.String)
                {
                    var innerJson = root.GetString();
                    if (!string.IsNullOrWhiteSpace(innerJson))
                    {
                        return ParseEmbeddingResponse(innerJson);
                    }
                }

                if (root.ValueKind == JsonValueKind.Array)
                {
                    if (root.GetArrayLength() == 0)
                    {
                        return Array.Empty<float>();
                    }

                    // Direct array of numbers
                    if (root[0].ValueKind == JsonValueKind.Number)
                    {
                        return JsonSerializer.Deserialize<float[]>(root.GetRawText()) ?? Array.Empty<float>();
                    }

                    // Array of objects with embedding property
                    var first = root[0];
                    if (first.TryGetProperty("embedding", out var embeddingElement) && embeddingElement.ValueKind == JsonValueKind.Array)
                    {
                        return ExtractFloatArray(embeddingElement);
                    }
                }

                if (root.TryGetProperty("embedding", out var directEmbedding) && directEmbedding.ValueKind == JsonValueKind.Array)
                {
                    return ExtractFloatArray(directEmbedding);
                }

                if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array && dataElement.GetArrayLength() > 0)
                {
                    var first = dataElement[0];
                    if (first.TryGetProperty("embedding", out var firstEmbedding) && firstEmbedding.ValueKind == JsonValueKind.Array)
                    {
                        return ExtractFloatArray(firstEmbedding);
                    }
                }

                if (root.TryGetProperty("result", out var resultElement) && resultElement.ValueKind == JsonValueKind.Array)
                {
                    return ExtractFloatArray(resultElement);
                }

                return Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse embedding response JSON");
                return Array.Empty<float>();
            }
        }

        private float[] ExtractFloatArray(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<float>();
            }

            if (element.GetArrayLength() == 0)
            {
                return Array.Empty<float>();
            }

            // If this is an array of arrays, use the first nested array
            if (element[0].ValueKind == JsonValueKind.Array)
            {
                var inner = element[0];
                return JsonSerializer.Deserialize<float[]>(inner.GetRawText()) ?? Array.Empty<float>();
            }

            return JsonSerializer.Deserialize<float[]>(element.GetRawText()) ?? Array.Empty<float>();
        }
    }
}
