using System.Text.Json;
using OpsPilotAI.Infrastructure.Configuration;

namespace OpsPilotAI.Infrastructure.AI;

/// <summary>
/// llama.cpp implementation of IAiCompletionService.
///
/// Key improvements over the original AiService:
///   - Uses IOptions&lt;LlamaOptions&gt; instead of reading IConfiguration in ctor (fail-fast validation)
///   - CancellationToken propagated through the entire call chain
///   - Throws on failure instead of returning empty strings — callers can't silently ignore errors
///   - Endpoint URL computed once from options (not on every call)
///   - Resilience (retries / timeouts) is applied at the HttpClient registration level in Program.cs,
///     not inside this class, keeping concerns separated
/// </summary>
public sealed class LlamaCompletionService(
    HttpClient httpClient,
    IOptions<LlamaOptions> options,
    ILogger<LlamaCompletionService> logger) : IAiCompletionService
{
    private readonly LlamaOptions _options = options.Value;

    public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
            var request = new
            {
                model = _options.SqlModel,
                prompt,
                stream = _options.CompletionStream,
                temperature = _options.CompletionTemperature,
                top_p = _options.CompletionTopP,
                num_predict = _options.CompletionNumPredict
            };

        logger.LogInformation("Sending completion request to {Endpoint}", _options.SqlEndpointUrl);

        var response = await httpClient.PostAsJsonAsync(_options.SqlEndpointUrl, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("llama.cpp completion failed — {StatusCode}: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"llama.cpp returned {(int)response.StatusCode} {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var text = ExtractTextFromResponse(json);

        if (string.IsNullOrWhiteSpace(text))
        {
            logger.LogWarning("llama.cpp returned an empty completion. Raw response: {Response}", json);
            throw new InvalidOperationException("LLM returned an empty completion response.");
        }

        logger.LogInformation("Completion successful ({Chars} chars)", text.Length);
        return text.Trim();
    }

    private string ExtractTextFromResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // OpenAI-compatible: choices[0].text
            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("text", out var text))
                    return text.GetString() ?? string.Empty;

                if (first.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var content))
                    return content.GetString() ?? string.Empty;
            }

            // Ollama-compatible: response
            if (root.TryGetProperty("response", out var resp))
                return resp.GetString() ?? string.Empty;

            // Generic fallbacks
            foreach (var key in new[] { "result", "output" })
                if (root.TryGetProperty(key, out var val))
                    return val.GetString() ?? string.Empty;

            return string.Empty;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse llama.cpp completion response");
            return string.Empty;
        }
    }
}
