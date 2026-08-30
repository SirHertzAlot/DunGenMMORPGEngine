-- Scylla/Cassandra CQL for mmo_world keyspace and tables
CREATE KEYSPACE IF NOT EXISTS mmo_world
WITH replication = {'class':'SimpleStrategy','replication_factor':1}
AND durable_writes = true;

USE mmo_world;

CREATE TABLE IF NOT EXISTS dungeon_sessions (
    session_id    text PRIMARY KEY,
    execution_id  text,
    pipeline_id   text,
    seed          int,
    width         int,
    height        int,
    dungeon_level int,
    room_count    int,
    enemy_count   int,
    loot_count    int,
    created_at    timestamp
);

CREATE TABLE IF NOT EXISTS dungeon_rooms (
    session_id text,
    room_id    int,
    x          int,
    y          int,
    width      int,
    height     int,
    PRIMARY KEY (session_id, room_id)
);

CREATE TABLE IF NOT EXISTS dungeon_enemies (
    session_id text,
    enemy_id   int,
    archetype  text,
    x          int,
    y          int,
    level      int,
    PRIMARY KEY (session_id, enemy_id)
);

CREATE TABLE IF NOT EXISTS dungeon_loot (
    session_id text,
    item_id    text,
    item_type  text,
    tier       text,
    x          int,
    y          int,
    PRIMARY KEY (session_id, item_id)
);

-- Additional tables to support snapshots and metadata. These allow
-- fast access to the latest entity state and arbitrary session metadata
-- without scanning the activity tables.

CREATE TABLE IF NOT EXISTS entity_snapshots (
    session_id   text,
    entity_id    text,
    entity_type  text,
    version      int,
    state_json   text,
    last_updated timestamp,
    PRIMARY KEY (session_id, entity_id)
);

CREATE TABLE IF NOT EXISTS session_metadata (
    session_id   text PRIMARY KEY,
    properties   map<text, text>,
    last_updated timestamp
);

-- Mastery tables used by MasteryPersistenceService (upsert offers and unlocked skills)
CREATE TABLE IF NOT EXISTS mastery_offers (
    offer_id      text PRIMARY KEY,
    user_id       text,
    item_type     text,
    mastery_tier  text,
    created_at    timestamp,
    options_json  text
);

CREATE TABLE IF NOT EXISTS mastery_unlocked (
    user_id      text,
    item_type    text,
    skill_id     text,
    skill_json   text,
    unlocked_at  timestamp,
    PRIMARY KEY ((user_id, item_type), skill_id)
);
