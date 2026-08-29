using System.Collections.Generic;
using Unity.Entities;

namespace DunGen.ECS.Core
{
    /// <summary>
    /// Spatial hash grid for O(1) position-based entity lookups.
    /// Reduces collision detection from O(n²) to O(n * avg_bucket_size).
    /// 
    /// Usage:
    /// 1. Call UpdatePosition() when an entity moves
    /// 2. Use GetEntitiesAt() for O(1) same-cell checks
    /// 3. Use GetEntitiesInRange() for nearby entity queries
    /// </summary>
    public class SpatialHashGrid
    {
        private static SpatialHashGrid _instance;
        public static SpatialHashGrid Instance => _instance ??= new SpatialHashGrid();

        private readonly int _cellSize;
        
        // Cell hash -> entities at that cell
        private readonly Dictionary<long, HashSet<int>> _cellToEntities = new(1024);
        
        // Entity index -> current cell hash (for fast removal during position updates)  
        private readonly Dictionary<int, long> _entityToCell = new(256);
        
        // Entity index -> position cache for change detection
        private readonly Dictionary<int, (int x, int y, int level)> _entityPositions = new(256);

        public SpatialHashGrid(int cellSize = 1)
        {
            _cellSize = cellSize;
        }

        /// <summary>
        /// Compute cell hash from position. Includes dungeon level for 3D grid support.
        /// </summary>
        private long GetCellHash(int x, int y, int dungeonLevel)
        {
            int cellX = x / _cellSize;
            int cellY = y / _cellSize;
            // Pack into 64-bit: 20 bits for level, 22 bits each for x and y
            return ((long)dungeonLevel << 44) | ((long)(cellX + 0x200000) << 22) | (long)(cellY + 0x200000);
        }

        /// <summary>
        /// Update an entity's position in the spatial grid.
        /// Call this whenever an entity moves.
        /// </summary>
        public void UpdatePosition(int entityIndex, int x, int y, int dungeonLevel)
        {
            // Check if position actually changed
            if (_entityPositions.TryGetValue(entityIndex, out var oldPos) && 
                oldPos.x == x && oldPos.y == y && oldPos.level == dungeonLevel)
            {
                return; // No change
            }

            // Remove from old cell
            if (_entityToCell.TryGetValue(entityIndex, out var oldCellHash))
            {
                if (_cellToEntities.TryGetValue(oldCellHash, out var oldCell))
                {
                    oldCell.Remove(entityIndex);
                    if (oldCell.Count == 0)
                        _cellToEntities.Remove(oldCellHash);
                }
            }

            // Add to new cell
            var newCellHash = GetCellHash(x, y, dungeonLevel);
            if (!_cellToEntities.TryGetValue(newCellHash, out var newCell))
            {
                newCell = new HashSet<int>(4);
                _cellToEntities[newCellHash] = newCell;
            }
            newCell.Add(entityIndex);

            _entityToCell[entityIndex] = newCellHash;
            _entityPositions[entityIndex] = (x, y, dungeonLevel);
        }

        /// <summary>
        /// Remove an entity from the spatial grid.
        /// </summary>
        public void Remove(int entityIndex)
        {
            if (_entityToCell.TryGetValue(entityIndex, out var cellHash))
            {
                if (_cellToEntities.TryGetValue(cellHash, out var cell))
                {
                    cell.Remove(entityIndex);
                    if (cell.Count == 0)
                        _cellToEntities.Remove(cellHash);
                }
                _entityToCell.Remove(entityIndex);
            }
            _entityPositions.Remove(entityIndex);
        }

        /// <summary>
        /// O(1) lookup: Get all entities at the same position.
        /// Perfect for collision detection.
        /// </summary>
        public bool GetEntitiesAt(int x, int y, int dungeonLevel, out HashSet<int> entities)
        {
            var cellHash = GetCellHash(x, y, dungeonLevel);
            return _cellToEntities.TryGetValue(cellHash, out entities);
        }

        /// <summary>
        /// Get entities within Manhattan distance of a point.
        /// Returns entities in nearby cells for range-based queries.
        /// </summary>
        public List<int> GetEntitiesInRange(int centerX, int centerY, int dungeonLevel, int range)
        {
            var result = new List<int>(16);
            int cellRange = (range / _cellSize) + 1;

            int centerCellX = centerX / _cellSize;
            int centerCellY = centerY / _cellSize;

            for (int dx = -cellRange; dx <= cellRange; dx++)
            {
                for (int dy = -cellRange; dy <= cellRange; dy++)
                {
                    int cellX = (centerCellX + dx) * _cellSize;
                    int cellY = (centerCellY + dy) * _cellSize;
                    var cellHash = GetCellHash(cellX, cellY, dungeonLevel);

                    if (_cellToEntities.TryGetValue(cellHash, out var entities))
                    {
                        foreach (var entityIndex in entities)
                        {
                            if (_entityPositions.TryGetValue(entityIndex, out var pos))
                            {
                                int distance = System.Math.Abs(pos.x - centerX) + System.Math.Abs(pos.y - centerY);
                                if (distance <= range)
                                    result.Add(entityIndex);
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Find the closest entity to a position within max range.
        /// Returns -1 if no entity found.
        /// </summary>
        public int FindClosestEntity(int centerX, int centerY, int dungeonLevel, int maxRange, int excludeEntityIndex = -1)
        {
            int closestIndex = -1;
            int closestDistance = int.MaxValue;

            var candidates = GetEntitiesInRange(centerX, centerY, dungeonLevel, maxRange);
            foreach (var entityIndex in candidates)
            {
                if (entityIndex == excludeEntityIndex)
                    continue;

                if (_entityPositions.TryGetValue(entityIndex, out var pos))
                {
                    int distance = System.Math.Abs(pos.x - centerX) + System.Math.Abs(pos.y - centerY);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestIndex = entityIndex;
                    }
                }
            }

            return closestIndex;
        }

        /// <summary>
        /// Get cached position for an entity.
        /// </summary>
        public bool TryGetPosition(int entityIndex, out int x, out int y, out int level)
        {
            if (_entityPositions.TryGetValue(entityIndex, out var pos))
            {
                x = pos.x;
                y = pos.y;
                level = pos.level;
                return true;
            }

            x = y = level = 0;
            return false;
        }

        /// <summary>
        /// Clear all cached data.
        /// </summary>
        public void Clear()
        {
            _cellToEntities.Clear();
            _entityToCell.Clear();
            _entityPositions.Clear();
        }

        /// <summary>
        /// Get stats for debugging.
        /// </summary>
        public (int entityCount, int cellCount) GetStats()
        {
            return (_entityToCell.Count, _cellToEntities.Count);
        }
    }
}
