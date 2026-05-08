namespace Features.Ai.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;

        public AiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> AskAsync(string prompt)
        {
            try
            {
                var request = new
                {
                    prompt = prompt,
                    n_predict = 200,
                    temperature = 0.2
                };

                var response = await _httpClient.PostAsJsonAsync(
                    "http://localhost:8080/completion",
                    request);

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<LlamaResponse>();

                return result?.Content ?? "No response";
            }
            catch (TaskCanceledException ex)
            {
                return $"Request timed out: {ex.Message}";
            }
            catch (HttpRequestException ex)
            {
                return $"HTTP error: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Unexpected error: {ex.Message}";
            }
        }

        public class LlamaResponse
        {
            public string Content { get; set; } = "";
        }
    }
}