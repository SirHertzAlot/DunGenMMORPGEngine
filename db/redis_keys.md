Redis key patterns used for caching and ephemeral state

- `charpart:{part_id}` -> Redis set of file paths for a given `part_id` (e.g. `charpart:hair`)
- `charpart:filemeta:{asset_path}` -> Hash storing metadata for an asset (gender, variant, priority, attachBone)
- `agent:task:{task_id}` -> Temporary task state for running agent tasks (optional)
- `session:active:{session_id}` -> Short-lived session state used by runtime

Notes:
- Use TTLs for ephemeral keys (sessions) and careful eviction policies for large sets.
- Prefer Redis hashes and small values; store large blobs in Scylla or S3.
