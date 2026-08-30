Snapshot API examples

GET snapshot

curl -sS -X GET "http://localhost:5000/v1/world/sessions/{sessionId}/snapshots/{entityId}"

POST snapshot

curl -sS -X POST "http://localhost:5000/v1/world/sessions/{sessionId}/snapshots/{entityId}" \
  -H "Content-Type: application/json" \
  -d '{ "EntityType": "enemy", "SnapshotJson": "{ \"id\":1, \"x\":10 }" }'

POST metadata

curl -sS -X POST "http://localhost:5000/v1/world/sessions/{sessionId}/metadata" \
  -H "Content-Type: application/json" \
  -d '{ "mapName": "dungeon-1", "difficulty": "normal" }'

Notes
- To request a TTL from the client, include header `X-Snapshot-TTL: <seconds>`; the sender helper sets this header when `ttlSeconds` is provided. The service currently supports TTL when provided.
- Snapshots are treated as opaque JSON blobs; the authoritative service does not validate structure.
 - To request a specific snapshot `version`, include header `X-Snapshot-Version: <int>`; defaults to `1`.
