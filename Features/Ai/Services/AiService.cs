using Microsoft.Extensions.Configuration;

namespace Features.Ai.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiService> _logger;
        private readonly string _model;
        private readonly string _sqlBaseUrl;

        public AiService(HttpClient httpClient, ILogger<AiService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;

            var llamaSection = configuration.GetSection("Llama");
            _model = llamaSection.GetValue<string>("SqlModel") ?? throw new InvalidOperationException("Llama SQL model is not configured.");
            _sqlBaseUrl = llamaSection.GetValue<string>("SqlBaseUrl") ?? throw new InvalidOperationException("Llama SQL base URL is not configured.");
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

                _logger.LogInformation("Calling llama.cpp for SQL generation");

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_sqlBaseUrl}/sql",
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("llama.cpp request failed: {StatusCode}", response.StatusCode);
                    return string.Empty;
                }

                var result = await response.Content.ReadFromJsonAsync<LlamaGenerateResponse>();

                if (result?.Response == null)
                {
                    _logger.LogError("Empty response from llama.cpp");
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

        private class LlamaGenerateResponse
        {
            public string Response { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
        }
    }
}