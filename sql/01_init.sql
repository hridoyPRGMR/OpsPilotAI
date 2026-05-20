-- OpsPilotAI — PostgreSQL initialisation script
-- Runs automatically when the Docker container first boots (docker-entrypoint-initdb.d).
--
-- FIX: The original repo had two conflicting "01_" files:
--   01_init.sql         — only created the extension
--   01_pgvector_setup.sql — created the extension + schema_embeddings table
-- Docker runs init scripts in alphabetical order and stops at first alphanumeric collision.
-- This single consolidated script replaces both.

-- 1. Enable pgvector
CREATE EXTENSION IF NOT EXISTS vector;

-- 2. schema_embeddings — stores table-level vector embeddings for semantic search
CREATE TABLE IF NOT EXISTS schema_embeddings (
    id          TEXT        PRIMARY KEY,
    table_name  TEXT        NOT NULL,
    schema_text TEXT        NOT NULL,
    embedding   vector(768) NOT NULL,
    metadata    JSONB,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

-- 3. IVFFlat index for cosine-distance similarity search
--    lists=100 is suitable for up to ~1M vectors; tune upward if the schema grows
CREATE INDEX IF NOT EXISTS idx_schema_embeddings_vector
    ON schema_embeddings USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 100);

-- 4. B-tree index for fast table_name lookups
CREATE INDEX IF NOT EXISTS idx_schema_embeddings_table
    ON schema_embeddings (table_name);
