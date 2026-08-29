-- Creates world_session_events table used by WorldEventPersistenceService
CREATE TABLE IF NOT EXISTS world_session_events (
    event_id    TEXT        NOT NULL PRIMARY KEY,
    session_id  TEXT        NOT NULL,
    event_type  TEXT        NOT NULL,
    category    TEXT        NOT NULL DEFAULT '',
    frame       INTEGER     NOT NULL DEFAULT 0,
    entity_id   TEXT        NOT NULL DEFAULT '',
    message     TEXT        NOT NULL DEFAULT '',
    data        JSONB       NOT NULL DEFAULT '{}',
    timestamp_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_wse_session_ts
    ON world_session_events(session_id, timestamp_utc DESC);
CREATE INDEX IF NOT EXISTS idx_wse_event_type
    ON world_session_events(event_type);
CREATE INDEX IF NOT EXISTS idx_wse_frame
    ON world_session_events(session_id, frame);
