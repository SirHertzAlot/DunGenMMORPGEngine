using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DunGen.ECS.Core
{
    /// <summary>
    /// Area of Interest (AoI) manager for high-performance spatial partitioning,
    /// network relevance culling, and client/server interest management.
    /// </summary>
    public sealed class SpatialInterestManager
    {
        private static SpatialInterestManager _instance;
        public static SpatialInterestManager Instance => _instance ??= new SpatialInterestManager();

        private readonly int _cellSize;
        private readonly Dictionary<int, ObserverInterestState> _observers = new(64);

        public SpatialInterestManager(int cellSize = 1)
        {
            _cellSize = cellSize <= 0 ? 1 : cellSize;
        }

        public readonly struct AoIInterestDelta
        {
            public AoIInterestDelta(List<int> enteredEntities, List<int> exitedEntities, List<int> stayedEntities)
            {
                EnteredEntities = enteredEntities ?? new List<int>(0);
                ExitedEntities = exitedEntities ?? new List<int>(0);
                StayedEntities = stayedEntities ?? new List<int>(0);
            }

            public List<int> EnteredEntities { get; }
            public List<int> ExitedEntities { get; }
            public List<int> StayedEntities { get; }
        }

        private sealed class ObserverInterestState
        {
            public int ObserverId;
            public int CenterX;
            public int CenterY;
            public int DungeonLevel;
            public int Radius;
            public readonly HashSet<int> KnownEntities = new(64);
        }

        /// <summary>
        /// Check if a 2D point lies within an AoI Manhattan or Chebyshev radius.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPointInAoI(int px, int py, int originX, int originY, int radius)
        {
            int dx = Math.Abs(px - originX);
            int dy = Math.Abs(py - originY);
            return dx <= radius && dy <= radius;
        }

        /// <summary>
        /// Check if a bounding box (e.g. room) intersects an AoI radius around an origin.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBoundsInAoI(int minX, int minY, int width, int height, int originX, int originY, int radius)
        {
            int maxX = minX + Math.Max(0, width - 1);
            int maxY = minY + Math.Max(0, height - 1);

            int closestX = Math.Max(minX, Math.Min(originX, maxX));
            int closestY = Math.Max(minY, Math.Min(originY, maxY));

            return IsPointInAoI(closestX, closestY, originX, originY, radius);
        }

        /// <summary>
        /// Get all entity IDs present in the AoI centered at (centerX, centerY, dungeonLevel) with given radius.
        /// Uses the SpatialHashGrid / LookupFacade.
        /// </summary>
        public List<int> QueryEntitiesInAoI(int centerX, int centerY, int dungeonLevel, int radius)
        {
            return SpatialHashGrid.Instance.GetEntitiesInRange(centerX, centerY, dungeonLevel, radius);
        }

        /// <summary>
        /// Update observer position and compute interest delta (entities that entered, exited, or persisted in view).
        /// Useful for diffing network updates for individual clients.
        /// </summary>
        public AoIInterestDelta UpdateObserver(int observerId, int centerX, int centerY, int dungeonLevel, int radius)
        {
            if (!_observers.TryGetValue(observerId, out var state))
            {
                state = new ObserverInterestState
                {
                    ObserverId = observerId
                };
                _observers[observerId] = state;
            }

            state.CenterX = centerX;
            state.CenterY = centerY;
            state.DungeonLevel = dungeonLevel;
            state.Radius = radius;

            var currentEntitiesList = QueryEntitiesInAoI(centerX, centerY, dungeonLevel, radius);
            var currentEntitiesSet = new HashSet<int>(currentEntitiesList);

            var entered = new List<int>(currentEntitiesList.Count);
            var stayed = new List<int>(currentEntitiesList.Count);
            var exited = new List<int>(state.KnownEntities.Count);

            foreach (var entity in currentEntitiesList)
            {
                if (state.KnownEntities.Contains(entity))
                {
                    stayed.Add(entity);
                }
                else
                {
                    entered.Add(entity);
                }
            }

            foreach (var previousEntity in state.KnownEntities)
            {
                if (!currentEntitiesSet.Contains(previousEntity))
                {
                    exited.Add(previousEntity);
                }
            }

            state.KnownEntities.Clear();
            state.KnownEntities.UnionWith(currentEntitiesSet);

            return new AoIInterestDelta(entered, exited, stayed);
        }

        /// <summary>
        /// Remove an observer from tracking.
        /// </summary>
        public void RemoveObserver(int observerId)
        {
            _observers.Remove(observerId);
        }

        /// <summary>
        /// Clear all observer tracking states.
        /// </summary>
        public void Clear()
        {
            _observers.Clear();
        }

        /// <summary>
        /// Filter an arbitrary collection of items that provide coordinates into those inside AoI.
        /// </summary>
        public static List<T> FilterItemsInAoI<T>(int originX, int originY, int radius, IReadOnlyList<T> items, Func<T, (int x, int y)> posSelector)
        {
            if (items == null || items.Count == 0)
                return new List<T>(0);

            var filtered = new List<T>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var pos = posSelector(item);
                if (IsPointInAoI(pos.x, pos.y, originX, originY, radius))
                {
                    filtered.Add(item);
                }
            }

            return filtered;
        }
    }
}
