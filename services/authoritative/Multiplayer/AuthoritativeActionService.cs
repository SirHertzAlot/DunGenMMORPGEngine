#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Authoritative.Domain;
using Authoritative.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Authoritative.Multiplayer
{
    public interface IAuthoritativeActionService
    {
        Task<AuthoritativeActionResponse> SubmitActionAsync(AuthoritativeActionRequest request, CancellationToken cancellationToken = default);
        Task<AuthoritativeGameStateDto?> GetStateAsync(string sessionId, CancellationToken cancellationToken = default);
        IReadOnlyCollection<AuthoritativeWorldEventDto> GetTimeline(string sessionId, int take = 50);
    }

    /// <summary>
    /// Owns per-session deterministic simulators. World sourcing (no pipeline
    /// side effects): headless latest job → executor in-memory executions →
    /// durable ScyllaDB world rows → deterministic local fallback keyed by the
    /// session id. Idempotent actionId replay plus lazy idle eviction.
    /// </summary>
    public sealed class AuthoritativeActionService : IAuthoritativeActionService
    {
        private sealed class SessionSlot
        {
            public required AuthoritativeSessionSimulator Simulator { get; init; }
            public ConcurrentDictionary<string, AuthoritativeActionResponse> Replays { get; } = new(StringComparer.Ordinal);
            public DateTime LastAccessUtc { get; set; } = DateTime.UtcNow;
        }

        private readonly ConcurrentDictionary<string, SessionSlot> _sessions = new(StringComparer.Ordinal);
        private readonly IHeadlessGeneratorService _generators;
        private readonly IPipelineExecutionService _executor;
        private readonly IScyllaWorldPersistenceService _scylla;
        private readonly TimeSpan _idleTimeout;
        private readonly ILogger<AuthoritativeActionService> _logger;

        public AuthoritativeActionService(
            IHeadlessGeneratorService generators,
            IPipelineExecutionService executor,
            IScyllaWorldPersistenceService scylla,
            IConfiguration configuration,
            ILogger<AuthoritativeActionService> logger)
        {
            _generators = generators;
            _executor = executor;
            _scylla = scylla;
            _logger = logger;

            int idleMinutes = 30;
            if (int.TryParse(configuration["AUTHORITATIVE_SESSION_IDLE_MINUTES"], out var configured) && configured > 0)
                idleMinutes = configured;
            _idleTimeout = TimeSpan.FromMinutes(idleMinutes);
        }

        public async Task<AuthoritativeActionResponse> SubmitActionAsync(
            AuthoritativeActionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.SessionId))
                return ClosedResponse("sessionId is required.");

            var slot = await GetOrCreateSlotAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
            if (slot == null || string.IsNullOrWhiteSpace(request.ActionId))
                return null == slot
                    ? ClosedResponse("No world is available for this session.")
                    : await RunActionAsync(slot, request, cancellationToken).ConfigureAwait(false);

            // Idempotent replay: a retried actionId returns the stored response
            // without mutating the session a second time.
            if (slot.Replays.TryGetValue(request.ActionId, out var replay))
            {
                slot.LastAccessUtc = DateTime.UtcNow;
                return replay;
            }

            var response = await RunActionAsync(slot, request, cancellationToken).ConfigureAwait(false);
            slot.Replays.TryAdd(request.ActionId, response);
            return response;
        }

        private static async Task<AuthoritativeActionResponse> RunActionAsync(
            SessionSlot slot,
            AuthoritativeActionRequest request,
            CancellationToken cancellationToken)
        {
            slot.LastAccessUtc = DateTime.UtcNow;
            await Task.Yield();

            AuthoritativeActionResponse response;
            lock (slot.Simulator.SyncRoot)
            {
                if (string.Equals(request.ActionType, AuthoritativeActionTypes.Attack, StringComparison.OrdinalIgnoreCase))
                {
                    response = slot.Simulator.ResolveTurn(request.ExpectedTurn);
                }
                else
                {
                    var outcome = slot.Simulator.QueueMove(request.DeltaX, request.DeltaY, request.ExpectedTurn);
                    var state = slot.Simulator.BuildState();
                    response = new AuthoritativeActionResponse
                    {
                        Accepted = outcome.Accepted,
                        Status = outcome.Status,
                        Message = outcome.Message,
                        Turn = state.Turn,
                        GameOver = state.GameOver,
                        GameOverReason = state.GameOverReason,
                        State = state
                    };
                }
            }

            return response;
        }

        public async Task<AuthoritativeGameStateDto?> GetStateAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            var slot = await GetOrCreateSlotAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (slot == null)
                return null;

            lock (slot.Simulator.SyncRoot)
            {
                slot.LastAccessUtc = DateTime.UtcNow;
                return slot.Simulator.BuildState();
            }
        }

        public IReadOnlyCollection<AuthoritativeWorldEventDto> GetTimeline(string sessionId, int take = 50)
        {
            if (_sessions.TryGetValue(sessionId, out var slot))
            {
                slot.LastAccessUtc = DateTime.UtcNow;
                lock (slot.Simulator.SyncRoot)
                {
                    return slot.Simulator.GetRecentEvents(take);
                }
            }

            return Array.Empty<AuthoritativeWorldEventDto>();
        }

        // ── Session lifecycle ────────────────────────────────────────────────

        private async Task<SessionSlot?> GetOrCreateSlotAsync(string sessionId, CancellationToken cancellationToken)
        {
            EvictIdleSessions();

            if (_sessions.TryGetValue(sessionId, out var existing))
            {
                existing.LastAccessUtc = DateTime.UtcNow;
                return existing;
            }

            var world = await TryBuildWorldAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (world == null)
            {
                _logger.LogWarning("No authoritative world could be sourced for session {SessionId}; using deterministic fallback seed.", sessionId);
                world = AuthoritativeWorldFactory.BuildFallback(Fnv1a(sessionId));
            }

            var slot = new SessionSlot { Simulator = new AuthoritativeSessionSimulator(world) };
            if (_sessions.TryAdd(sessionId, slot))
            {
                _logger.LogInformation(
                    "Authoritative session {SessionId} created from seed {Seed} ({Width}x{Height}, {RoomCount} rooms, {EnemyCount} enemies).",
                    sessionId, world.Seed, world.Width, world.Height, world.Rooms.Count, world.Enemies.Count);
                return slot;
            }

            return _sessions[sessionId];
        }

        private void EvictIdleSessions()
        {
            if (_sessions.IsEmpty)
                return;

            var cutoff = DateTime.UtcNow - _idleTimeout;
            foreach (var (sessionId, slot) in _sessions)
            {
                if (slot.LastAccessUtc < cutoff)
                {
                    if (_sessions.TryRemove(sessionId, out var removed))
                        _logger.LogInformation("Evicted idle authoritative session {SessionId}.", sessionId);
                }
            }
        }

        private async Task<GeneratedWorldArtifact?> TryBuildWorldAsync(string sessionId, CancellationToken cancellationToken)
        {
            try
            {
                var latestJob = _generators.GetLatestJobForSession(sessionId);
                var latestExecution = latestJob?.Execution ?? _executor.GetExecutions(50)
                    .OfType<PipelineExecutionRecord>()
                    .Where(x => string.Equals(x.SessionId, sessionId, StringComparison.Ordinal))
                    .OrderByDescending(x => x.CompletedAtUtc)
                    .FirstOrDefault();

                if (latestExecution != null && latestExecution.World.Rooms.Count > 0)
                    return latestExecution.World;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "In-memory world lookup failed for session {SessionId}.", sessionId);
            }

            try
            {
                var sessionRow = await _scylla.GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
                if (sessionRow != null)
                {
                    var roomsTask = _scylla.GetRoomsAsync(sessionId, cancellationToken);
                    var enemiesTask = _scylla.GetEnemiesAsync(sessionId, cancellationToken);
                    var lootTask = _scylla.GetLootAsync(sessionId, cancellationToken);
                    await Task.WhenAll(roomsTask, enemiesTask, lootTask).ConfigureAwait(false);

                    var world = new GeneratedWorldArtifact
                    {
                        Seed = sessionRow.Seed,
                        Width = sessionRow.Width,
                        Height = sessionRow.Height,
                        DungeonLevel = sessionRow.DungeonLevel,
                        Rooms = (await roomsTask.ConfigureAwait(false)).Select(r => new WorldRoom
                        {
                            Id = r.RoomId, X = r.X, Y = r.Y, Width = r.Width, Height = r.Height
                        }).ToList(),
                        Enemies = (await enemiesTask.ConfigureAwait(false)).Select(e => new WorldEnemy
                        {
                            Id = e.EnemyId, Archetype = e.Archetype, X = e.X, Y = e.Y, Level = e.Level
                        }).ToList(),
                        Loot = (await lootTask.ConfigureAwait(false)).Select(l => new WorldLoot
                        {
                            ItemId = l.ItemId, ItemType = l.ItemType, Tier = l.Tier, X = l.X, Y = l.Y
                        }).ToList()
                    };

                    if (world.Rooms.Count > 0)
                        return world;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ScyllaDB world lookup failed for session {SessionId}.", sessionId);
            }

            return null;
        }

        private static AuthoritativeActionResponse ClosedResponse(string message)
        {
            return new AuthoritativeActionResponse
            {
                Accepted = false,
                Status = AuthoritativeActionStatus.SessionUnavailable,
                Message = message,
                Turn = 0
            };
        }

        private static int Fnv1a(string value)
        {
            uint hash = 2166136261;
            foreach (char c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return unchecked((int)hash);
        }
    }

    /// <summary>
    /// Deterministic local world generator used when a session has no pipeline
    /// execution or stored world. No pipeline side effects: it intentionally
    /// bypasses the item store, produced purely from the session seed.
    /// </summary>
    public static class AuthoritativeWorldFactory
    {
        private static readonly string[] EnemyArchetypes = PersistenceTagCatalog.EnemyArchetypes;
        private static readonly string[] LootTiers = { "common", "rare", "epic", "legendary" };

        public static GeneratedWorldArtifact BuildFallback(int seed)
        {
            const int width = 80;
            const int height = 24;
            const int dungeonLevel = 1;

            var rng = new DeterministicRng((ulong)(uint)seed);
            var rooms = new List<WorldRoom>();
            int roomCount = Math.Clamp(Math.Max(4, (width * height) / 180), 4, 64);
            for (int i = 0; i < roomCount; i++)
            {
                int roomWidth = rng.Next(4, Math.Max(5, Math.Min(12, width)));
                int roomHeight = rng.Next(4, Math.Max(5, Math.Min(10, height)));
                int xBound = Math.Max(1, width - roomWidth);
                int yBound = Math.Max(1, height - roomHeight);

                rooms.Add(new WorldRoom
                {
                    Id = i + 1,
                    X = rng.Next(0, xBound),
                    Y = rng.Next(0, yBound),
                    Width = roomWidth,
                    Height = roomHeight
                });
            }

            var enemies = new List<WorldEnemy>();
            int enemyCount = 5 + rng.Next(0, 5);
            for (int i = 0; i < enemyCount; i++)
            {
                enemies.Add(new WorldEnemy
                {
                    Id = i + 1,
                    Archetype = EnemyArchetypes[rng.Next(EnemyArchetypes.Length)],
                    X = rng.Next(0, width),
                    Y = rng.Next(0, height),
                    Level = Math.Max(1, dungeonLevel + rng.Next(-1, 2))
                });
            }

            var loot = new List<WorldLoot>();
            int lootCount = 3 + rng.Next(0, 4);
            for (int i = 0; i < lootCount; i++)
            {
                loot.Add(new WorldLoot
                {
                    ItemId = $"item-{seed}-{i}",
                    ItemType = PersistenceTagCatalog.FallbackLootItemType,
                    Tier = LootTiers[rng.Next(LootTiers.Length)],
                    X = rng.Next(0, width),
                    Y = rng.Next(0, height)
                });
            }

            return new GeneratedWorldArtifact
            {
                Seed = seed,
                Width = width,
                Height = height,
                DungeonLevel = dungeonLevel,
                Rooms = rooms,
                Enemies = enemies,
                Loot = loot,
                TerrainMesh = new GeneratedTerrainMesh()
            };
        }
    }
}
#endif