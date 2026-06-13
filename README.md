# OpsPilotAI

## About This Project

**OpsPilotAI** is a production-grade Text-to-SQL platform that converts natural language questions into executable SQL queries using AI. It uses a Retrieval-Augmented Generation (RAG) pipeline backed by PostgreSQL pgvector for semantic schema retrieval, and llama.cpp for local LLM inference — no cloud dependency required.

<img width="1130" height="700" alt="screenshot" src="https://github.com/user-attachments/assets/d087659e-0a65-4930-be7d-bdf50bc374da" />

### Project Goals

- Enable non-technical users to query databases using natural language
- Provide intelligent, context-aware SQL generation using a local LLM
- Ensure database safety through SQL validation before any execution
- Serve as a production-ready reference architecture for text-to-SQL systems in .NET 10

---

## Key Features

- **Natural Language to SQL** — Converts questions into SQL using Qwen2.5-Coder via llama.cpp
- **Schema Intelligence** — Extracts PostgreSQL schema (tables, columns, FKs) and builds AI-readable semantic documents enriched with business keywords and relationship graphs
- **Vector-Based Retrieval** — Uses pgvector cosine similarity to find the most relevant tables for any question
- **Safety-First Execution** — Blocks dangerous operations (DELETE, DROP, TRUNCATE, etc.) and enforces LIMIT clauses before any SQL runs
- **Resilient HTTP** — Polly retry + circuit-breaker on all LLM/embedding calls
- **Structured Logging** — Serilog with per-request correlation and file rolling
- **Health Checks** — `/healthz` endpoint with PostgreSQL probe
- **Containerised** — Multi-stage Dockerfile + Docker Compose for the full stack

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10 ASP.NET Core Web API |
| Database | PostgreSQL 15 + pgvector extension (768 dimensions) |
| Vector distance | Cosine similarity (`<=>`) |
| LLM | Qwen2.5-Coder via llama.cpp |
| Embeddings | nomic-embed-text (768 dimensions) |
| ORM | Dapper |
| Logging | Serilog |
| Resilience | Polly v8 via `Microsoft.Extensions.Http.Resilience` |
| Health checks | `AspNetCore.HealthChecks.NpgSql` |

---

## Project Structure

```
OpsPilotAI/
├── Common/
│   └── Exceptions/
│       └── PipelineException.cs        # Domain failure type (surfaced as 422)
│
├── Features/
│   ├── Schema/                         # PostgreSQL schema extraction
│   │   ├── Dtos/
│   │   ├── Models/                     # ColumnSchema, TableSchema, RelationshipSchema
│   │   └── Services/
│   │       ├── ISchemaExtractorService / SchemaExtractorService
│   │       ├── ISchemaBuilderService   / SchemaBuilderService
│   │       └── IRelationshipGraphService / RelationshipGraphService
│   │
│   ├── VectorStore/                    # pgvector embedding storage & retrieval
│   │   ├── Models/                     # EmbeddingModel, VectorSearchResult
│   │   └── Services/
│   │       └── IVectorStoreService     / VectorStoreService
│   │
│   └── Query/                          # Text-to-SQL pipeline
│       ├── Dtos/                       # QueryRequest, QueryResponse
│       ├── Models/                     # TextToSqlResult, ExecutionResult
│       └── Services/
│           ├── IRetrieverService       / RetrieverService
│           ├── IPromptBuilderService   / PromptBuilderService
│           ├── ISqlValidatorService    / SqlValidatorService
│           ├── IQueryExecutionService  / QueryExecutionService
│           └── IQueryOrchestrationService / QueryOrchestrationService
│
├── Infrastructure/
│   ├── AI/                             # LLM / embedding adapters
│   │   ├── IAiCompletionService        / LlamaCompletionService
│   │   └── IEmbeddingService           / LlamaEmbeddingService
│   ├── Configuration/
│   │   └── LlamaOptions.cs             # Strongly-typed config (IOptions<T>)
│   └── Middleware/
│       └── GlobalExceptionHandlingMiddleware.cs
│
├── Controllers/
│   ├── QueryController.cs              # POST /api/query  (production)
│   ├── SchemaController.cs             # GET  /schema/*   (inspection)
│   └── DiagnosticsController.cs        # POST /diagnostics/* (pipeline debug)
│
├── OpsPilotAI.Tests/                   # xUnit unit tests
│   └── Unit/
│       ├── SqlValidatorServiceTests.cs
│       ├── QueryOrchestrationServiceTests.cs
│       └── PromptBuilderServiceTests.cs
│
├── sql/
│   └── 01_init.sql                     # pgvector + schema_embeddings table
├── Dockerfile                          # Multi-stage, non-root user
├── docker-compose.yml                  # db + app containers
├── .env.example
└── Program.cs
```

---

## Getting Started

### Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 10.0+ |
| Docker + Docker Compose | Latest |
| llama.cpp HTTP server | Any compatible build |
| Models | `qwen2.5-coder` (SQL), `nomic-embed-text` (embeddings) |

---

### Option A — Docker Compose (recommended)

Starts PostgreSQL **and** the application together.

```bash
# 1. Copy environment file
cp .env.example .env

# 2. Edit .env if you need different credentials or ports
#    Also set LLAMA_SQL_BASE_URL / LLAMA_EMBEDDING_BASE_URL to your llama.cpp host

# 3. Start everything
docker compose up -d

# 4. Seed the vector database (first run only)
curl -X POST http://localhost:5010/diagnostics/populate-vector-db

# 5. Run a query
curl -X POST http://localhost:5010/api/query \
     -H "Content-Type: application/json" \
     -d '{"question": "How many films are in the Action category?"}'
```

---

### Run Locally (development)

#### Prerequisites
- .NET 10 SDK installed
- Docker (for running PostgreSQL) or a local PostgreSQL instance
- A running llama.cpp HTTP server and required models (`qwen2.5-coder`, `nomic-embed-text`)

#### Quick start (DB via Docker, app locally)
1. Copy and edit env:
```bash
cp .env.example .env
# edit .env to set POSTGRES_* and LLAMA_* URLs if needed
```

2. Start only the database:
```bash
docker compose up -d db
```

Note: the `docker-compose.yml` uses the `pgvector/pgvector:pg15` image so the `pgvector` extension is available in the container. If you run PostgreSQL manually instead of via Docker, install and enable the `pgvector` extension in your database before seeding or running queries:

```bash
# inside psql as a superuser (or via docker exec):
CREATE EXTENSION IF NOT EXISTS vector;

# or using docker-compose (substitute env values if needed):
docker compose exec db psql -U $POSTGRES_USER -d $POSTGRES_DB -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

3. Restore and run the API (development):
- macOS / Linux:
```bash
ASPNETCORE_URLS=http://localhost:5010 dotnet run --project OpsPilotAI.csproj
```
- PowerShell:
```powershell
$env:ASPNETCORE_URLS='http://localhost:5010'; dotnet run --project OpsPilotAI.csproj
```

4. Ensure llama.cpp endpoints are reachable (match `.env` or `appsettings.Development.json`):
- `LLAMA_SQL_BASE_URL` — completion server
- `LLAMA_EMBEDDING_BASE_URL` — embedding server

5. Seed vector DB (first run only):
```bash
curl -X POST http://localhost:5010/diagnostics/populate-vector-db
```

6. Test a query:
```bash
curl -X POST http://localhost:5010/api/query \
  -H "Content-Type: application/json" \
  -d '{"question":"How many films are in the Action category?"}'
```

#### Notes
- If you prefer to run DB and app together, use `docker compose up --build`.
- To replay the SQL init script, remove the volume: `docker compose down -v` then `docker compose up -d`.
- Run tests:
```bash
cd OpsPilotAI.Tests
dotnet test
```

---

### Configuration

All settings live in `appsettings.json` (defaults) and `appsettings.Development.json` (overrides).

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=opspilotdb;Username=opspilot;Password=opspilot_pass"
  },
  "Llama": {
    "SqlBaseUrl": "http://127.0.0.1:8080",       // llama.cpp completion endpoint base URL
    "EmbeddingBaseUrl": "http://127.0.0.1:8081",  // llama.cpp embedding endpoint base URL
    "SqlModel": "qwen2.5-coder",
    "EmbeddingModel": "nomic-embed-text",
    "SqlTimeoutSeconds": 300,
    "EmbeddingTimeoutSeconds": 120,
    "CompletionTemperature": 0.3,
    "CompletionTopP": 0.9,
    "CompletionNumPredict": 200,
    "CompletionStream": false
  }
}
```

> All `Llama.*` values are validated at startup with `ValidateOnStart()`. The app refuses to start if any required field is missing.

---

## API Endpoints

### Production

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/query` | Run a natural language query end-to-end |
| `GET` | `/healthz` | Health check (PostgreSQL probe) |

**POST /api/query — request**
```json
{ "question": "How many films are in the Action category?" }
```

**POST /api/query — response**
```json
{
  "success": true,
  "question": "How many films are in the Action category?",
  "sql": "SELECT COUNT(*) FROM film f JOIN film_category fc ON f.film_id = fc.film_id JOIN category c ON fc.category_id = c.category_id WHERE c.name = 'Action' LIMIT 100;",
  "results": [{ "count": 64 }],
  "rowCount": 1,
  "executionTimeMs": 42
}
```

---

### Schema Inspection

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/schema/tables` | List all public tables |
| `GET` | `/schema/columns/{table}` | Columns for a specific table |
| `GET` | `/schema/relationships` | All FK relationships |
| `GET` | `/schema/full` | Full extracted schema |
| `GET` | `/schema/semantic` | AI-readable semantic documents |
| `GET` | `/schema/graph` | Relationship graph (including inferred FKs) |

---

### Pipeline Diagnostics

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/diagnostics/populate-vector-db` | Embed all tables and store in pgvector |
| `GET` | `/diagnostics/retrieve?query=&topK=` | Test vector retrieval for a query |
| `POST` | `/diagnostics/prompt` | Preview the LLM prompt for a question |
| `POST` | `/diagnostics/validate-sql` | Test SQL safety validation |
| `POST` | `/diagnostics/execute` | Execute raw SQL (validated before running) |

---

## How It Works

```
User Question
    │
    ▼
[1] Embed question            IEmbeddingService → LlamaEmbeddingService
    │
    ▼
[2] Vector search             IVectorStoreService → pgvector cosine similarity
    │  top-K most relevant tables
    ▼
[3] Build prompt              IPromptBuilderService
    │  schema context + rules injected
    ▼
[4] Generate SQL              IAiCompletionService → LlamaCompletionService
    │  Qwen2.5-Coder via llama.cpp
    ▼
[5] Validate SQL              ISqlValidatorService
    │  blocks DELETE/DROP/ALTER etc., enforces LIMIT
    ▼
[6] Execute                   IQueryExecutionService → Dapper → PostgreSQL
    │
    ▼
JSON response
```

Each stage failure raises a `PipelineException`, which the global middleware maps to **HTTP 422 Unprocessable Entity** with a clear message — no stack trace leaks in production.

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| RAG architecture | Keeps LLM prompts small and focused — only relevant tables included |
| pgvector for embeddings | No extra infrastructure; everything stays in PostgreSQL |
| Interfaces on every service | Enables unit testing with Moq; allows swapping backends (e.g. OpenAI instead of llama.cpp) |
| `IOptions<LlamaOptions>` + `ValidateOnStart` | Bad configuration fails at startup, not at first request |
| `PipelineException` + 422 | Clear user-facing errors without exposing internals |
| Singletons for stateless services | `SqlValidatorService` and `PromptBuilderService` are never re-allocated |
| Polly resilience on HTTP clients | LLM inference is unreliable; 2 automatic retries before giving up |
| Single batched schema query | Eliminates N+1 round-trips (was 1+N queries for N tables at startup) |
| `[GeneratedRegex]` on validator | Regex patterns compiled at build time, not per-call |

---

## Running Tests

```bash
cd OpsPilotAI.Tests
dotnet test --verbosity normal
```

Tests cover `SqlValidatorService` (safety rules), `QueryOrchestrationService` (pipeline logic), and `PromptBuilderService` (prompt construction) — all using Moq with zero real dependencies.

---

## Troubleshooting

**App refuses to start — "Llama SQL model is not configured"**
- Verify the `Llama` section exists in `appsettings.json` with all required fields.

**`/diagnostics/populate-vector-db` returns an error**
- Ensure llama.cpp is running and reachable at the configured embedding URL.
- Check `docker compose logs app` for the actual exception.

**Vector search returns no results**
- Run `POST /diagnostics/populate-vector-db` first; the vector DB is empty on a fresh install.
- If pgvector extension is missing: `docker compose exec db psql -U opspilot -d opspilotdb -c "CREATE EXTENSION vector;"`

**Need to re-run the SQL init script**
- The init script only runs on a fresh volume. To replay it:
  ```bash
  docker compose down -v   # destroys the opspilot_pgdata volume
  docker compose up -d
  ```

**llama.cpp connection timeout**
- Increase `Llama:SqlTimeoutSeconds` in `appsettings.json` (default: 300s).
- Verify `LLAMA_SQL_BASE_URL` in `.env` points to your llama.cpp server.

---

## Future Enhancements

- [ ] Authentication / RBAC (architecture is auth-ready — plug middleware into the pipeline)
- [ ] Query result caching for repeated questions
- [ ] Streaming SQL generation responses
- [ ] Multi-database support (connection-per-tenant)
- [ ] Result pagination
- [ ] OpenTelemetry traces for the full pipeline
- [ ] SQL formatting before display
- [ ] Admin UI for vector DB management

---

## License

This project is licensed under the **Apache License 2.0**. See the [LICENSE](LICENSE) file for details.

---

**Questions or Issues?** Please file an issue in the repository.
