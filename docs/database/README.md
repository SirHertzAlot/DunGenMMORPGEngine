Database schemas, migrations, CQL and seeders for local development.

Single source of truth
- Scylla DDL lives in `services/authoritative/Services/PersistenceSchemaText.cs`
  and is mirrored 1:1 in `db/scylla/mmo_world.cql` and `db/migrations/`.
- `PersistenceContractTests` (services/authoritative/tests/PersistenceContractTests.cs)
  enforces schema <-> row-model lockstep for every table and asserts the canonical
  CQL/migration files stay in sync with the code. Keep all three in step.

Scylla / Cassandra (keyspace mmo_world)
- `db/scylla/mmo_world.cql` contains the keyspace and all tables used by the
  authoritative services. `ScyllaWorldPersistenceService` and
  `MasteryPersistenceService` bootstrap the same keyspace at startup (idempotent
  `CREATE TABLE IF NOT EXISTS`), so a fresh cluster needs no manual run.
- Tables:
  - dungeon_sessions  - one row per persisted world (session/execution/pipeline, seed, dimensions, counts)
  - dungeon_rooms     - room placements per session
  - dungeon_enemies   - enemy placements per session (archetype, x/y, level)
  - dungeon_loot      - loot placements per session (item_type, tier, x/y)
  - entity_snapshots  - latest JSON entity state per session/entity (for /v1/world/.../snapshots)
  - session_metadata  - arbitrary key/value session metadata map (for /v1/world/.../metadata)
  - mastery_offers    - generated mastery offers per offer id (options stored as JSON)
  - mastery_unlocked  - unlocked mastery skills per (user_id, item_type) pair

Postgres (mmodb)
- Run the SQL files in `db/migrations/` against your Postgres instance (mmodb).
  `MigrationHostedService` also runs them in filename order and records them in
  `schema_migrations`.
- Tables:
  - world_session_events - observability events ingested from clients; summary
    queries count `entity.state.snapshot` events and `system.%` events
  - agent_tasks         - agentic tool-use task queue (created by AgentTaskService)
  - character_parts     - modular character asset manifest; written ONLY by the
    seeder, no authoritative service reads it (CharacterGenerator uses the JSON
    catalog `Assets/Characters/character_parts_expanded.json`)
  - schema_migrations   - applied-migration ledger maintained by the runtime

Redis
- No application key families. Admin observability tooling performs generic
  GET/SET/DEL of arbitrary keys plus FLUSHDB/MEMORY PURGE/BGSAVE. See
  `redis-keys.md`.

Seeders
- `db/seeders/seed_character_parts.py` reads `Assets/Characters/character_parts_expanded.json`
  and upserts into Postgres `character_parts`.

Examples
Install seeder deps:
```
pip install -r Assets/DunGenMMORPGEngine/db/seeders/requirements.txt
```

Run migrations (psql):
```
psql "postgresql://mmouser:mmopass@localhost:5432/mmodb" -f Assets/DunGenMMORPGEngine/db/migrations/0001_create_world_session_events.sql
psql "postgresql://mmouser:mmopass@localhost:5432/mmodb" -f Assets/DunGenMMORPGEngine/db/migrations/0002_create_agent_tasks.sql
psql "postgresql://mmouser:mmopass@localhost:5432/mmodb" -f Assets/DunGenMMORPGEngine/db/migrations/0003_create_character_parts.sql
```

Run seeder:
```
python Assets/DunGenMMORPGEngine/db/seeders/seed_character_parts.py --db "Host=localhost;Port=5432;Username=mmouser;Password=mmopass;Database=mmodb"
```