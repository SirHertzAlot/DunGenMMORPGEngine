using System.Runtime.CompilerServices;
using Unity.Entities;

namespace DunGen.ECS.Core
{
    /// <summary>
    /// Unified facade for spatial and entity lookups.
    /// Allows switching between implementations at runtime for profiling,
    /// or compile-time for production.
    /// 
    /// Available backends:
    /// - Dictionary-based (original): Dynamic memory, good for sparse/variable entity counts
    /// - Morton/Direct (exotic): Fixed memory, cache-optimized, zero GC during runtime
    /// - Bloom-accelerated: Adds bloom filter for fast negative checks
    /// 
    /// Default: Morton + Direct for maximum performance in bounded scenarios.
    /// </summary>
    public static class LookupFacade
    {
        public enum Backend
        {
            Dictionary,     // Original Dictionary<> based (flexible, some GC)
            Direct,         // Array-indexed with Morton codes (fast, fixed memory)
            BloomAccelerated // Direct + bloom filter for fast negative checks
        }

        private static Backend _spatialBackend = Backend.BloomAccelerated;
        private static Backend _entityBackend = Backend.Direct;

        public static void SetBackend(Backend spatial, Backend entity)
        {
            _spatialBackend = spatial;
            _entityBackend = entity;
        }

        // === Spatial Lookups ===

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdatePosition(int entityIndex, int x, int y, int level)
        {
            switch (_spatialBackend)
            {
                case Backend.Dictionary:
                    SpatialHashGrid.Instance.UpdatePosition(entityIndex, x, y, level);
                    break;
                case Backend.Direct:
                    MortonSpatialGrid.Instance.UpdatePosition(entityIndex, x, y, level);
                    break;
                case Backend.BloomAccelerated:
                    FastSpatialLookup.Instance.UpdatePosition(entityIndex, x, y, level);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetEntitiesAt(int x, int y, int level, out int firstEntity, out int count)
        {
            switch (_spatialBackend)
            {
                case Backend.Dictionary:
                    if (SpatialHashGrid.Instance.GetEntitiesAt(x, y, level, out var entities))
                    {
                        using var enumerator = entities.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            firstEntity = enumerator.Current;
                            count = entities.Count;
                            return true;
                        }
                    }
                    firstEntity = -1;
                    count = 0;
                    return false;

                case Backend.Direct:
                    return MortonSpatialGrid.Instance.TryGetEntitiesAt(x, y, level, out firstEntity, out count);

                case Backend.BloomAccelerated:
                    return FastSpatialLookup.Instance.TryGetEntitiesAt(x, y, level, out firstEntity, out count);

                default:
                    firstEntity = -1;
                    count = 0;
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindClosestEntity(int x, int y, int level, int maxRange, int exclude = -1)
        {
            switch (_spatialBackend)
            {
                case Backend.Dictionary:
                    return SpatialHashGrid.Instance.FindClosestEntity(x, y, level, maxRange, exclude);
                case Backend.Direct:
                    return MortonSpatialGrid.Instance.FindClosestEntity(x, y, level, maxRange, exclude);
                case Backend.BloomAccelerated:
                    return FastSpatialLookup.Instance.FindClosest(x, y, level, maxRange, exclude);
                default:
                    return -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetPosition(int entityIndex, out int x, out int y, out int level)
        {
            switch (_spatialBackend)
            {
                case Backend.Dictionary:
                    return SpatialHashGrid.Instance.TryGetPosition(entityIndex, out x, out y, out level);
                case Backend.Direct:
                    return MortonSpatialGrid.Instance.TryGetPosition(entityIndex, out x, out y, out level);
                case Backend.BloomAccelerated:
                    return FastSpatialLookup.Instance.TryGetPosition(entityIndex, out x, out y, out level);
                default:
                    x = y = level = 0;
                    return false;
            }
        }

        public static void RemoveFromSpatial(int entityIndex)
        {
            switch (_spatialBackend)
            {
                case Backend.Dictionary:
                    SpatialHashGrid.Instance.Remove(entityIndex);
                    break;
                case Backend.Direct:
                    MortonSpatialGrid.Instance.Remove(entityIndex);
                    break;
                case Backend.BloomAccelerated:
                    FastSpatialLookup.Instance.Remove(entityIndex);
                    break;
            }
        }

        // === Entity Lookups ===

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegisterEntity(Entity entity)
        {
            switch (_entityBackend)
            {
                case Backend.Dictionary:
                    EntityIndexCache.Instance.Register(entity);
                    break;
                default:
                    DirectEntityCache.Instance.Register(entity);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegisterEntity(Entity entity, int combatSessionId)
        {
            switch (_entityBackend)
            {
                case Backend.Dictionary:
                    EntityIndexCache.Instance.Register(entity, combatSessionId);
                    break;
                default:
                    DirectEntityCache.Instance.Register(entity, combatSessionId);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegisterPlayer(Entity entity)
        {
            switch (_entityBackend)
            {
                case Backend.Dictionary:
                    EntityIndexCache.Instance.RegisterPlayer(entity);
                    break;
                default:
                    DirectEntityCache.Instance.RegisterPlayer(entity);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetEntity(int entityIndex, out Entity entity)
        {
            switch (_entityBackend)
            {
                case Backend.Dictionary:
                    return EntityIndexCache.Instance.TryGetEntity(entityIndex, out entity);
                default:
                    return DirectEntityCache.Instance.TryGetEntity(entityIndex, out entity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetPlayerEntity(out Entity entity)
        {
            switch (_entityBackend)
            {
                case Backend.Dictionary:
                    return EntityIndexCache.Instance.TryGetPlayerEntity(out entity);
                default:
                    return DirectEntityCache.Instance.TryGetPlayerEntity(out entity);
            }
        }

        public static void UnregisterEntity(Entity entity)
        {
            switch (_entityBackend)
            {
                case Backend.Dictionary:
                    EntityIndexCache.Instance.Unregister(entity);
                    break;
                default:
                    DirectEntityCache.Instance.Unregister(entity);
                    break;
            }
        }

        // === Bulk Operations ===

        public static void ClearAll()
        {
            // Clear spatial
            SpatialHashGrid.Instance.Clear();
            MortonSpatialGrid.Instance.Clear();
            FastSpatialLookup.Instance.Clear();

            // Clear entity
            EntityIndexCache.Instance.Clear();
            DirectEntityCache.Instance.Clear();
        }

        public static void ClearSpatial()
        {
            switch (_spatialBackend)
            {
                case Backend.Dictionary:
                    SpatialHashGrid.Instance.Clear();
                    break;
                case Backend.Direct:
                    MortonSpatialGrid.Instance.Clear();
                    break;
                case Backend.BloomAccelerated:
                    FastSpatialLookup.Instance.Clear();
                    break;
            }
        }

        public static void ClearEntities()
        {
            switch (_entityBackend)
            {
                case Backend.Dictionary:
                    EntityIndexCache.Instance.Clear();
                    break;
                default:
                    DirectEntityCache.Instance.Clear();
                    break;
            }
        }
    }
}
