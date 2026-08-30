#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Authoritative.Multiplayer;
using Authoritative.Services;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Types;

namespace Authoritative.GraphQL
{
    public sealed class GraphQLQuery
    {
        [UsePaging]
        public async Task<IQueryable<Session>> GetSessions(
            [Service] IScyllaWorldPersistenceService scylla,
            CancellationToken cancellationToken)
        {
            var ids = await scylla.GetAllSessionIdsAsync(cancellationToken).ConfigureAwait(false);
            return ids
                .Select(id => new Session { SessionId = id })
                .AsQueryable()
                .OrderBy(s => s.SessionId);
        }

        public async Task<AuthoritativeGameStateDto?> SessionState(
            string sessionId,
            [Service] IAuthoritativeActionService service,
            CancellationToken cancellationToken)
        {
            return await service.GetStateAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<WorldRoomRow>> SessionRooms(
            string sessionId,
            [Service] IScyllaWorldPersistenceService scylla,
            CancellationToken cancellationToken)
        {
            return await scylla.GetRoomsAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<WorldEnemyRow>> SessionEnemies(
            string sessionId,
            [Service] IScyllaWorldPersistenceService scylla,
            CancellationToken cancellationToken)
        {
            return await scylla.GetEnemiesAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<WorldLootRow>> SessionLoot(
            string sessionId,
            [Service] IScyllaWorldPersistenceService scylla,
            CancellationToken cancellationToken)
        {
            return await scylla.GetLootAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }

        [UsePaging]
        public IQueryable<WorldSessionEvent> Events(
            string sessionId,
            [Service] IWorldEventPersistenceService events,
            CancellationToken cancellationToken)
        {
            var list = events.QueryEventsAsync(sessionId, 1000, cancellationToken).GetAwaiter().GetResult();
            return list.AsQueryable().OrderBy(e => e.EventId);
        }
    }
}
#endif
