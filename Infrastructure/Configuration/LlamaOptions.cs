using System.ComponentModel.DataAnnotations;

namespace OpsPilotAI.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the llama.cpp HTTP API.
/// Bound at startup and validated via IOptions&lt;T&gt; with DataAnnotations.
/// Using Options pattern instead of reading IConfiguration in constructors allows:
///   - Startup-time validation (fail fast on bad config rather than at first request)
///   - Easy unit testing (just new up the options object)
///   - Single source of truth for all LLM settings
/// </summary>
public sealed class LlamaOptions
{
    public const string SectionName = "Llama";

    [Required]
    public required string SqlBaseUrl { get; init; }

    [Required]
    public required string SqlEndpointPath { get; init; }

    [Required]
    public required string EmbeddingBaseUrl { get; init; }

    [Required]
    public required string EmbeddingEndpointPath { get; init; }

    [Required]
    public required string SqlModel { get; init; }

    [Required]
    public required string EmbeddingModel { get; init; }

    public int SqlTimeoutSeconds { get; init; } = 300;

    public int EmbeddingTimeoutSeconds { get; init; } = 120;

    public string SqlEndpointUrl =>
        $"{SqlBaseUrl.TrimEnd('/')}/{SqlEndpointPath.TrimStart('/')}";

    public string EmbeddingEndpointUrl =>
        $"{EmbeddingBaseUrl.TrimEnd('/')}/{EmbeddingEndpointPath.TrimStart('/')}";
}
