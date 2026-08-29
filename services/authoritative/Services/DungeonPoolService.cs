#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Authoritative.Services
{
    /// <summary>
    /// Manages a pool of pre-generated dungeons with ratio-based batch generation.
    /// 
    /// Pool Size = Active Players × GenerationRatio
    /// Example: 10 active players with ratio 0.5 = pool target of 5 dungeons
    /// 
    /// The pool is replenished periodically and clients claim dungeons via REST API.
    /// Pooled dungeons are ECS-compatible and ready for immediate client consumption.
    /// </summary>
    public interface IDungeonPoolService
    {
        /// <summary>Claims a dungeon from the pool for a specific difficulty level.</summary>
        Task<PooledDungeonClaimResult?> ClaimDungeonAsync(int difficultyLevel, CancellationToken ct);

        /// <summary>Gets current pool statistics.</summary>
        PoolStatistics GetStatistics();

        /// <summary>Gets the current generation ratio.</summary>
        double GetGenerationRatio();

        /// <summary>Sets the generation ratio (e.g., 0.5 means 1 dungeon per 2 active players).</summary>
        void SetGenerationRatio(double ratio);

        /// <summary>Manually trigger batch generation.</summary>
        Task GenerateBatchAsync(int difficultyLevel, int count, CancellationToken ct);

        /// <summary>Register an active session (used for player count calculation).</summary>
        void RegisterSession(string sessionId);

        /// <summary>Unregister a session when player disconnects.</summary>
        void UnregisterSession(string sessionId);
    }

    public sealed class PooledDungeonClaimResult
    {
        public string PoolId { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public int DifficultyLevel { get; set; }
        public int Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int RoomCount { get; set; }
        public int EnemyCount { get; set; }
        public int LootCount { get; set; }
        public DateTime ClaimedAt { get; set; }
    }

    public sealed class PoolStatistics
    {
        public int ActiveSessions { get; set; }
        public int PoolSize { get; set; }
        public int TargetPoolSize { get; set; }
        public double GenerationRatio { get; set; }
        public Dictionary<int, int> PoolByDifficulty { get; set; } = new();
        public DateTime LastGenerationTime { get; set; }
        public int TotalClaimed { get; set; }
    }

    public sealed class PooledDungeon
    {
        public string PoolId { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public int DifficultyLevel { get; set; }
        public int Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int RoomCount { get; set; }
        public int EnemyCount { get; set; }
        public int LootCount { get; set; }
        public PoolStatus Status { get; set; } = PoolStatus.Available;
        public DateTime CreatedAt { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public string? ClaimedBy { get; set; }
    }

    public enum PoolStatus
    {
        Available,
        Claimed,
        Expired,
        Failed
    }

    // ────────────────────────────────────────────────────────────────────────

    public sealed class DungeonPoolService : BackgroundService, IDungeonPoolService
    {
        private readonly IHeadlessGeneratorService _generatorService;
        private readonly IScyllaWorldPersistenceService _persistence;
        private readonly ILogger<DungeonPoolService> _log;
        private readonly TimeSpan _poolRefreshInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _dungeonExpiration = TimeSpan.FromHours(1);

        // In-memory tracking
        private readonly ConcurrentDictionary<string, PooledDungeon> _pool;
        private readonly ConcurrentHashSet<string> _activeSessions;
        private double _generationRatio = 0.5; // 1 dungeon per 2 active players by default
        private int _totalClaimed = 0;
        private DateTime _lastGenerationTime = DateTime.UtcNow;

        public DungeonPoolService(
            IHeadlessGeneratorService generatorService,
            IScyllaWorldPersistenceService persistence,
            ILogger<DungeonPoolService> log)
        {
            _generatorService = generatorService;
            _persistence = persistence;
            _log = log;
            _pool = new ConcurrentDictionary<string, PooledDungeon>();
            _activeSessions = new ConcurrentHashSet<string>();
        }

        public void RegisterSession(string sessionId)
        {
            _activeSessions.Add(sessionId);
            _log.LogDebug("Session registered: {SessionId}. Active: {Count}", sessionId, _activeSessions.Count);
        }

        public void UnregisterSession(string sessionId)
        {
            _activeSessions.TryRemove(sessionId);
            _log.LogDebug("Session unregistered: {SessionId}. Active: {Count}", sessionId, _activeSessions.Count);
        }

        public double GetGenerationRatio()
        {
            return _generationRatio;
        }

        public void SetGenerationRatio(double ratio)
        {
            if (ratio <= 0 || ratio > 1.0)
                throw new ArgumentException("Generation ratio must be between 0 and 1");
            _generationRatio = ratio;
            _log.LogInformation("Generation ratio updated to {Ratio}", ratio);
        }

        public PoolStatistics GetStatistics()
        {
            var byDifficulty = new Dictionary<int, int>();
            for (int level = 1; level <= 10; level++)
            {
                byDifficulty[level] = _pool.Count(kvp => kvp.Value.DifficultyLevel == level && kvp.Value.Status == PoolStatus.Available);
            }

            int activeCount = _activeSessions.Count;
            int targetPoolSize = Math.Max(1, (int)Math.Ceiling(activeCount * _generationRatio));

            return new PoolStatistics
            {
                ActiveSessions = activeCount,
                PoolSize = _pool.Count(kvp => kvp.Value.Status == PoolStatus.Available),
                TargetPoolSize = targetPoolSize,
                GenerationRatio = _generationRatio,
                PoolByDifficulty = byDifficulty,
                LastGenerationTime = _lastGenerationTime,
                TotalClaimed = _totalClaimed
            };
        }

        public async Task<PooledDungeonClaimResult?> ClaimDungeonAsync(int difficultyLevel, CancellationToken ct)
        {
            // Find first available dungeon at this difficulty level
            var pooledDungeon = _pool.Values
                .FirstOrDefault(d => d.DifficultyLevel == difficultyLevel && d.Status == PoolStatus.Available);

            if (pooledDungeon == null)
            {
                _log.LogWarning("No available dungeon in pool for difficulty level {Level}", difficultyLevel);
                return null;
            }

            // Mark as claimed
            pooledDungeon.Status = PoolStatus.Claimed;
            pooledDungeon.ClaimedAt = DateTime.UtcNow;
            Interlocked.Increment(ref _totalClaimed);

            _log.LogInformation("Dungeon claimed from pool: {PoolId} (Level {Level})", pooledDungeon.PoolId, difficultyLevel);

            return new PooledDungeonClaimResult
            {
                PoolId = pooledDungeon.PoolId,
                ExecutionId = pooledDungeon.ExecutionId,
                DifficultyLevel = pooledDungeon.DifficultyLevel,
                Seed = pooledDungeon.Seed,
                Width = pooledDungeon.Width,
                Height = pooledDungeon.Height,
                RoomCount = pooledDungeon.RoomCount,
                EnemyCount = pooledDungeon.EnemyCount,
                LootCount = pooledDungeon.LootCount,
                ClaimedAt = pooledDungeon.ClaimedAt ?? DateTime.UtcNow
            };
        }

        public async Task GenerateBatchAsync(int difficultyLevel, int count, CancellationToken ct)
        {
            _log.LogInformation("Generating batch of {Count} dungeons at level {Level}", count, difficultyLevel);

            // Get a runtime snapshot for generation
            var snapshot = new PipelineRuntimeSnapshot
            {
                IsLoaded = true,
                ActiveDefinition = new PipelineDefinition
                {
                    PipelineId = $"pool_batch_{Guid.NewGuid():N}",
                    RequestId = $"pool_req_{Guid.NewGuid():N}",
                    Ecs = new EcsGenerationConfig
                    {
                        DungeonLevel = difficultyLevel,
                        Width = 60,
                        Height = 40,
                        EnemyCount = Math.Max(4, difficultyLevel),
                        LootCount = Math.Max(2, difficultyLevel / 2)
                    },
                    Steps =
                    {
                        new PipelineStepDefinition { Stage = "layout", EcsSystem = "DungeonGeneratorSystem", Enabled = true },
                        new PipelineStepDefinition { Stage = "encounters", EcsSystem = "EncounterSpawnSystem", Enabled = true },
                        new PipelineStepDefinition { Stage = "loot", EcsSystem = "LootPlacementSystem", Enabled = true }
                    }
                }
            };

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var seed = (int)(DateTime.UtcNow.Ticks % int.MaxValue) + i;
                    var job = _generatorService.CreateJob(snapshot, new GeneratorJobRequest
                    {
                        SessionId = $"pool_{difficultyLevel}_{i}_{Guid.NewGuid():N}",
                        RequestedBy = "pool-service",
                        SeedOverride = seed
                    });

                    if (job.Status == "completed" && job.Execution?.World != null)
                    {
                        var poolId = $"pool_{difficultyLevel}_{Guid.NewGuid():N}";
                        var pooled = new PooledDungeon
                        {
                            PoolId = poolId,
                            ExecutionId = job.Execution.ExecutionId,
                            DifficultyLevel = difficultyLevel,
                            Seed = seed,
                            Width = job.Execution.World.Width,
                            Height = job.Execution.World.Height,
                            RoomCount = job.Execution.World.Rooms.Count,
                            EnemyCount = job.Execution.World.Enemies.Count,
                            LootCount = job.Execution.World.Loot.Count,
                            Status = PoolStatus.Available,
                            CreatedAt = DateTime.UtcNow
                        };

                        _pool.TryAdd(poolId, pooled);
                        _log.LogDebug("Added dungeon to pool: {PoolId}", poolId);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to generate dungeon for pool batch");
                }
            }

            _lastGenerationTime = DateTime.UtcNow;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("DungeonPoolService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_poolRefreshInterval, stoppingToken);

                    // Cleanup expired dungeons
                    CleanupExpired();

                    // Check if we need to generate more
                    var stats = GetStatistics();
                    if (stats.PoolSize < stats.TargetPoolSize)
                    {
                        int deficit = stats.TargetPoolSize - stats.PoolSize;
                        _log.LogInformation("Pool deficit detected. Current: {Current}, Target: {Target}. Generating {Deficit} dungeons",
                            stats.PoolSize, stats.TargetPoolSize, deficit);

                        // Distribute deficit across difficulty levels based on active sessions
                        for (int level = 1; level <= 10; level++)
                        {
                            int levelDeficit = (int)Math.Ceiling((double)deficit / 10);
                            if (levelDeficit > 0)
                            {
                                await GenerateBatchAsync(level, levelDeficit, stoppingToken);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Error in pool refresh cycle");
                }
            }

            _log.LogInformation("DungeonPoolService stopped");
        }

        private void CleanupExpired()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _pool
                .Where(kvp => (now - kvp.Value.CreatedAt) > _dungeonExpiration && kvp.Value.Status != PoolStatus.Claimed)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                if (_pool.TryRemove(key, out var removed))
                {
                    _log.LogDebug("Removed expired dungeon from pool: {PoolId}", key);
                }
            }
        }
    }

    // ── Helper class for concurrent hash set ─────────────────────────────────
    public sealed class ConcurrentHashSet<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> _dict = new();

        public void Add(T item) => _dict.TryAdd(item, 0);
        public bool TryRemove(T item) => _dict.TryRemove(item, out _);
        public int Count => _dict.Count;
        public IEnumerable<T> Items => _dict.Keys;
    }
}
#endif
