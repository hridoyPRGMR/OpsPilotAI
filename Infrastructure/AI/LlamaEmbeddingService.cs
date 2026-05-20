using System.Text.Json;
using OpsPilotAI.Infrastructure.Configuration;

namespace OpsPilotAI.Infrastructure.AI;

/// <summary>
/// llama.cpp implementation of IEmbeddingService.
///
/// Key improvements over the original EmbeddingService:
///   - Uses IOptions&lt;LlamaOptions&gt; (fail-fast startup validation)
///   - CancellationToken propagated throughout
///   - Throws on infrastructure failure instead of returning Array.Empty
///   - Supports all common embedding response shapes (OpenAI, Ollama, direct array)
/// </summary>
public sealed class LlamaEmbeddingService(
    HttpClient httpClient,
    IOptions<LlamaOptions> options,
    ILogger<LlamaEmbeddingService> logger) : IEmbeddingService
{
    private readonly LlamaOptions _options = options.Value;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.EmbeddingModel,
            input = text
        };

        var response = await httpClient.PostAsJsonAsync(
            _options.EmbeddingEndpointUrl, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("llama.cpp embedding failed — {StatusCode}: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"llama.cpp embedding returned {(int)response.StatusCode} {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var embedding = ExtractEmbedding(json);

        if (embedding.Length == 0)
        {
            logger.LogWarning("llama.cpp returned an empty embedding vector. Raw: {Raw}", json);
            throw new InvalidOperationException("Embedding service returned an empty vector.");
        }

        logger.LogDebug("Embedding complete — {Dimensions} dimensions", embedding.Length);
        return embedding;
    }

    private float[] ExtractEmbedding(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // OpenAI-compatible: data[0].embedding
            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Array &&
                data.GetArrayLength() > 0 &&
                data[0].TryGetProperty("embedding", out var dataEmb))
                return ToFloatArray(dataEmb);

            // Direct: { "embedding": [...] }
            if (root.TryGetProperty("embedding", out var emb))
                return ToFloatArray(emb);

            // Ollama-compatible array of objects: [{ "embedding": [...] }]
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                if (first.ValueKind == JsonValueKind.Number)
                    return JsonSerializer.Deserialize<float[]>(root.GetRawText()) ?? [];

                if (first.TryGetProperty("embedding", out var arrEmb))
                    return ToFloatArray(arrEmb);
            }

            if (root.TryGetProperty("result", out var result))
                return ToFloatArray(result);

            return [];
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse embedding response");
            return [];
        }
    }

    private static float[] ToFloatArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
            return [];

        // Handle nested array: [[...]]
        if (element[0].ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<float[]>(element[0].GetRawText()) ?? [];

        return JsonSerializer.Deserialize<float[]>(element.GetRawText()) ?? [];
    }
}
