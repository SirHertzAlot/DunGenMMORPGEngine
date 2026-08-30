#if !UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Authoritative.Multiplayer;
using Authoritative.Services;
using HotChocolate;
using HotChocolate.Types;

namespace Authoritative.GraphQL
{
    [ObjectType("Session")]
    public sealed class Session
    {
        public string SessionId { get; set; } = string.Empty;

        public async Task<AuthoritativeGameStateDto?> GetState(
            [Parent] Session session,
            [Service] IAuthoritativeActionService service,
            CancellationToken cancellationToken)
        {
            return await service.GetStateAsync(session.SessionId, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<WorldRoomRow>> GetRooms(
            [Parent] Session session,
            RoomsBySessionDataLoader loader,
            CancellationToken cancellationToken)
        {
            return (await loader.LoadAsync(session.SessionId, cancellationToken).ConfigureAwait(false))!;
        }

        public async Task<IReadOnlyList<WorldEnemyRow>> GetEnemies(
            [Parent] Session session,
            EnemiesBySessionDataLoader loader,
            CancellationToken cancellationToken)
        {
            return (await loader.LoadAsync(session.SessionId, cancellationToken).ConfigureAwait(false))!;
        }

        public async Task<IReadOnlyList<WorldLootRow>> GetLoot(
            [Parent] Session session,
            LootBySessionDataLoader loader,
            CancellationToken cancellationToken)
        {
            return (await loader.LoadAsync(session.SessionId, cancellationToken).ConfigureAwait(false))!;
        }

        public async Task<IReadOnlyList<WorldSessionEvent>> GetEvents(
            [Parent] Session session,
            EventsBySessionDataLoader loader,
            CancellationToken cancellationToken)
        {
            return (await loader.LoadAsync(session.SessionId, cancellationToken).ConfigureAwait(false))!;
        }
    }
}
#endif
