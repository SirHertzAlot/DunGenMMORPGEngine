# DunGenMMORPGEngine — Wiki Home

A deterministic, server-authoritative, procedurally generated MMORPG engine.

- **Client**: Unity (`projects/5`) — DOTS-style ECS simulation, generated dungeons, combat.
- **Backend**: ASP.NET Core 10 (`services/authoritative`) — world generation, persistence, the authoritative action loop, REST + GraphQL APIs.
- **Infra**: Postgres, Redis, Scylla, RabbitMQ, Prometheus, Grafana (`docker-compose.yml`), GitHub Actions CI.

This folder is the wiki. Start below; every page here is maintained. Anything not reachable from here is not an engine claim.

## Navigation

| Area | Page |
|------|------|
| **Status** — single source of truth, verified facts, corrected claims | [`STATUS.md`](STATUS.md) |
| **Run locally** — docker stack + dev instructions | [`how-to-run-locally.md`](how-to-run-locally.md) |
| **Testing** — backend + Unity EditMode suites, how to run | [`testing-guide.md`](testing-guide.md) |
| **API** — REST endpoints (incl. the authoritative action API) | [`api/snapshot-api.md`](api/snapshot-api.md) |
| **Database** — keyspaces/tables, keys, canonical CQL | [`database/README.md`](database/README.md), [`database/redis-keys.md`](database/redis-keys.md) |
| **Systems** — how the engine is built | [`systems/dungeon-pool-system.md`](systems/dungeon-pool-system.md), [`systems/dungeon-runtime-integration.md`](systems/dungeon-runtime-integration.md), [`systems/data-oriented-refactoring.md`](systems/data-oriented-refactoring.md) |
| **Security roadmap** — EIT/EAT token design for pre-alpha | [`security/eit-eat-plan.md`](security/eit-eat-plan.md) |

## One-paragraph summary (2026-08-30)

The backend now compiles clean (0 warnings/0 errors) and its test suite is green
(`dotnet test` — 81 passed, ~2 s). It has a real deterministic RNG (`DeterministicRng`,
golden parity against the Unity twin), a server-authoritative action loop
(`POST /v1/actions/{sessionId}` + state/timeline GETs), schema-aligned persistence
(Scylla/Postgres DDL locked to the data models by contract tests), real docker healthchecks,
backend CI, and a GraphQL layer (`/graphql`) with DataLoader batching. The Unity client side
(forward-authoritative mode, generator/walkability, config wiring) is implemented but still
needs a manual two-client run in Unity to be declared passing.

See [`STATUS.md`](STATUS.md) for the verified truth table and exact commands.

## Quick verify

```bash
dotnet build services\authoritative\Authoritative.csproj -c Debug --nologo -v q
dotnet test  services\authoritative\tests\Authoritative.Tests.csproj -c Debug --nologo -v q
docker compose config
```