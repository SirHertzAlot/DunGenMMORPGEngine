Redis usage in the authoritative backend

Redis (redis:6379) is used ONLY by the admin observability tooling
(`DatabaseObservabilityService`). There are no application-namespaced keys.
Key naming is not prescriptive: the admin CRUD endpoints accept and operate on
arbitrary trimmed key strings.

Operations
- GET key            -> reads a string value (`StringGet`)
- SET key value      -> writes a string value with an optional TTL (`StringSet`)
- DEL key            -> deletes the key (`KeyDelete`)
- Maintenance actions: FLUSHDB, MEMORY PURGE, BGSAVE

Notes
- Values are arbitrary small strings supplied by the admin tooling (e.g. flags,
  scratch state). Store large blobs in Scylla or Postgres instead.
- Prefer explicit TTLs on any ephemeral keys you set so they self-expire.
- No part of the game/generation runtime reads or writes Redis keys, so there
  are no `charpart:*`, `agent:task:*` or `session:active:*` key families.