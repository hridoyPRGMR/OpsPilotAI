using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Features.Ai.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiService> _logger;
        private readonly string _model;
        private readonly string _sqlBaseUrl;
        private readonly string _sqlEndpointPath;

        public AiService(HttpClient httpClient, ILogger<AiService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;

            var llamaSection = configuration.GetSection("Llama");
            _model = llamaSection.GetValue<string>("SqlModel") ?? throw new InvalidOperationException("Llama SQL model is not configured.");
            _sqlBaseUrl = llamaSection.GetValue<string>("SqlBaseUrl") ?? throw new InvalidOperationException("Llama SQL base URL is not configured.");
            _sqlEndpointPath = llamaSection.GetValue<string>("SqlEndpointPath") ?? "sql";
        }

        public async Task<string> GenerateSqlAsync(string prompt)
        {
            try
            {
                var request = new
                {
                    model = _model,
                    prompt = prompt,
                    stream = false,
                    temperature = 0.3,
                    top_p = 0.9,
                    num_predict = 500
                };

                _logger.LogInformation("Calling llama.cpp for SQL generation against {Endpoint}", GetSqlEndpointUrl());

                var response = await _httpClient.PostAsJsonAsync(
                    GetSqlEndpointUrl(),
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("llama.cpp request failed for {Endpoint}: {StatusCode}", GetSqlEndpointUrl(), response.StatusCode);
                    return string.Empty;
                }

                var responseText = await response.Content.ReadAsStringAsync();
                var generatedText = ParseGenerateResponse(responseText);

                if (string.IsNullOrWhiteSpace(generatedText))
                {
                    _logger.LogError("Empty or unexpected response from llama.cpp: {ResponseText}", responseText);
                    return string.Empty;
                }

                _logger.LogInformation("SQL generation complete");
                return generatedText.Trim();
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timed out");
                return string.Empty;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during SQL generation");
                return string.Empty;
            }
        }

        public async Task<string> AskAsync(string prompt)
        {
            return await GenerateSqlAsync(prompt);
        }

        private string GetSqlEndpointUrl()
        {
            var trimmedBaseUrl = _sqlBaseUrl.TrimEnd('/');
            var trimmedPath = _sqlEndpointPath.TrimStart('/');
            return $"{trimmedBaseUrl}/{trimmedPath}";
        }

        private string ParseGenerateResponse(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("response", out var responseElement) && responseElement.ValueKind == JsonValueKind.String)
                    {
                        return responseElement.GetString() ?? string.Empty;
                    }

                    if (root.TryGetProperty("result", out var resultElement) && resultElement.ValueKind == JsonValueKind.String)
                    {
                        return resultElement.GetString() ?? string.Empty;
                    }

                    if (root.TryGetProperty("output", out var outputElement) && outputElement.ValueKind == JsonValueKind.String)
                    {
                        return outputElement.GetString() ?? string.Empty;
                    }

                    if (root.TryGetProperty("choices", out var choicesElement) && choicesElement.ValueKind == JsonValueKind.Array && choicesElement.GetArrayLength() > 0)
                    {
                        var firstChoice = choicesElement[0];
                        if (firstChoice.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                        {
                            return textElement.GetString() ?? string.Empty;
                        }

                        if (firstChoice.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.Object)
                        {
                            if (messageElement.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String)
                            {
                                return contentElement.GetString() ?? string.Empty;
                            }
                        }
                    }
                }

                return root.ValueKind == JsonValueKind.String ? root.GetString() ?? string.Empty : string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse llama.cpp generation response");
                return string.Empty;
            }
        }
    }
}