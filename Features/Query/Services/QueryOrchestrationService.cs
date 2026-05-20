using OpsPilotAI.Common.Exceptions;
using OpsPilotAI.Features.Query.Models;
using OpsPilotAI.Infrastructure.AI;

namespace OpsPilotAI.Features.Query.Services;

/// <summary>
/// Orchestrates the complete text-to-SQL pipeline:
///   1. Retrieve relevant schema via vector similarity
///   2. Build LLM prompt with schema context
///   3. Generate SQL via LLM
///   4. Validate SQL safety
///   5. Execute against PostgreSQL
///
/// Key improvements over the original:
///   - CancellationToken propagated to every stage
///   - Throws PipelineException on stage failures instead of returning Success=false objects
///     mid-method and continuing (partial results were confusing)
///   - Removed wrong STEP comments (original had STEP 5–9 with no steps 1–4)
///   - Each stage is a clearly labelled log scope for easier tracing
///   - No nested result classes — uses top-level records
/// </summary>
public sealed class QueryOrchestrationService(
    IRetrieverService retriever,
    IPromptBuilderService promptBuilder,
    IAiCompletionService aiCompletion,
    ISqlValidatorService sqlValidator,
    IQueryExecutionService execution,
    ILogger<QueryOrchestrationService> logger) : IQueryOrchestrationService
{
    public async Task<TextToSqlResult> ExecuteAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Pipeline start — question: {Question}", question);

        // Stage 1: Vector retrieval
        var relevantSchema = await retriever.RetrieveRelevantSchemaAsync(question, topK: 5, cancellationToken);

        if (relevantSchema.Count == 0)
        {
            logger.LogWarning("No relevant schema found for question: {Question}", question);
            throw new PipelineException(PipelineStage.SchemaRetrieval,
                "No relevant schema could be found. Ensure the vector database has been populated.");
        }

        var relevantTables = relevantSchema.Select(s => s.TableName).ToList();
        logger.LogInformation("Schema retrieval complete — tables: [{Tables}]",
            string.Join(", ", relevantTables));

        // Stage 2: Prompt construction
        var prompt = promptBuilder.BuildSqlGenerationPrompt(question, relevantSchema);

        // Stage 3: SQL generation
        var generatedSql = await aiCompletion.CompleteAsync(prompt, cancellationToken);

        logger.LogInformation("SQL generated: {Sql}", generatedSql);

        // Stage 4: Validation
        var (isValid, validationMessage) = sqlValidator.Validate(generatedSql);

        if (!isValid)
        {
            logger.LogWarning("SQL validation failed: {Message} — SQL: {Sql}",
                validationMessage, generatedSql);
            throw new PipelineException(PipelineStage.SqlValidation,
                $"Generated SQL failed safety validation: {validationMessage}");
        }

        var safeSql = sqlValidator.EnsureLimit(generatedSql);

        // Stage 5: Execution
        var executionResult = await execution.ExecuteAsync(safeSql, cancellationToken);

        logger.LogInformation(
            "Pipeline complete — {RowCount} rows in {Ms}ms",
            executionResult.RowCount,
            executionResult.ExecutionTimeMs);

        return new TextToSqlResult
        {
            Success         = true,
            Message         = "Query executed successfully.",
            RelevantTables  = relevantTables,
            GeneratedSql    = generatedSql,
            ValidatedSql    = safeSql,
            Rows            = executionResult.Rows,
            RowCount        = executionResult.RowCount,
            ExecutionTimeMs = executionResult.ExecutionTimeMs
        };
    }
}
