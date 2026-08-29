-- Creates character_parts table to store expanded manifest of modular assets
CREATE TABLE IF NOT EXISTS character_parts (
    id          SERIAL PRIMARY KEY,
    part_id     TEXT    NOT NULL,
    asset_path  TEXT    NOT NULL,
    gender      TEXT,
    variant     INT,
    priority    INT,
    attach_bone TEXT,
    meta        JSONB   NOT NULL DEFAULT '{}',
    is_sample   BOOLEAN NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
-- Unique constraint to allow upserts based on part+asset
CREATE UNIQUE INDEX IF NOT EXISTS ux_character_parts_part_asset ON character_parts(part_id, asset_path);
CREATE INDEX IF NOT EXISTS idx_character_parts_partid ON character_parts(part_id);
CREATE INDEX IF NOT EXISTS idx_character_parts_asset_path ON character_parts(asset_path);
