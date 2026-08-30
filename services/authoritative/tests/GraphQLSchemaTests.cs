using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Authoritative.GraphQL;
using Authoritative.Multiplayer;
using Authoritative.Services;
using HotChocolate;
using HotChocolate.CostAnalysis;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

#if !UNITY_5_3_OR_NEWER
#if UNITY_5_3_OR_NEWER
using Assert = NUnit.Framework.Assert;
using FactAttribute = NUnit.Framework.TestAttribute;
#else
using Assert = Xunit.Assert;
using FactAttribute = Xunit.FactAttribute;
#endif

namespace Authoritative.Tests
{
    public class GraphQLSchemaTests
    {
        private sealed class StubActionService : IAuthoritativeActionService
        {
            private readonly Dictionary<string, AuthoritativeGameStateDto> _states;
            public StubActionService(Dictionary<string, AuthoritativeGameStateDto> states)
                => _states = states;

            public Task<AuthoritativeActionResponse> SubmitActionAsync(AuthoritativeActionRequest request, CancellationToken cancellationToken = default)
            {
                var state = _states.TryGetValue(request.SessionId, out var s) ? s : null;
                return Task.FromResult(new AuthoritativeActionResponse
                {
                    Accepted = true,
                    Status = AuthoritativeActionStatus.Accepted,
                    Message = "ok",
                    Turn = state?.Turn ?? 1,
                    GameOver = state?.GameOver ?? false,
                    State = state
                });
            }

            public Task<AuthoritativeGameStateDto?> GetStateAsync(string sessionId, CancellationToken cancellationToken = default)
                => Task.FromResult(_states.TryGetValue(sessionId, out var s) ? s : null);

            public IReadOnlyCollection<AuthoritativeWorldEventDto> GetTimeline(string sessionId, int take = 50)
                => Array.Empty<AuthoritativeWorldEventDto>();
        }

        private sealed class StubScyllaService : IScyllaWorldPersistenceService
        {
            private readonly Dictionary<string, IReadOnlyList<WorldRoomRow>> _rooms;
            private readonly Dictionary<string, IReadOnlyList<WorldEnemyRow>> _enemies;
            private readonly Dictionary<string, IReadOnlyList<WorldLootRow>> _loot;
            public readonly List<string> SessionIds = new();
            public long EnemiesQueries;
            public long RoomsQueries;
            public long LootQueries;

            public StubScyllaService(
                Dictionary<string, IReadOnlyList<WorldRoomRow>> rooms,
                Dictionary<string, IReadOnlyList<WorldEnemyRow>> enemies,
                Dictionary<string, IReadOnlyList<WorldLootRow>> loot)
            {
                _rooms = rooms;
                _enemies = enemies;
                _loot = loot;
            }

            public void EnqueueWorld(PipelineExecutionRecord record) { }

            public Task<WorldSessionRow?> GetSessionAsync(string sessionId, CancellationToken ct)
                => Task.FromResult(SessionIds.Contains(sessionId)
                    ? new WorldSessionRow { SessionId = sessionId, Seed = 1 }
                    : (WorldSessionRow?)null);

            public Task<IReadOnlyList<WorldRoomRow>> GetRoomsAsync(string sessionId, CancellationToken ct)
            {
                Interlocked.Increment(ref RoomsQueries);
                return Task.FromResult(_rooms.TryGetValue(sessionId, out var r) ? r : (IReadOnlyList<WorldRoomRow>)Array.Empty<WorldRoomRow>());
            }

            public Task<IReadOnlyList<WorldEnemyRow>> GetEnemiesAsync(string sessionId, CancellationToken ct)
            {
                Interlocked.Increment(ref EnemiesQueries);
                return Task.FromResult(_enemies.TryGetValue(sessionId, out var e) ? e : (IReadOnlyList<WorldEnemyRow>)Array.Empty<WorldEnemyRow>());
            }

            public Task<IReadOnlyList<WorldLootRow>> GetLootAsync(string sessionId, CancellationToken ct)
            {
                Interlocked.Increment(ref LootQueries);
                return Task.FromResult(_loot.TryGetValue(sessionId, out var l) ? l : (IReadOnlyList<WorldLootRow>)Array.Empty<WorldLootRow>());
            }

            public Task<string?> GetEntitySnapshotAsync(string sessionId, string entityId, CancellationToken ct)
                => Task.FromResult<string?>(null);

            public Task<Dictionary<string, string>?> GetSessionMetadataAsync(string sessionId, CancellationToken ct)
                => Task.FromResult<Dictionary<string, string>?>(null);

            public Task<bool> InsertEntitySnapshotAsync(string sessionId, string entityId, string entityType, string stateJson, int version = 1, int ttlSeconds = 0, CancellationToken ct = default)
                => Task.FromResult(true);

            public Task<bool> UpsertSessionMetadataAsync(string sessionId, IDictionary<string, string> properties, CancellationToken ct)
                => Task.FromResult(true);

            public Task<IReadOnlyList<string>> GetAllSessionIdsAsync(CancellationToken ct)
                => Task.FromResult((IReadOnlyList<string>)SessionIds.ToArray());
        }

        private sealed class StubEventService : IWorldEventPersistenceService
        {
            private readonly Dictionary<string, IReadOnlyList<WorldSessionEvent>> _events;
            public long Queries;

            public StubEventService(Dictionary<string, IReadOnlyList<WorldSessionEvent>> events)
                => _events = events;

            public void EnqueueEvent(WorldSessionEvent evt) { }

            public Task<IReadOnlyList<WorldSessionEvent>> QueryEventsAsync(string sessionId, int take, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Queries);
                return Task.FromResult(_events.TryGetValue(sessionId, out var e) ? e : (IReadOnlyList<WorldSessionEvent>)Array.Empty<WorldSessionEvent>());
            }

            public Task<WorldSessionSummary> GetSessionSummaryAsync(string sessionId, CancellationToken cancellationToken)
                => Task.FromResult(new WorldSessionSummary { SessionId = sessionId });
        }

        private static async Task<HotChocolate.Execution.IRequestExecutor> BuildExecutor(
            IScyllaWorldPersistenceService scylla,
            IWorldEventPersistenceService events,
            IAuthoritativeActionService action)
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddSingleton(scylla);
            services.AddSingleton(events);
            services.AddSingleton(action);
            services.AddSingleton(new DataLoaderCounters());

            return await services
                .AddGraphQLServer()
                .AddQueryType<GraphQLQuery>()
                .AddMutationType<GraphQLMutation>()
                .AddDataLoader<RoomsBySessionDataLoader>()
                .AddDataLoader<EnemiesBySessionDataLoader>()
                .AddDataLoader<LootBySessionDataLoader>()
                .AddDataLoader<EventsBySessionDataLoader>()
                .ModifyCostOptions(o => o.MaxFieldCost = 10000)
                .ModifyRequestOptions(o => o.ExecutionTimeout = TimeSpan.FromSeconds(30))
                .BuildRequestExecutorAsync()
                .ConfigureAwait(false);
        }

        [FactAttribute]
        public async Task SessionState_ReturnsExpectedPlayerAndEnemyAndEventFields()
        {
            var state = new AuthoritativeGameStateDto
            {
                Turn = 4,
                GameOver = false,
                Player = new AuthoritativePlayerDto
                {
                    Id = 1, X = 5, Y = 6, Level = 3, Health = 80, MaxHealth = 100,
                    Gold = 50, Experience = 120, IsDead = false
                },
                Enemies =
                {
                    new AuthoritativeEnemyDto { Id = 7, Archetype = "skeleton", X = 2, Y = 2, Level = 2, Health = 30, MaxHealth = 30, IsDead = false }
                },
                RecentEvents = new List<AuthoritativeWorldEventDto>
                {
                    new AuthoritativeWorldEventDto { EventId = "evt_1", Turn = 4, Type = "move", Message = "moved" }
                }
            };

            var action = new StubActionService(new Dictionary<string, AuthoritativeGameStateDto> { ["s1"] = state });
            var scylla = new StubScyllaService(new(), new(), new());
            var events = new StubEventService(new());
            var executor = await BuildExecutor(scylla, events, action);

            var result = await executor.ExecuteAsync(
                "{ sessionState(sessionId: \"s1\") { turn gameOver player { id health level gold } enemies { id archetype health } recentEvents { eventId type } } }");
            var json = result.ToJson(false);

            Assert.Contains("\"data\"", json);
            Assert.Contains("\"sessionState\"", json);
            Assert.Contains("\"turn\":4", json);
            Assert.Contains("\"health\":80", json);
            Assert.Contains("\"skeleton\"", json);
            Assert.Contains("\"evt_1\"", json);
        }

        [FactAttribute]
        public async Task Events_ArePaginatedWithConnectionShapes()
        {
            var list = new List<WorldSessionEvent>();
            for (int i = 0; i < 5; i++)
            {
                list.Add(new WorldSessionEvent
                {
                    EventId = $"evt_{i}",
                    SessionId = "s1",
                    EventType = "type_" + i,
                    Category = "simulation",
                    Message = "msg " + i,
                    Frame = (uint)i
                });
            }

            var events = new StubEventService(new Dictionary<string, IReadOnlyList<WorldSessionEvent>> { ["s1"] = list });
            var scylla = new StubScyllaService(new(), new(), new());
            var action = new StubActionService(new());
            var executor = await BuildExecutor(scylla, events, action);

            var result = await executor.ExecuteAsync(
                "{ events(sessionId: \"s1\", first: 2) { edges { node { eventId eventType } cursor } pageInfo { hasNextPage hasPreviousPage } } }");
            var json = result.ToJson(false);

            Assert.Contains("\"edges\"", json);
            Assert.Contains("\"node\"", json);
            Assert.Contains("\"cursor\"", json);
            Assert.Contains("\"evt_0\"", json);
            Assert.Contains("\"evt_1\"", json);
            Assert.Contains("\"hasNextPage\":true", json);
        }

        [FactAttribute]
        public async Task Sessions_ArePaginatedWithConnectionShapes()
        {
            var scylla = new StubScyllaService(new(), new(), new());
            for (int i = 0; i < 5; i++)
                scylla.SessionIds.Add("session_" + i);
            var events = new StubEventService(new());
            var action = new StubActionService(new());
            var executor = await BuildExecutor(scylla, events, action);

            var result = await executor.ExecuteAsync(
                "{ sessions(first: 2) { edges { node { sessionId } cursor } pageInfo { hasNextPage } } }");
            var json = result.ToJson(false);

            Assert.Contains("\"edges\"", json);
            Assert.Contains("\"session_0\"", json);
            Assert.Contains("\"session_1\"", json);
            Assert.Contains("\"hasNextPage\":true", json);
        }

        [FactAttribute]
        public async Task DataLoader_BatchesEnemyLoadsAcrossManySessions()
        {
            const int sessionCount = 20;
            var rooms = new Dictionary<string, IReadOnlyList<WorldRoomRow>>();
            var enemies = new Dictionary<string, IReadOnlyList<WorldEnemyRow>>();
            var loot = new Dictionary<string, IReadOnlyList<WorldLootRow>>();

            for (int i = 0; i < sessionCount; i++)
            {
                var sid = "s_" + i.ToString("D2");
                rooms[sid] = new List<WorldRoomRow> { new() { RoomId = i, X = i, Y = i, Width = 4, Height = 4 } };
                enemies[sid] = new List<WorldEnemyRow>
                {
                    new() { EnemyId = i * 10 + 1, Archetype = "wolf", X = 1, Y = 1, Level = 1 },
                    new() { EnemyId = i * 10 + 2, Archetype = "goblin", X = 2, Y = 2, Level = 2 }
                };
                loot[sid] = new List<WorldLootRow> { new() { ItemId = "i" + i, ItemType = "gold", Tier = "common", X = 3, Y = 3 } };
            }

            var scylla = new StubScyllaService(rooms, enemies, loot);
            for (int i = 0; i < sessionCount; i++)
                scylla.SessionIds.Add("s_" + i.ToString("D2"));

            var events = new StubEventService(new());
            var action = new StubActionService(new());
            var counters = new DataLoaderCounters();
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddSingleton<IScyllaWorldPersistenceService>(scylla);
            services.AddSingleton<IWorldEventPersistenceService>(events);
            services.AddSingleton<IAuthoritativeActionService>(action);
            services.AddSingleton(counters);

            var executor = await services
                .AddGraphQLServer()
                .AddQueryType<GraphQLQuery>()
                .AddMutationType<GraphQLMutation>()
                .AddDataLoader<RoomsBySessionDataLoader>()
                .AddDataLoader<EnemiesBySessionDataLoader>()
                .AddDataLoader<LootBySessionDataLoader>()
                .AddDataLoader<EventsBySessionDataLoader>()
                .ModifyCostOptions(o => o.MaxFieldCost = 10000)
                .BuildRequestExecutorAsync();

            var result = await executor.ExecuteAsync(
                "{ sessions(first: 20) { nodes { sessionId enemies { enemyId archetype } rooms { roomId } loot { itemId } } } }");
            var json = result.ToJson(false);

            Assert.DoesNotContain("\"errors\"", json);
            Assert.Contains("\"wolf\"", json);
            Assert.Contains("\"goblin\"", json);

            Assert.Equal(1, counters.EnemiesBatches);
            Assert.Equal(1, counters.RoomsBatches);
            Assert.Equal(1, counters.LootBatches);
            Assert.Equal(sessionCount, scylla.EnemiesQueries);
        }
    }
}
#endif
