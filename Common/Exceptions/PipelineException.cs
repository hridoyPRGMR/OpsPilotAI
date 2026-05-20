namespace OpsPilotAI.Common.Exceptions;

/// <summary>
/// Thrown when a stage in the text-to-SQL pipeline fails in a way the caller should surface to the user.
/// This distinguishes expected domain failures (no schema found, SQL validation failed)
/// from unexpected infrastructure exceptions.
/// </summary>
public sealed class PipelineException : Exception
{
    public PipelineStage Stage { get; }

    public PipelineException(PipelineStage stage, string message)
        : base(message)
    {
        Stage = stage;
    }

    public PipelineException(PipelineStage stage, string message, Exception inner)
        : base(message, inner)
    {
        Stage = stage;
    }
}

public enum PipelineStage
{
    SchemaRetrieval,
    Embedding,
    PromptBuilding,
    SqlGeneration,
    SqlValidation,
    SqlExecution
}
