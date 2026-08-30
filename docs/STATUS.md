# STATUS — DunGenMMORPGEngine (Single Source of Truth)

Last verified: 2026-08-30. This is the authoritative status document. Anything not in this
file (or linked from it) is not an engine claim. Historical/roadmap docs were moved or
removed on 2026-08-30; the wiki home lives at [`docs/index.md`](index.md).

## 1. What the project is

A deterministic, server-authoritative, procedurally generated MMORPG engine: a Unity client
(`projects/5`), an ASP.NET Core 10 backend (`services/authoritative`), and supporting infra
(Postgres, Redis, Scylla, RabbitMQ, Prometheus, Grafana) orchestrated by `docker-compose.yml`,
with GitHub Actions CI.

## 2. Verified facts (2026-08-30)

Commands below are run from `Assets/DunGenMMORPGEngine`.

- **Backend builds with 0 warnings / 0 errors.**
  `dotnet build services\authoritative\Authoritative.csproj -c Debug --nologo -v q`
  -> `Build succeeded. 0 Warning(s), 0 Error(s)`.

- **Backend test suite is green: 81 passed, 0 failed, ~2 s.**
  `dotnet test services\authoritative\tests\Authoritative.Tests.csproj -c Debug --nologo -v q`
  -> `Passed! Failed: 0, Passed: 81, Skipped: 0, Total: 81, Duration: 2 s`.
  Includes golden RNG parity tests, authoritative-simulator tests, persistence
  schema-vs-model contract tests, and GraphQL schema tests.

- **`GET /healthz` exists** (`services/authoritative/Program.cs`, `app.MapGet("/healthz" ...)`).

- **Deterministic backend RNG unification DONE.** All backend `System.Random` usage removed;
  `Multiplayer/DeterministicRng.cs` mirrors the Unity twin. Golden parity sequences locked in
  `tests/DeterministicRngTests.cs`: seed 42 d20[0..7] = `12,5,9,13,14,1,1,4`;
  seed 42 next(6,18) = `12,8,10,13,14,6`; seed 42 next(5) = `2,1,2,3,3`; seed 7 3d6 = `15,5`.

- **Server-authoritative action loop IMPLEMENTED (server side).** Routes in `Program.cs`
  (~line 1149): `POST /v1/actions/{sessionId}`, `GET /v1/actions/{sessionId}/state`,
  `GET /v1/actions/{sessionId}/timeline`. In-memory simulator mirrors the Unity `GameSession`
  rules (movement budget/blocked/occupied/duplicate/stale, combat steps, XP/level-loot,
  deterministic RNG draw order). E2E two-client validation in Unity is still PENDING (manual).

- **GraphQL IMPLEMENTED (server side).** `POST /graphql` (HotChocolate). Queries:
  `sessions`, `sessionState`, `sessionRooms`, `sessionEnemies`, `sessionLoot`, `events` (paged);
  mutation: `submitAction`. DataLoaders batch per-session child loads (N+1 test passes:
  20 sessions -> 1 batch per loader). Same client-security gate as `/client`; introspection
  disabled outside dev.

- **Persistence schema aligns with data models.** Scylla tables (incl. the previously
  missing `entity_snapshots`/`session_metadata` DDL — a real runtime bug, now fixed),
  `world_session_events`, `agent_tasks`; DDL/mapper/tag contract tests enforce lockstep.
  Canonical CQL: `db/scylla/mmo_world.cql`. DB credentials come from env with a
  `DevCredentials` dev-only fallback.

- **Docker healthchecks are real.** No `exit 0` no-ops; authoritative services curl
  `/healthz`, Scylla/Redis/Postgres/RabbitMQ/prometheus/grafana exporters have real probes.
  `docker compose config` parses cleanly.

- **Backend CI added** (`.github/workflows/main.yml`): `backend-build-test` job runs
  `dotnet build` + `dotnet test` on ubuntu-latest. Unity job unchanged.

- **Docs restructured.** Wiki home at `docs/index.md`; stale/duplicate .md files removed;
  working docs moved under `docs/`; dangerous/obsolete `verify_ecs_refactoring.sh` deleted.

- **Unity EditMode tests = 56 `[Test]` methods across 9 files** (source count in
  `projects/5/Assets/Tests/Editor`). A headless Unity run was NOT possible on this machine
  (no Unity license) — authored count, not a pass claim.

## 3. World state and known open items

| Item | Status |
|------|--------|
| Unity: authoritative forward-mode on the client (`AuthoritativeSessionClient` submit + `GameSession` apply-server-state) | **PENDING** — not implemented this session |
| Unity: walkability fix (rooms/corridors wall grid in `SimpleDungeonGenerator`; stop ignoring walls) | **PENDING** — not implemented this session |
| Unity: generator RNG swap to `DeterministicRNG` (client twin of the backend) | **PENDING** |
| Unity: `ConfigLoader` wired into gameplay defaults | **PENDING** |
| Unity EditMode tests for the above | **PENDING** |
| Backend authoritative loop implemented + tested | DONE (server side) |
| GraphQL server layer + DataLoaders | DONE |
| DB schema ↔ data-model consistency (Scylla/Postgres) | DONE |
| Deterministic RNG unified (backend) w/ golden parity tests | DONE |
| Docker real healthchecks, backend CI | DONE |
| EIT / EAT security design (pre-alpha plan) | PLANNED — see `docs/security/eit-eat-plan.md` |

## 4. Corrected claims (were false in previous docs)

- "Authoritative backend is real" — previously did not compile; now compiles 0/0 and runs 81 tests.
- "Backend tests pass 42/42" — that batch had never run (compile-broken code + `Random` nondeterminism); the real, current number is 81 passing in this session.
- Hardcoded alpha-gate counts (122/122, 133/133, 6/6, 22/22, 11/11) were contradicted by source — dropped.
- "Server-authoritative loop exists" — REST only existed before; the authoritative action loop is new this session.
- "asmdefs exist" — no project asmdefs; claim dropped.
- "Client authoritative forward-mode already implemented" (from an earlier session note about `ReactAdminPanelLauncher.cs` edits) — **incorrect**: the Unity client work is not done; see PENDING items above.

## 5. API contract (authoritative backend — implemented)

### POST /v1/actions/{sessionId}
Request: `{ "actionId": string, "actionType": "move"|"attack", "deltaX": int, "deltaY": int, "expectedTurn": int, "sourcePlayerId": string }`
Response 200: `{ "accepted": bool, "status": "accepted"|"invalid"|"stale"|"duplicate"|"blocked"|"occupied"|"session_unavailable", "message": string, "turn": int, "gameOver": bool, "state": AuthoritativeStateDto|null }`

### GET /v1/actions/{sessionId}/state
`{ sessionId, seed, turn, playerX, playerY, playerHealth, playerMaxHealth, playerLevel, playerXp, playerGold, livingEnemies, isGameOver, enemies: [{ entityId, archetype, x, y, health, maxHealth, inCombat, level }], recentEvents: [{ eventId, type, turn, message }] }`

### GET /v1/actions/{sessionId}/timeline?take=n
Array of the same event shape.

### GraphQL — POST /graphql
Queries: `sessions(first,after)` (paged), `sessionState(sessionId)`, `sessionRooms(sessionId)`,
`sessionEnemies(sessionId)`, `sessionLoot(sessionId)`, `events(sessionId, first, after)` (paged).
Mutation: `submitAction(input: { actionId, actionType, deltaX, deltaY, expectedTurn, sourcePlayerId, sessionId })`
-> `{ accepted, status, message, turn, gameOver, state }`.

## 6. How to verify

```bash
dotnet build services\authoritative\Authoritative.csproj -c Debug --nologo -v q
dotnet test  services\authoritative\tests\Authoritative.Tests.csproj -c Debug --nologo -v q
docker compose config          # compose must parse; healthchecks now real
curl -fsS http://localhost:8081/healthz   # authoritative-primary
```
Unity EditMode/PlayMode runs need a licensed Unity Editor on the machine; those remain the
gate before any "alpha green" declaration.

## 7. Roadmap

- Pre-alpha security: implement EIT/EAT (see `docs/security/eit-eat-plan.md`).
- Finish the Unity client workstreams in the open-items table, then run two-client E2E.