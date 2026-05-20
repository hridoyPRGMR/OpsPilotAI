using OpsPilotAI.Features.Schema.Services;
using OpsPilotAI.Features.VectorStore.Models;
using OpsPilotAI.Features.VectorStore.Services;
using OpsPilotAI.Infrastructure.AI;

namespace OpsPilotAI.Features.Query.Services;

/// <summary>
/// Orchestrates schema population and semantic retrieval.
///
/// Key fixes over the original:
///   1. Uses ISchemaBuilderService (with relationship graph) to build semantic documents
///      instead of SchemaExtractorService.BuildSemanticDocument (which had no graph context).
///      This means the embedded documents now include business keywords and relationships —
///      making vector search significantly more relevant.
///   2. PopulateVectorDatabaseAsync throws on any embedding failure (previously returned false
///      after silently swallowing errors).
///   3. CancellationToken propagated throughout.
/// </summary>
public sealed class RetrieverService(
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStore,
    ISchemaExtractorService schemaExtractor,
    ISchemaBuilderService schemaBuilder,
    IRelationshipGraphService relationshipGraph,
    ILogger<RetrieverService> logger) : IRetrieverService
{
    public async Task<IReadOnlyList<VectorSearchResult>> RetrieveRelevantSchemaAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Embedding query for vector retrieval: {Query}", query);

        var vector = await embeddingService.EmbedAsync(query, cancellationToken);
        var results = await vectorStore.SearchAsync(vector, topK, cancellationToken);

        logger.LogInformation("Retrieved {Count} schema entries (topK={TopK})", results.Count, topK);
        return results;
    }

    public async Task PopulateVectorDatabaseAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting vector database population.");

        await vectorStore.EnsureCollectionAsync(cancellationToken);

        var schema = await schemaExtractor.ExtractSchemaAsync(cancellationToken);
        var graph  = await relationshipGraph.GetGraphAsync(cancellationToken);

        logger.LogInformation("Embedding {Count} tables.", schema.Count);

        int processed = 0;
        foreach (var table in schema)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Build semantic doc WITH graph context — the critical fix.
            // Original code used SchemaExtractorService.BuildSemanticDocument which lacked
            // business keywords and relationship context.
            var semanticDoc = schemaBuilder.BuildSemanticDocument(table, graph);
            var vector      = await embeddingService.EmbedAsync(semanticDoc, cancellationToken);

            var embedding = new EmbeddingModel
            {
                TableName  = table.TableName,
                SchemaText = semanticDoc,
                Vector     = vector,
                Metadata   = new Dictionary<string, object>
                {
                    ["columns_count"]      = table.Columns.Count,
                    ["relationships_count"] = table.Relationships.Count
                }
            };

            await vectorStore.UpsertAsync((++processed).ToString(), embedding, cancellationToken);

            logger.LogInformation("  [{Processed}/{Total}] Embedded '{Table}'",
                processed, schema.Count, table.TableName);
        }

        logger.LogInformation("Vector database population complete — {Count} tables embedded.", processed);
    }
}
