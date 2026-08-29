using System.Collections.Generic;
using Unity.Entities;

namespace DunGen.ECS.Core
{
    /// <summary>
    /// High-performance entity lookup cache providing O(1) entity retrieval by index.
    /// Replaces O(n) linear scans across entity arrays with hash-based lookups.
    /// 
    /// Usage:
    /// 1. Call Register() when creating entities
    /// 2. Call Unregister() when destroying entities  
    /// 3. Use TryGetEntity() for O(1) lookups instead of iterating all entities
    /// 
    /// For session-scoped lookups, use the combat session cache methods.
    /// </summary>
    public class EntityIndexCache
    {
        private static EntityIndexCache _instance;
        public static EntityIndexCache Instance => _instance ??= new EntityIndexCache();

        // Primary cache: Entity.Index -> Entity for O(1) lookup
        private readonly Dictionary<int, Entity> _entityByIndex = new(256);

        // Secondary cache: CombatSessionId -> List of entity indices for session-scoped queries
        private readonly Dictionary<int, HashSet<int>> _combatSessionEntities = new(32);

        // Player entity cache for ultra-fast player lookups
        private Entity _playerEntity = Entity.Null;
        private int _playerEntityIndex = -1;

        /// <summary>
        /// Register an entity in the cache for O(1) lookup.
        /// Call this when creating new entities.
        /// </summary>
        public void Register(Entity entity)
        {
            if (entity == Entity.Null)
                return;

            if (_entityByIndex.TryGetValue(entity.Index, out var existing) && existing != entity)
            {
                RemoveIndexFromAllSessions(entity.Index);
                if (entity.Index == _playerEntityIndex)
                {
                    _playerEntity = Entity.Null;
                    _playerEntityIndex = -1;
                }
            }

            _entityByIndex[entity.Index] = entity;
        }

        /// <summary>
        /// Register an entity with its combat session for session-scoped lookups.
        /// </summary>
        public void Register(Entity entity, int combatSessionId)
        {
            Register(entity);

            if (combatSessionId <= 0)
                return;

            if (!_combatSessionEntities.TryGetValue(combatSessionId, out var sessionEntities))
            {
                sessionEntities = new HashSet<int>(16);
                _combatSessionEntities[combatSessionId] = sessionEntities;
            }

            sessionEntities.Add(entity.Index);
        }

        /// <summary>
        /// Register the player entity for ultra-fast player lookups.
        /// </summary>
        public void RegisterPlayer(Entity entity)
        {
            if (entity == Entity.Null)
                return;

            Register(entity);
            _playerEntity = entity;
            _playerEntityIndex = entity.Index;
        }

        /// <summary>
        /// Unregister an entity from the cache.
        /// Call this when destroying entities.
        /// </summary>
        public void Unregister(Entity entity)
        {
            if (entity == Entity.Null)
                return;

            _entityByIndex.Remove(entity.Index);

            RemoveIndexFromAllSessions(entity.Index);

            if (entity.Index == _playerEntityIndex)
            {
                _playerEntity = Entity.Null;
                _playerEntityIndex = -1;
            }
        }

        /// <summary>
        /// Unregister an entity from a specific combat session.
        /// </summary>
        public void UnregisterFromSession(int entityIndex, int combatSessionId)
        {
            if (_combatSessionEntities.TryGetValue(combatSessionId, out var sessionEntities))
            {
                sessionEntities.Remove(entityIndex);
                if (sessionEntities.Count == 0)
                    _combatSessionEntities.Remove(combatSessionId);
            }
        }

        /// <summary>
        /// O(1) entity lookup by index. Returns true if found, false otherwise.
        /// </summary>
        public bool TryGetEntity(int entityIndex, out Entity entity)
        {
            return _entityByIndex.TryGetValue(entityIndex, out entity);
        }

        /// <summary>
        /// O(1) player entity lookup.
        /// </summary>
        public bool TryGetPlayerEntity(out Entity entity)
        {
            entity = _playerEntity;
            return _playerEntity != Entity.Null;
        }

        /// <summary>
        /// Get all entity indices in a combat session. O(1) lookup.
        /// </summary>
        public bool TryGetSessionEntities(int combatSessionId, out HashSet<int> entityIndices)
        {
            return _combatSessionEntities.TryGetValue(combatSessionId, out entityIndices);
        }

        /// <summary>
        /// Get entities in a combat session as a list (creates new list).
        /// </summary>
        public List<Entity> GetSessionEntityList(int combatSessionId)
        {
            var result = new List<Entity>(16);

            if (!_combatSessionEntities.TryGetValue(combatSessionId, out var indices))
                return result;

            foreach (var index in indices)
            {
                if (_entityByIndex.TryGetValue(index, out var entity))
                    result.Add(entity);
            }

            return result;
        }

        /// <summary>
        /// Check if an entity exists in the cache.
        /// </summary>
        public bool Contains(int entityIndex)
        {
            return _entityByIndex.ContainsKey(entityIndex);
        }

        /// <summary>
        /// Get current cache size.
        /// </summary>
        public int Count => _entityByIndex.Count;

        /// <summary>
        /// Clear all cached data. Call when resetting the game state.
        /// </summary>
        public void Clear()
        {
            _entityByIndex.Clear();
            _combatSessionEntities.Clear();
            _playerEntity = Entity.Null;
            _playerEntityIndex = -1;
        }

        private void RemoveIndexFromAllSessions(int entityIndex)
        {
            foreach (var sessionId in new List<int>(_combatSessionEntities.Keys))
            {
                var sessionEntities = _combatSessionEntities[sessionId];
                sessionEntities.Remove(entityIndex);
                if (sessionEntities.Count == 0)
                    _combatSessionEntities.Remove(sessionId);
            }
        }

        /// <summary>
        /// Rebuild cache from EntityManager. Useful after loading or when cache may be stale.
        /// </summary>
        public void RebuildFromEntityManager(EntityManager entityManager)
        {
            Clear();

            // This is O(n) but only called once during initialization
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Unity.Transforms.LocalTransform>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Register(entities[i]);
            }
        }
    }
}
