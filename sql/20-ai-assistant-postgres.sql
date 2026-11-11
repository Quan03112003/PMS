-- Lotus AI Assistant. Apply on a trial database before using /api/ai.
-- Does not change booking, folio, or guest tables.

CREATE TABLE IF NOT EXISTS ai_knowledge_documents (
    id uuid PRIMARY KEY,
    property_id integer NULL REFERENCES properties(id),
    code varchar(40) NOT NULL,
    title varchar(160) NOT NULL,
    file_name varchar(200) NOT NULL,
    version_label varchar(40) NOT NULL DEFAULT '1',
    module varchar(40) NOT NULL DEFAULT 'OTHER',
    status varchar(20) NOT NULL DEFAULT 'DRAFT',
    raw_text text NOT NULL,
    uploaded_by_user_id integer NOT NULL REFERENCES app_users(id),
    uploaded_at_utc timestamptz NOT NULL,
    approved_by_user_id integer NULL REFERENCES app_users(id),
    approved_at_utc timestamptz NULL
);

CREATE TABLE IF NOT EXISTS ai_knowledge_chunks (
    id uuid PRIMARY KEY,
    document_id uuid NOT NULL REFERENCES ai_knowledge_documents(id) ON DELETE CASCADE,
    ordinal integer NOT NULL,
    heading varchar(200) NOT NULL DEFAULT '',
    content text NOT NULL,
    CONSTRAINT uq_ai_chunk UNIQUE (document_id, ordinal)
);

CREATE TABLE IF NOT EXISTS ai_conversations (
    id uuid PRIMARY KEY,
    user_id integer NOT NULL REFERENCES app_users(id),
    property_id integer NULL REFERENCES properties(id),
    module varchar(40) NOT NULL DEFAULT 'OTHER',
    created_at_utc timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_ai_conversations_user ON ai_conversations (user_id, created_at_utc);

CREATE TABLE IF NOT EXISTS ai_messages (
    id uuid PRIMARY KEY,
    conversation_id uuid NOT NULL REFERENCES ai_conversations(id) ON DELETE CASCADE,
    role varchar(20) NOT NULL,
    content text NOT NULL,
    citations_json text NOT NULL DEFAULT '[]',
    created_at_utc timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_ai_messages_conversation ON ai_messages (conversation_id, created_at_utc);

CREATE TABLE IF NOT EXISTS ai_answer_feedback (
    id uuid PRIMARY KEY,
    message_id uuid NOT NULL REFERENCES ai_messages(id) ON DELETE CASCADE,
    user_id integer NOT NULL REFERENCES app_users(id),
    helpful boolean NOT NULL,
    note varchar(500) NULL,
    created_at_utc timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS ai_golden_questions (
    id uuid PRIMARY KEY,
    dataset_code varchar(40) NOT NULL DEFAULT 'default',
    module varchar(40) NOT NULL,
    intent varchar(40) NOT NULL,
    question text NOT NULL,
    question_variations text NOT NULL DEFAULT '',
    question_type varchar(40) NOT NULL,
    difficulty varchar(20) NOT NULL,
    expected_answer text NOT NULL,
    expected_keywords text NOT NULL DEFAULT '',
    evaluation_criteria text NOT NULL DEFAULT '',
    answerable varchar(40) NOT NULL,
    source_document_ids text NOT NULL DEFAULT '',
    source_version varchar(40) NOT NULL DEFAULT '',
    approval_status varchar(20) NOT NULL DEFAULT 'DRAFT',
    approved_by_user_id integer NULL REFERENCES app_users(id),
    approved_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS ai_evaluation_runs (
    id uuid PRIMARY KEY,
    model_name varchar(80) NOT NULL,
    prompt_version varchar(40) NOT NULL,
    retrieval_mode varchar(20) NOT NULL,
    started_at_utc timestamptz NOT NULL,
    finished_at_utc timestamptz NULL,
    passed integer NOT NULL DEFAULT 0,
    failed integer NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS ai_evaluation_results (
    id uuid PRIMARY KEY,
    run_id uuid NOT NULL REFERENCES ai_evaluation_runs(id) ON DELETE CASCADE,
    question_id uuid NOT NULL REFERENCES ai_golden_questions(id),
    question text NOT NULL,
    retrieved text NOT NULL,
    expected_answer text NOT NULL,
    actual_answer text NOT NULL,
    citations text NOT NULL,
    passed boolean NOT NULL,
    note text NOT NULL,
    created_at_utc timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS ai_ingestion_jobs (
    id uuid PRIMARY KEY,
    document_id uuid NOT NULL REFERENCES ai_knowledge_documents(id) ON DELETE CASCADE,
    status varchar(20) NOT NULL,
    message text NOT NULL,
    created_at_utc timestamptz NOT NULL
);
