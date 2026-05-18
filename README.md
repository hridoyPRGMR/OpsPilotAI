# OpsPilotAI

## About This Project

**OpsPilotAI** is a Text-to-SQL system that converts natural language questions into executable SQL queries using AI. It leverages a Retrieval-Augmented Generation (RAG) approach combined with vector embeddings to intelligently retrieve relevant database schema information and generate safe, accurate SQL queries.

### Project Goal

The primary goal of OpsPilotAI is to:
- Enable non-technical users to query databases using natural language
- Provide intelligent, context-aware SQL generation using AI
- Ensure database safety through SQL validation before execution
- Demonstrate a production-ready architecture for text-to-SQL systems in .NET

## Key Features

✅ **Natural Language to SQL**
- Converts user questions into SQL queries using Qwen2.5-Coder LLM via Ollama

✅ **Schema Intelligence**
- Automatically extracts PostgreSQL database schema (tables, columns, relationships)
- Builds semantic documents with AI-readable database descriptions
- Infers foreign key relationships for related table discovery

✅ **Vector-Based Retrieval**
- Uses pgvector (PostgreSQL vector extension) for semantic similarity search
- Retrieves top-K most relevant tables based on user query
- Cosine distance metric for similarity calculations

✅ **Safety-First Execution**
- Validates SQL queries before execution
- Blocks dangerous operations (DELETE, DROP, EXEC, etc.)
- Enforces LIMIT clauses to prevent runaway queries

✅ **Full Query Pipeline**
- Schema Extraction → Semantic Indexing → Vector Retrieval → Prompt Building → SQL Generation → Validation → Execution

## Architecture

### Technology Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 10 ASP.NET Core Web API |
| Database | PostgreSQL with pgvector extension (768 dimensions) |
| Vector Distance | Cosine similarity |
| LLM | Qwen2.5-Coder via Ollama |
| Embeddings | nomic-embed-text (768 dimensions) |
| ORM | Dapper |

### Project Structure

```
OpsPilotAI/
├── Features/
│   ├── SchemaExtractor/
│   │   ├── Models/
│   │   │   ├── TableSchema.cs
│   │   │   ├── ColumnSchema.cs
│   │   │   └── RelationshipSchema.cs
│   │   ├── Dtos/
│   │   │   └── ColumnQueryResult.cs
│   │   └── Services/
│   │       ├── SchemaExtractorService.cs
│   │       ├── SchemaBuilderService.cs
│   │       ├── RelationshipGraphService.cs
│   │       └── RetrieverService.cs
│   ├── Ai/
│   │   ├── Models/
│   │   │   └── EmbeddingModel.cs
│   │   └── Services/
│   │       ├── AiService.cs
│   │       ├── EmbeddingService.cs
│   │       ├── VectorDatabaseService.cs
│   │       ├── PromptBuilderService.cs
│   │       ├── SqlValidatorService.cs
│   │       ├── ExecutionService.cs
│   │       └── QueryOrchestrationService.cs
├── Controllers/
│   ├── QueryController.cs
│   ├── TestController.cs
│   ├── AiTestController.cs
│   └── AiController.cs
├── Program.cs
├── appsettings.json
└── README.md
```

### Component Details

**SchemaExtractor Services:**
- `SchemaExtractorService` - Extracts tables, columns, PKs, FKs from PostgreSQL
- `SchemaBuilderService` - Generates AI-readable semantic documents
- `RelationshipGraphService` - Builds and infers foreign key relationships
- `RetrieverService` - Manages vector database population and retrieval

**AI Services:**
- `EmbeddingService` - Generates vector embeddings via Ollama
- `VectorDatabaseService` - Stores/retrieves embeddings from PostgreSQL pgvector
- `PromptBuilderService` - Constructs structured prompts with relevant schema
- `AiService` - Calls LLM (Qwen2.5-Coder) for SQL generation
- `SqlValidatorService` - Validates query safety
- `ExecutionService` - Executes validated SQL against PostgreSQL
- `QueryOrchestrationService` - Orchestrates the complete pipeline

## Getting Started

### Prerequisites

Ensure you have the following installed and running:

1. **PostgreSQL 14+**
   - Must have pgvector extension installed
   - Run: `CREATE EXTENSION IF NOT EXISTS vector;`
   - Sample database: opspilotdb

2. **Ollama** (for AI inference)
   - Download from https://ollama.ai
   - Required models:
     - `qwen2.5-coder` - SQL generation
     - `nomic-embed-text` - Text embeddings (768 dimensions)

3. **.NET 10 SDK**
   - VSCode or Visual Studio recommended

### Installation & Setup

1. **Clone/Download the project**
   ```bash
   cd OpsPilotAI
   ```

2. **Configure Database Connection**
   
   Edit `appsettings.json` and `appsettings.Development.json` to use the local Docker host port:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5433;Database=opspilotdb;Username=opspilot;Password=opspilot_pass"
     },
     "Ollama": {
       "BaseUrl": "http://localhost:11434",
       "SqlModel": "qwen2.5-coder",
       "EmbeddingModel": "nomic-embed-text"
     }
   }
   ```

3. **Initialize Vector Database**
   
   If you are running Postgres via Docker Compose, the `sql/01_init.sql` script runs automatically on first boot. Otherwise, run:
   ```bash
   psql -U opspilot -d opspilotdb -f sql/01_init.sql
   ```

4. **Start Ollama**
   ```bash
   ollama serve
   ```
   In another terminal, pull required models:
   ```bash
   ollama pull qwen2.5-coder
   ollama pull nomic-embed-text
   ```

5. **Run the Application**
   ```bash
   dotnet run
   ```
   
   API will start at: `http://localhost:5000` (or configured port)

## API Endpoints

### Main Production Endpoint

**POST /api/query** - Execute natural language query
```json
{
  "question": "How many films are in the Action category?"
}
```

Response:
```json
{
  "question": "How many films are in the Action category?",
  "sql": "SELECT COUNT(*) FROM film WHERE film_id IN (SELECT film_id FROM film_category WHERE category_id = (SELECT category_id FROM category WHERE name = 'Action'))",
  "results": [[64]],
  "success": true
}
```

### Test & Debug Endpoints

**Schema Information:**
- `GET /test/tables` - List all tables
- `GET /test/columns/{table}` - Get columns for a table
- `GET /test/relationships` - View all relationships
- `GET /test/schema` - Get full schema
- `GET /test/semantic` - Get semantic documents
- `GET /test/graph` - Get relationship graph

**Vector Retrieval:**
- `GET /test/retrieve?query=...` - Retrieve top-5 relevant tables for a query

**Query Building & Validation:**
- `POST /test/prompt` - Build prompt (body: `{"question":"..."}`)
- `POST /test/generate-sql` - Generate SQL (body: `{"question":"..."}`)
- `POST /test/validate-sql` - Validate SQL (body: `{"sql":"..."}`)
- `POST /test/execute` - Execute SQL (body: `{"sql":"..."}`)

## How It Works

### Query Processing Pipeline

```
User Question
    ↓
Embedding Lookup (Vector DB)
    ↓
Retrieve Relevant Tables
    ↓
Build Schema-Aware Prompt
    ↓
Generate SQL (LLM)
    ↓
Validate Query Safety
    ↓
Execute Against Database
    ↓
Return Results
```

1. **Retrieval** - User question is embedded and compared against schema vectors to find relevant tables
2. **Prompt Building** - Only relevant schema is included in the prompt to the LLM
3. **Generation** - Qwen2.5-Coder generates SQL based on the prompt and schema context
4. **Validation** - SqlValidatorService checks for dangerous operations (DELETE, DROP, etc.)
5. **Execution** - Safe SQL is executed against PostgreSQL using Dapper
6. **Response** - Results are returned to the user

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=opspilotdb;Username=opspilot;Password=opspilot_pass"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "SqlModel": "qwen2.5-coder",
    "EmbeddingModel": "nomic-embed-text"
  },
  "VectorDb": {
    "TopK": 5,
    "SimilarityThreshold": 0.3
  }
}
```

## Dependencies

- **Dapper** - Lightweight ORM for database access
- **Npgsql** - PostgreSQL .NET data provider
- **System.Net.Http.Json** - JSON serialization for HTTP requests

Install via:
```bash
dotnet add package Dapper
dotnet add package Npgsql
```

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| RAG Architecture | Reduces context size, improves accuracy, speeds up inference |
| Semantic Indexing | Schema documents make vector search more meaningful |
| pgvector instead of external DB | Keeps all data in PostgreSQL for simplicity |
| SQL Validation | Prevents accidental or malicious database modifications |
| Stateless Services | Enables horizontal scaling without session management |
| Separate Test Endpoints | Allows debugging/validation without affecting production |

## Development Notes

- Schema is cached in memory with 10-minute TTL for performance
- Vector embeddings are computed once during initialization
- Each query performs fresh vector similarity search
- All schema extraction happens on app startup

## Troubleshooting

### Docker (WSL): Postgres + pgvector

If you run PostgreSQL inside Docker on WSL, here's a minimal setup and troubleshooting notes used by this project.

- Start the database (run from the project root in WSL):

```bash
cp .env.example .env
# edit .env if you change credentials
docker compose up -d
```

- Check container status and logs:

```bash
docker compose ps
docker compose logs -f db
```

- Verify pgvector is installed and the init script ran:

```bash
docker compose exec db psql -U $POSTGRES_USER -d $POSTGRES_DB -c "SELECT extname FROM pg_extension;"
docker compose exec db psql -U $POSTGRES_USER -d $POSTGRES_DB -c "\dt"
```

- Notes:
  - The init script `sql/01_init.sql` runs only when the DB data directory is initialized. If you change the SQL and need it re-run, remove the `opspilot_pgdata` volume and run `docker compose down` and `docker compose up -d` to recreate the container and re-run initialization.
  - Ensure your WSL distro has access to Docker (Docker Desktop WSL backend or Docker in WSL). If using Docker Desktop, confirm the WSL integration is enabled.
  - Update the connection string in `appsettings.Development.json` and `appsettings.json` if you change credentials, host, or port.

**Issue: Connection refused to PostgreSQL**
- Ensure PostgreSQL is running: `sudo systemctl start postgresql`
- Verify connection string in `appsettings.json`

**Issue: Ollama connection failed**
- Ensure Ollama is running: `ollama serve`
- Verify models are available: `ollama list`
- Check BaseUrl matches your Ollama installation

**Issue: Vectors not retrieving results**
- Ensure `sql/01_init.sql` was executed
- Check pgvector extension is installed: `CREATE EXTENSION vector;`
- Populate vector DB: `POST /test/populate-vector-db`

**Issue: SQL generation is inaccurate**
- Review /test/retrieve to see which tables were selected
- Check /test/semantic to verify schema documentation
- Review the generated prompt: `POST /test/prompt`

## Future Enhancements

- [ ] Query result caching for frequently asked questions
- [ ] Query execution timeout handling
- [ ] Request logging and telemetry
- [ ] Multi-database support
- [ ] SQL query formatting before execution
- [ ] Result pagination for large datasets
- [ ] Role-based schema filtering
- [ ] Query performance optimization suggestions

## License

This project is licensed under the **Apache License 2.0**. See the [LICENSE](LICENSE) file for details.

```
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

<!-- ## Contributing

[Add contribution guidelines here] -->

---

**Questions or Issues?** Please file an issue in the repository or contact the development team.
