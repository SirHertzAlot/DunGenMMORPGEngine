using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace DunGen.ECS.Core
{
    /// <summary>
    /// High-performance entity index cache using direct array indexing.
    /// Eliminates Dictionary overhead with O(1) array access.
    /// 
    /// Uses slot-based allocation with generation counters for ABA problem prevention.
    /// Memory layout: contiguous arrays for maximum cache efficiency.
    /// 
    /// Trade-off: Fixed memory allocation (N * sizeof(slot)) vs Dictionary's dynamic growth.
    /// For game use: predictable memory, zero GC allocations during runtime.
    /// </summary>
    public sealed class DirectEntityCache
    {
        private static DirectEntityCache _instance;
        public static DirectEntityCache Instance => _instance ??= new DirectEntityCache();

        private const int MaxEntities = 8192;
        private const int InvalidIndex = -1;

        [Flags]
        public enum EntityFlags : uint
        {
            None = 0,
            Valid = 1 << 0,
            IsPlayer = 1 << 1,
            InCombat = 1 << 2,
            IsDead = 1 << 3,
            // Room for 28 more flags
        }

        // Slot structure: [Entity (8 bytes)][Generation (4 bytes)][Flags (4 bytes)] = 16 bytes
        // Fits exactly in cache line with 4 entities per line
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct EntitySlot
        {
            public Entity Entity;           // 8 bytes (index + version)
            public uint Generation;         // 4 bytes - for detecting stale references
            public EntityFlags Flags;       // 4 bytes - compact flag storage
        }

        // Primary storage: direct-indexed array
        private readonly EntitySlot[] _slots;

        // Combat session mapping: sessionId -> packed entity list
        // Using fixed-size arrays to avoid allocations
        private const int MaxSessions = 8192;
        private const int MaxEntitiesPerSession = 16;
        private readonly int[,] _sessionEntities;
        private readonly byte[] _sessionEntityCounts;

        // Cached special entities
        private int _playerEntityIndex = InvalidIndex;
        private uint _currentGeneration;

        public DirectEntityCache()
        {
            _slots = new EntitySlot[MaxEntities];
            _sessionEntities = new int[MaxSessions, MaxEntitiesPerSession];
            _sessionEntityCounts = new byte[MaxSessions];

            // Initialize session arrays with InvalidIndex
            for (int s = 0; s < MaxSessions; s++)
            {
                for (int e = 0; e < MaxEntitiesPerSession; e++)
                {
                    _sessionEntities[s, e] = InvalidIndex;
                }
            }
        }

        /// <summary>
        /// Register entity for O(1) lookup. Uses Entity.Index as array index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Register(Entity entity)
        {
            if (entity == Entity.Null)
                return;

            int idx = entity.Index;
            if (idx < 0 || idx >= MaxEntities)
                return;

            ref var slot = ref _slots[idx];
            var flags = slot.Flags;
            if ((flags & EntityFlags.Valid) != 0 && slot.Entity != entity)
            {
                for (int sessionId = 0; sessionId < MaxSessions; sessionId++)
                {
                    UnregisterFromSession(idx, sessionId);
                }

                if (idx == _playerEntityIndex)
                    _playerEntityIndex = InvalidIndex;

                flags = EntityFlags.None;
            }

            slot.Entity = entity;
            slot.Generation = ++_currentGeneration;
            slot.Flags = flags | EntityFlags.Valid;
        }

        /// <summary>
        /// Register entity with combat session.
        /// </summary>
        public void Register(Entity entity, int combatSessionId)
        {
            Register(entity);

            if (combatSessionId <= 0 || combatSessionId >= MaxSessions)
                return;

            int idx = entity.Index;
            if (idx < 0 || idx >= MaxEntities)
                return;

            // Mark as in combat
            _slots[idx].Flags |= EntityFlags.InCombat;

            // Add to session list
            int count = _sessionEntityCounts[combatSessionId];
            for (int i = 0; i < count; i++)
            {
                if (_sessionEntities[combatSessionId, i] == idx)
                    return;
            }

            if (count < MaxEntitiesPerSession)
            {
                _sessionEntities[combatSessionId, count] = idx;
                _sessionEntityCounts[combatSessionId]++;
            }
        }

        /// <summary>
        /// Register as player entity for ultra-fast lookup.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterPlayer(Entity entity)
        {
            if (entity == Entity.Null)
                return;

            Register(entity);

            int idx = entity.Index;
            if (idx >= 0 && idx < MaxEntities)
            {
                _slots[idx].Flags |= EntityFlags.IsPlayer;
                _playerEntityIndex = idx;
            }
        }

        /// <summary>
        /// Unregister entity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unregister(Entity entity)
        {
            if (entity == Entity.Null)
                return;

            int idx = entity.Index;
            if (idx < 0 || idx >= MaxEntities)
                return;

            ref var slot = ref _slots[idx];
            slot.Flags = EntityFlags.None;
            slot.Entity = Entity.Null;

            for (int sessionId = 0; sessionId < MaxSessions; sessionId++)
            {
                UnregisterFromSession(idx, sessionId);
            }

            if (idx == _playerEntityIndex)
                _playerEntityIndex = InvalidIndex;
        }

        /// <summary>
        /// O(1) entity lookup by index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetEntity(int entityIndex, out Entity entity)
        {
            if (entityIndex < 0 || entityIndex >= MaxEntities)
            {
                entity = Entity.Null;
                return false;
            }

            ref readonly var slot = ref _slots[entityIndex];
            if ((slot.Flags & EntityFlags.Valid) == 0)
            {
                entity = Entity.Null;
                return false;
            }

            entity = slot.Entity;
            return true;
        }

        /// <summary>
        /// O(1) player entity lookup.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPlayerEntity(out Entity entity)
        {
            if (_playerEntityIndex == InvalidIndex)
            {
                entity = Entity.Null;
                return false;
            }

            entity = _slots[_playerEntityIndex].Entity;
            return true;
        }

        /// <summary>
        /// Get entities in combat session. Returns array segment to avoid allocation.
        /// </summary>
        public int GetSessionEntityCount(int combatSessionId)
        {
            if (combatSessionId <= 0 || combatSessionId >= MaxSessions)
                return 0;

            return _sessionEntityCounts[combatSessionId];
        }

        /// <summary>
        /// Get entity at index within session. Use with GetSessionEntityCount.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSessionEntityAt(int combatSessionId, int index)
        {
            if (combatSessionId <= 0 || combatSessionId >= MaxSessions)
                return InvalidIndex;

            if (index < 0 || index >= _sessionEntityCounts[combatSessionId])
                return InvalidIndex;

            return _sessionEntities[combatSessionId, index];
        }

        /// <summary>
        /// Remove entity from combat session.
        /// </summary>
        public void UnregisterFromSession(int entityIndex, int combatSessionId)
        {
            if (combatSessionId <= 0 || combatSessionId >= MaxSessions)
                return;

            int count = _sessionEntityCounts[combatSessionId];
            for (int i = 0; i < count; i++)
            {
                if (_sessionEntities[combatSessionId, i] == entityIndex)
                {
                    // Swap with last and decrement count
                    _sessionEntities[combatSessionId, i] = _sessionEntities[combatSessionId, count - 1];
                    _sessionEntities[combatSessionId, count - 1] = InvalidIndex;
                    _sessionEntityCounts[combatSessionId]--;

                    if (entityIndex >= 0 && entityIndex < MaxEntities)
                        _slots[entityIndex].Flags &= ~EntityFlags.InCombat;

                    return;
                }
            }
        }

        /// <summary>
        /// Set entity flags (dead, in combat, etc).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFlag(int entityIndex, EntityFlags flag, bool value)
        {
            if (entityIndex < 0 || entityIndex >= MaxEntities)
                return;

            if (value)
                _slots[entityIndex].Flags |= flag;
            else
                _slots[entityIndex].Flags &= ~flag;
        }

        /// <summary>
        /// Check entity flags.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFlag(int entityIndex, EntityFlags flag)
        {
            if (entityIndex < 0 || entityIndex >= MaxEntities)
                return false;

            return (_slots[entityIndex].Flags & flag) != 0;
        }

        /// <summary>
        /// Check if entity exists in cache.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int entityIndex)
        {
            if (entityIndex < 0 || entityIndex >= MaxEntities)
                return false;

            return (_slots[entityIndex].Flags & EntityFlags.Valid) != 0;
        }

        /// <summary>
        /// Clear all data.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
            Array.Clear(_sessionEntityCounts, 0, _sessionEntityCounts.Length);

            for (int s = 0; s < MaxSessions; s++)
            {
                for (int e = 0; e < MaxEntitiesPerSession; e++)
                {
                    _sessionEntities[s, e] = InvalidIndex;
                }
            }

            _playerEntityIndex = InvalidIndex;
        }

        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxEntities; i++)
                {
                    if ((_slots[i].Flags & EntityFlags.Valid) != 0)
                        count++;
                }
                return count;
            }
        }
    }
}
