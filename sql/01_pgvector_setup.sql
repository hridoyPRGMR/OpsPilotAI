-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Create schema_embeddings table
CREATE TABLE IF NOT EXISTS schema_embeddings (
    id TEXT PRIMARY KEY,
    table_name TEXT NOT NULL,
    schema_text TEXT NOT NULL,
    embedding vector(768) NOT NULL,
    metadata JSONB,
    created_at TIMESTAMP DEFAULT NOW()
);

-- Create index for vector similarity search
CREATE INDEX IF NOT EXISTS idx_schema_embeddings_embedding 
    ON schema_embeddings USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 100);

-- Create index for table name search
CREATE INDEX IF NOT EXISTS idx_schema_embeddings_table_name 
    ON schema_embeddings (table_name);

-- Grant permissions (if needed)
-- GRANT ALL ON schema_embeddings TO postgres;
