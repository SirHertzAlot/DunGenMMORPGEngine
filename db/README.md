Database migrations, CQL and seeders for local development.

Postgres migrations
- Run the SQL files in `db/migrations/` against your Postgres instance (mmodb).

Scylla CQL
- `db/scylla/mmo_world.cql` contains the keyspace and tables used by `ScyllaWorldPersistenceService`.

Seeders
- `db/seeders/seed_character_parts.py` reads `Assets/Characters/character_parts_expanded.json` and upserts into Postgres `character_parts`.

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
