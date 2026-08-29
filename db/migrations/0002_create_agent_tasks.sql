-- Creates agent_tasks table used by AgentTaskService
CREATE TABLE IF NOT EXISTS agent_tasks (
    id           TEXT        PRIMARY KEY,
    status       TEXT        NOT NULL DEFAULT 'pending',
    description  TEXT        NOT NULL,
    result       TEXT,
    agent_log    TEXT        NOT NULL DEFAULT '',
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS idx_agent_tasks_status ON agent_tasks (status, created_at DESC);
