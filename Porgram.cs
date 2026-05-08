using Features.Ai.Services;
using OpsPilotAI.Features.SchemaExtractor.Services;
using OpsPilotAI.Features.Ai.Services;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHttpClient<AiService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHttpClient<EmbeddingService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHttpClient<VectorDatabaseService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<SchemaExtractorService>();
builder.Services.AddScoped<SchemaBuilderService>();
builder.Services.AddScoped<RelationshipGraphService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<VectorDatabaseService>();
builder.Services.AddScoped<RetrieverService>();
builder.Services.AddScoped<PromptBuilderService>();
builder.Services.AddScoped<SqlValidatorService>();
builder.Services.AddScoped<ExecutionService>();
builder.Services.AddScoped<QueryOrchestrationService>();

var connectionString = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string 'Default' not found.");
builder.Services.AddNpgsqlDataSource(connectionString);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();


app.MapControllers();

app.Run();