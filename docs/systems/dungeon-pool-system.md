# Dungeon Pool System - Ratio-Based Batch Generation

**Date**: May 11, 2026  
**Status**: ✅ Implemented and tested  
**Test Coverage**: 10 tests, all passing

## Overview

The dungeon pool system pre-generates dungeons on the backend and stores them in a pool, ready for clients to claim via REST API. Pool size scales dynamically based on active player count using a configurable generation ratio.

## Core Concept: Ratio-Based Scaling

```
Pool Target Size = Active Players × Generation Ratio

Example:
- 10 active players
- Ratio: 0.5 (one dungeon per 2 players)
- Target pool size: 5 dungeons
```

This ensures:
- **Scalability**: Pool grows/shrinks with player count
- **Efficiency**: No over-generation when few players active
- **Availability**: Always enough dungeons for concurrent players
- **ECS-Ready**: Pooled dungeons pre-generated for immediate client consumption

## Architecture

### Three Core Components

#### 1. DungeonPoolService (Backend)
- **File**: `services/authoritative/Services/DungeonPoolService.cs`
- **Responsibilities**:
  - Tracks active player sessions
  - Calculates ideal pool size based on ratio
  - Triggers batch generation when pool below target
  - Manages claim/cleanup lifecycle
  - Expires unclaimed dungeons after 1 hour

- **Key Methods**:
  - `RegisterSession(sessionId)` - Player joins
  - `UnregisterSession(sessionId)` - Player leaves
  - `ClaimDungeonAsync(difficultyLevel, ct)` - Client claims dungeon
  - `GenerateBatchAsync(level, count, ct)` - Generate batch for difficulty level
  - `SetGenerationRatio(ratio)` - Configure scaling (0-1.0)
  - `GetStatistics()` - Current pool metrics

#### 2. REST API Endpoints
- **Client Endpoints**:
  - `GET /v1/pool/status` - Pool statistics (active players, pool size, target)
  - `POST /v1/pool/claim` - Claim dungeon from pool for difficulty level

- **Admin Endpoints**:
  - `POST /admin/pool/config` - Set generation ratio
  - `POST /admin/pool/generate-batch` - Manually trigger batch generation

#### 3. Hosted Service
- Runs background refresh cycle every 1 minute
- Monitors active player count
- Detects pool deficit
- Triggers batch generation across difficulty levels 1-10
- Cleans up expired unclaimed dungeons

## Data Structures

### PooledDungeon
```csharp
public sealed class PooledDungeon
{
    public string PoolId { get; set; }              // Unique pool identifier
    public string ExecutionId { get; set; }         // Backend generation job ID
    public int DifficultyLevel { get; set; }        // 1-10
    public int Seed { get; set; }                   // Deterministic generation
    public int Width { get; set; }                  // Dungeon width
    public int Height { get; set; }                 // Dungeon height
    public int RoomCount { get; set; }              // Precomputed
    public int EnemyCount { get; set; }             // Precomputed
    public int LootCount { get; set; }              // Precomputed
    public PoolStatus Status { get; set; }          // Available/Claimed/Expired/Failed
    public DateTime CreatedAt { get; set; }         // For expiration logic
    public DateTime? ClaimedAt { get; set; }        // Claim timestamp
    public string? ClaimedBy { get; set; }          // Optional: claiming player ID
}
```

### PoolStatistics
```csharp
public sealed class PoolStatistics
{
    public int ActiveSessions { get; set; }           // Current player count
    public int PoolSize { get; set; }                 // Available dungeons now
    public int TargetPoolSize { get; set; }           // Target based on ratio
    public double GenerationRatio { get; set; }       // Current ratio (0-1.0)
    public Dictionary<int, int> PoolByDifficulty { get; set; }  // Breakdown per level
    public DateTime LastGenerationTime { get; set; }  // Last batch generation
    public int TotalClaimed { get; set; }             // Lifetime claims
}
```

## Generation Strategy

### Batch Generation
- Triggered when `PoolSize < TargetPoolSize`
- Distributes deficit evenly across difficulty levels 1-10
- Example: If deficit is 10 dungeons, generate 1 dungeon per level
- Uses HeadlessGeneratorService to create ECS-compatible dungeons
- Each dungeon in batch has unique seed for variety

### Expiration Policy
- Unclaimed pooled dungeons expire after 1 hour
- Claimed dungeons can be kept indefinitely by clients
- Expired dungeons automatically removed from pool
- Cleanup runs during refresh cycle

## Usage Patterns

### Client: Claim a Dungeon
```bash
POST /v1/pool/claim?difficultyLevel=5

Response:
{
  "poolId": "pool_5_abc123",
  "executionId": "exec_xyz789",
  "difficultyLevel": 5,
  "seed": 42,
  "width": 60,
  "height": 40,
  "roomCount": 8,
  "enemyCount": 10,
  "lootCount": 5,
  "claimedAt": "2026-05-11T12:00:00Z"
}
```

### Admin: Configure Ratio
```bash
POST /admin/pool/config?generationRatio=0.75

Response:
{
  "message": "Generation ratio set to 0.75",
  "stats": {
    "activeCount": 20,
    "poolSize": 12,
    "targetPoolSize": 15,
    "generationRatio": 0.75
  }
}
```

### Monitor: Check Pool Status
```bash
GET /v1/pool/status

Response:
{
  "activeSessions": 20,
  "poolSize": 14,
  "targetPoolSize": 15,
  "generationRatio": 0.5,
  "poolByDifficulty": {
    "1": 2,
    "2": 2,
    "3": 2,
    "4": 2,
    "5": 3,
    "6": 1,
    "7": 0,
    "8": 0,
    "9": 0,
    "10": 0
  },
  "lastGenerationTime": "2026-05-11T12:00:00Z",
  "totalClaimed": 45
}
```

## ECS Integration

### Pre-Generated for Immediate Use
1. Backend generates dungeon with full ECS world (rooms, enemies, loot, ECS entities)
2. Dungeon stored in pool with all metadata
3. Client claims dungeon and receives:
   - Pool metadata (PoolId, ExecutionId, dimensions)
   - Full world snapshot available via `/v1/world/sessions/{sessionId}/` endpoint
   - Can immediately spawn ECS entities for the dungeon

### Client Flow
```
1. Client claims dungeon: POST /v1/pool/claim?level=5
2. Backend returns PooledDungeonClaimResult
3. Client queries world: GET /v1/world/sessions/{poolId}/snapshot
4. Client deserializes rooms/enemies/loot into ECS entities
5. Game starts with fully populated dungeon
```

## Configuration

### Default Settings
- **Generation Ratio**: 0.5 (1 dungeon per 2 active players)
- **Minimum Pool Size**: 1 (always have something available)
- **Refresh Interval**: 1 minute
- **Dungeon Expiration**: 1 hour
- **Difficulty Levels**: 1-10 (distributed evenly)

### Tuning for Load

**Light Load** (< 50 players):
- Ratio: 0.3 (1 dungeon per 3.3 players)
- Command: `POST /admin/pool/config?generationRatio=0.3`

**Medium Load** (50-500 players):
- Ratio: 0.5 (1 dungeon per 2 players) - DEFAULT

**Heavy Load** (500+ players):
- Ratio: 0.75 (1 dungeon per 1.3 players)
- Command: `POST /admin/pool/config?generationRatio=0.75`

## Test Coverage (10 tests)

✅ **Registration/Unregistration**
- Session tracking increases/decreases active count
- Pool statistics reflect current active sessions

✅ **Ratio Calculation**
- Target pool size = active × ratio
- Different ratios scale correctly (0.1, 0.5, 1.0)
- Minimum pool size of 1 maintained

✅ **Configuration**
- SetGenerationRatio validates range (0-1.0)
- Invalid ratios throw ArgumentException

✅ **Claiming**
- Claim from empty pool returns null
- Claim from available pool marks as claimed

✅ **Generation**
- GenerateBatchAsync creates multiple dungeons
- LastGenerationTime updated after batch

## Performance Notes

- In-memory pool for < 1ms claim latency
- Background generation doesn't block REST requests
- Pool cleanup O(n) where n = pool size (acceptable < 1000)
- Each dungeon ~5KB metadata (rooms, enemies, loot lists)
- Full world data stored in ScyllaDB separately

## Integration Points

1. **HeadlessGeneratorService**: Provides dungeon generation
2. **IScyllaWorldPersistenceService**: Persists claimed dungeons
3. **Program.cs**: Registers service, defines REST endpoints
4. **Session Management**: Hook into player join/leave events

## Future Enhancements

- Persistent pool state to ScyllaDB (survive service restart)
- Pre-warming: generate at startup before clients connect
- Difficulty distribution: prioritize high-level dungeons for endgame
- Analytics: track claim patterns, predict demand
- Caching: Redis for ultra-fast claims
