using OpsPilotAI.Features.Ai.Services;
using OpsPilotAI.Features.SchemaExtractor.Services;
using Features.Ai.Services;

namespace OpsPilotAI.Features.Ai.Services
{
    public class QueryOrchestrationService
    {
        private readonly RetrieverService _retriever;
        private readonly PromptBuilderService _promptBuilder;
        private readonly AiService _aiService;
        private readonly SqlValidatorService _validator;
        private readonly ExecutionService _execution;
        private readonly ILogger<QueryOrchestrationService> _logger;

        public QueryOrchestrationService(
            RetrieverService retriever,
            PromptBuilderService promptBuilder,
            AiService aiService,
            SqlValidatorService validator,
            ExecutionService execution,
            ILogger<QueryOrchestrationService> logger)
        {
            _retriever = retriever;
            _promptBuilder = promptBuilder;
            _aiService = aiService;
            _validator = validator;
            _execution = execution;
            _logger = logger;
        }

        public async Task<TextToSqlResult> ExecuteAsync(string userQuestion)
        {
            _logger.LogInformation("Starting text-to-SQL pipeline for: {Question}", userQuestion);

            var result = new TextToSqlResult();

            // STEP 5: Retrieve relevant schema
            var relevantSchema = await _retriever.RetrieveRelevantSchemaAsync(userQuestion, topK: 5);
            if (!relevantSchema.Any())
            {
                _logger.LogWarning("No relevant schema retrieved");
                result.Success = false;
                result.Message = "Could not retrieve relevant schema";
                return result;
            }

            result.RelevantTables = relevantSchema.Select(s => s.TableName).ToList();
            _logger.LogInformation("Retrieved tables: {Tables}", string.Join(", ", result.RelevantTables));

            // STEP 6: Build prompt
            var prompt = _promptBuilder.BuildSqlGenerationPrompt(userQuestion, relevantSchema);
            result.Prompt = prompt;

            // STEP 7: Generate SQL
            var generatedSql = await _aiService.GenerateSqlAsync(prompt);
            if (string.IsNullOrEmpty(generatedSql))
            {
                _logger.LogError("Failed to generate SQL");
                result.Success = false;
                result.Message = "Failed to generate SQL query";
                return result;
            }

            result.GeneratedSql = generatedSql;
            _logger.LogInformation("Generated SQL: {Sql}", generatedSql);

            // STEP 8: Validate SQL
            var (isValid, validationMessage) = _validator.ValidateSql(generatedSql);
            if (!isValid)
            {
                _logger.LogError("SQL validation failed: {Message}", validationMessage);
                result.Success = false;
                result.Message = $"SQL validation failed: {validationMessage}";
                result.ValidatedSql = generatedSql;
                return result;
            }

            var validatedSql = _validator.EnsureLimitClause(generatedSql);
            result.ValidatedSql = validatedSql;
            _logger.LogInformation("SQL validation passed");

            // STEP 9: Execute SQL
            var executionResult = await _execution.ExecuteQueryAsync(validatedSql);
            if (!executionResult.Success)
            {
                _logger.LogError("Query execution failed: {Message}", executionResult.Message);
                result.Success = false;
                result.Message = executionResult.Message;
                return result;
            }

            result.Success = true;
            result.Message = "Query executed successfully";
            result.Rows = executionResult.Rows;
            result.RowCount = executionResult.RowCount;
            result.ExecutionTimeMs = executionResult.ExecutionTimeMs;

            _logger.LogInformation("Query completed successfully in {Elapsed}ms", executionResult.ExecutionTimeMs);

            return result;
        }

        public class TextToSqlResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public List<string> RelevantTables { get; set; } = new();
            public string Prompt { get; set; } = string.Empty;
            public string GeneratedSql { get; set; } = string.Empty;
            public string ValidatedSql { get; set; } = string.Empty;
            public List<object> Rows { get; set; } = new();
            public int RowCount { get; set; }
            public long ExecutionTimeMs { get; set; }
        }
    }
}
