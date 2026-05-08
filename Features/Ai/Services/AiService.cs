namespace Features.Ai.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiService> _logger;
        private readonly string _model = "qwen2.5-coder";
        private readonly string _ollamaBaseUrl = "http://localhost:11434";

        public AiService(HttpClient httpClient, ILogger<AiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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

                _logger.LogInformation("Calling Ollama for SQL generation");

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_ollamaBaseUrl}/api/generate",
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ollama request failed: {StatusCode}", response.StatusCode);
                    return string.Empty;
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();

                if (result?.Response == null)
                {
                    _logger.LogError("Empty response from Ollama");
                    return string.Empty;
                }

                _logger.LogInformation("SQL generation complete");
                return result.Response.Trim();
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

        private class OllamaGenerateResponse
        {
            public string Response { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
        }
    }
}