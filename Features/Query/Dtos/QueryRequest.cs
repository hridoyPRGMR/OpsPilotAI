using System.ComponentModel.DataAnnotations;

namespace OpsPilotAI.Features.Query.Dtos;

/// <summary>
/// Extracted from the nested class inside QueryController.
/// Adding [Required] enables model-binding validation before the action runs.
/// </summary>
public sealed class QueryRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Question is required.")]
    [MaxLength(2000, ErrorMessage = "Question must not exceed 2000 characters.")]
    public required string Question { get; init; }
}
