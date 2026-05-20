using Microsoft.Extensions.Http.Resilience;
using OpsPilotAI.Features.Query.Services;
using OpsPilotAI.Features.Schema.Services;
using OpsPilotAI.Features.VectorStore.Services;
using OpsPilotAI.Infrastructure.AI;
using OpsPilotAI.Infrastructure.Configuration;
using OpsPilotAI.Infrastructure.Middleware;
using Scalar.AspNetCore;
using Serilog;

// ── Bootstrap Serilog from appsettings before the host starts ─────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting OpsPilotAI...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ────────────────────────────────────────────────────────────────
    // Replace default Microsoft logger with Serilog for structured, correlated logging.
    // Configuration is read from appsettings.json "Serilog" section.
    builder.Host.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration));

    // ── Strongly-typed configuration with startup validation ───────────────────
    // Options pattern instead of reading IConfiguration in constructors.
    // .ValidateDataAnnotations() + .ValidateOnStart() surfaces misconfiguration
    // immediately at startup, not at first request (fail-fast).
    builder.Services
        .AddOptions<LlamaOptions>()
        .BindConfiguration(LlamaOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // ── Database ───────────────────────────────────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' is not configured.");

    builder.Services.AddNpgsqlDataSource(connectionString);

    // ── Health checks ──────────────────────────────────────────────────────────
    // /healthz — used by load balancers, Docker, Kubernetes probes.
    // PostgreSQL check ensures the DB is reachable before traffic is routed in.
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgresql", tags: ["db", "ready"]);

    // ── HTTP clients with resilience ───────────────────────────────────────────
    // AddHttpClient + AddStandardResilienceHandler wires Polly v8:
    //   - Retry (3x with exponential back-off)
    //   - Circuit breaker
    //   - Total request timeout
    // Timeouts are configured per-client from LlamaOptions.
    builder.Services
        .AddHttpClient<IAiCompletionService, LlamaCompletionService>(
            (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<LlamaOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(opts.SqlTimeoutSeconds);
            })
        .AddStandardResilienceHandler(opts =>
        {
            opts.Retry.MaxRetryAttempts = 2;
            opts.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(6);
        });

    builder.Services
        .AddHttpClient<IEmbeddingService, LlamaEmbeddingService>(
            (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<LlamaOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(opts.EmbeddingTimeoutSeconds);
            })
        .AddStandardResilienceHandler(opts =>
        {
            opts.Retry.MaxRetryAttempts = 2;
            opts.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
        });

    // ── Caching ────────────────────────────────────────────────────────────────
    builder.Services.AddMemoryCache();

    // ── Schema feature ─────────────────────────────────────────────────────────
    // Scoped: each request gets its own DB connection lifecycle.
    builder.Services.AddScoped<ISchemaExtractorService, SchemaExtractorService>();
    builder.Services.AddScoped<IRelationshipGraphService, RelationshipGraphService>();

    // Singleton: SchemaBuilderService is stateless and string-building only.
    builder.Services.AddSingleton<ISchemaBuilderService, SchemaBuilderService>();

    // ── Vector store feature ───────────────────────────────────────────────────
    builder.Services.AddScoped<IVectorStoreService, VectorStoreService>();

    // ── Query feature ──────────────────────────────────────────────────────────
    builder.Services.AddScoped<IRetrieverService, RetrieverService>();
    builder.Services.AddScoped<IQueryOrchestrationService, QueryOrchestrationService>();

    // Singleton: stateless prompt/validation services — no per-request allocation.
    builder.Services.AddSingleton<IPromptBuilderService, PromptBuilderService>();
    builder.Services.AddSingleton<ISqlValidatorService, SqlValidatorService>();

    // Scoped: execution needs a fresh DB connection per request.
    builder.Services.AddScoped<IQueryExecutionService, QueryExecutionService>();

    // ── API / OpenAPI ──────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // ── Build ──────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // GlobalExceptionHandlingMiddleware must be FIRST so it catches exceptions from
    // all subsequent middleware and handlers.
    app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

    // Request logging via Serilog — logs method, path, status, elapsed time per request.
    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diag, ctx) =>
        {
            diag.Set("RequestHost", ctx.Request.Host.Value ?? string.Empty);
            diag.Set("RequestScheme", ctx.Request.Scheme);
        };
    });

    // ── Health endpoints ───────────────────────────────────────────────────────
    app.MapHealthChecks("/healthz");

    // ── OpenAPI (development only) ─────────────────────────────────────────────
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();
    app.MapControllers();

    Log.Information("OpsPilotAI started successfully.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OpsPilotAI failed to start.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
