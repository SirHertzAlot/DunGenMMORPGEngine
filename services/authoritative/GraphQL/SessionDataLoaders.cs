#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Authoritative.Services;
using GreenDonut;

namespace Authoritative.GraphQL
{
    public sealed class RoomsBySessionDataLoader : BatchDataLoader<string, IReadOnlyList<WorldRoomRow>>
    {
        private readonly IScyllaWorldPersistenceService _scylla;
        private readonly DataLoaderCounters _counters;

        public RoomsBySessionDataLoader(
            IBatchScheduler batchScheduler,
            DataLoaderOptions options,
            IScyllaWorldPersistenceService scylla,
            DataLoaderCounters counters)
            : base(batchScheduler, options)
        {
            _scylla = scylla;
            _counters = counters;
        }

        protected override async Task<IReadOnlyDictionary<string, IReadOnlyList<WorldRoomRow>>> LoadBatchAsync(
            IReadOnlyList<string> keys,
            CancellationToken cancellationToken)
        {
            _counters.RoomsBatches++;
            var result = new Dictionary<string, IReadOnlyList<WorldRoomRow>>();
            var tasks = new Task<(string, IReadOnlyList<WorldRoomRow>)>[keys.Count];
            for (int i = 0; i < keys.Count; i++)
                tasks[i] = LoadOneAsync(keys[i], cancellationToken);
            foreach (var t in await Task.WhenAll(tasks).ConfigureAwait(false))
                result[t.Item1] = t.Item2;
            return result;
        }

        private async Task<(string, IReadOnlyList<WorldRoomRow>)> LoadOneAsync(string sessionId, CancellationToken ct)
        {
            return (sessionId, await _scylla.GetRoomsAsync(sessionId, ct).ConfigureAwait(false));
        }
    }

    public sealed class EnemiesBySessionDataLoader : BatchDataLoader<string, IReadOnlyList<WorldEnemyRow>>
    {
        private readonly IScyllaWorldPersistenceService _scylla;
        private readonly DataLoaderCounters _counters;

        public EnemiesBySessionDataLoader(
            IBatchScheduler batchScheduler,
            DataLoaderOptions options,
            IScyllaWorldPersistenceService scylla,
            DataLoaderCounters counters)
            : base(batchScheduler, options)
        {
            _scylla = scylla;
            _counters = counters;
        }

        protected override async Task<IReadOnlyDictionary<string, IReadOnlyList<WorldEnemyRow>>> LoadBatchAsync(
            IReadOnlyList<string> keys,
            CancellationToken cancellationToken)
        {
            _counters.EnemiesBatches++;
            var result = new Dictionary<string, IReadOnlyList<WorldEnemyRow>>();
            var tasks = new Task<(string, IReadOnlyList<WorldEnemyRow>)>[keys.Count];
            for (int i = 0; i < keys.Count; i++)
                tasks[i] = LoadOneAsync(keys[i], cancellationToken);
            foreach (var t in await Task.WhenAll(tasks).ConfigureAwait(false))
                result[t.Item1] = t.Item2;
            return result;
        }

        private async Task<(string, IReadOnlyList<WorldEnemyRow>)> LoadOneAsync(string sessionId, CancellationToken ct)
        {
            return (sessionId, await _scylla.GetEnemiesAsync(sessionId, ct).ConfigureAwait(false));
        }
    }

    public sealed class LootBySessionDataLoader : BatchDataLoader<string, IReadOnlyList<WorldLootRow>>
    {
        private readonly IScyllaWorldPersistenceService _scylla;
        private readonly DataLoaderCounters _counters;

        public LootBySessionDataLoader(
            IBatchScheduler batchScheduler,
            DataLoaderOptions options,
            IScyllaWorldPersistenceService scylla,
            DataLoaderCounters counters)
            : base(batchScheduler, options)
        {
            _scylla = scylla;
            _counters = counters;
        }

        protected override async Task<IReadOnlyDictionary<string, IReadOnlyList<WorldLootRow>>> LoadBatchAsync(
            IReadOnlyList<string> keys,
            CancellationToken cancellationToken)
        {
            _counters.LootBatches++;
            var result = new Dictionary<string, IReadOnlyList<WorldLootRow>>();
            var tasks = new Task<(string, IReadOnlyList<WorldLootRow>)>[keys.Count];
            for (int i = 0; i < keys.Count; i++)
                tasks[i] = LoadOneAsync(keys[i], cancellationToken);
            foreach (var t in await Task.WhenAll(tasks).ConfigureAwait(false))
                result[t.Item1] = t.Item2;
            return result;
        }

        private async Task<(string, IReadOnlyList<WorldLootRow>)> LoadOneAsync(string sessionId, CancellationToken ct)
        {
            return (sessionId, await _scylla.GetLootAsync(sessionId, ct).ConfigureAwait(false));
        }
    }

    public sealed class EventsBySessionDataLoader : BatchDataLoader<string, IReadOnlyList<WorldSessionEvent>>
    {
        private readonly IWorldEventPersistenceService _events;
        private readonly DataLoaderCounters _counters;

        public EventsBySessionDataLoader(
            IBatchScheduler batchScheduler,
            DataLoaderOptions options,
            IWorldEventPersistenceService events,
            DataLoaderCounters counters)
            : base(batchScheduler, options)
        {
            _events = events;
            _counters = counters;
        }

        protected override async Task<IReadOnlyDictionary<string, IReadOnlyList<WorldSessionEvent>>> LoadBatchAsync(
            IReadOnlyList<string> keys,
            CancellationToken cancellationToken)
        {
            _counters.EventsBatches++;
            var result = new Dictionary<string, IReadOnlyList<WorldSessionEvent>>();
            var tasks = new Task<(string, IReadOnlyList<WorldSessionEvent>)>[keys.Count];
            for (int i = 0; i < keys.Count; i++)
                tasks[i] = LoadOneAsync(keys[i], cancellationToken);
            foreach (var t in await Task.WhenAll(tasks).ConfigureAwait(false))
                result[t.Item1] = t.Item2;
            return result;
        }

        private async Task<(string, IReadOnlyList<WorldSessionEvent>)> LoadOneAsync(string sessionId, CancellationToken ct)
        {
            return (sessionId, await _events.QueryEventsAsync(sessionId, 1000, ct).ConfigureAwait(false));
        }
    }
}
#endif
