using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DunGen.ECS.Core
{
    /// <summary>
    /// Ultra-fast spatial grid using Morton codes (Z-order curves) for cache-coherent lookups.
    /// Morton encoding interleaves x/y bits, giving O(1) spatial hashing with excellent
    /// cache locality for nearby positions.
    /// 
    /// Memory layout optimized for L1/L2 cache (64-byte cache lines).
    /// Uses direct array indexing instead of Dictionary for bounded coordinate spaces.
    /// 
    /// Complexity: O(1) insert, O(1) lookup, O(k) range query where k = entities in range
    /// Space: O(W*H) for grid + O(N) for entity data where N = max entities
    /// </summary>
    public sealed class MortonSpatialGrid
    {
        private static MortonSpatialGrid _instance;
        public static MortonSpatialGrid Instance => _instance ??= new MortonSpatialGrid();

        // Configuration - tune for your dungeon size
        private const int MaxWidth = 256;      // Max dungeon width
        private const int MaxHeight = 256;     // Max dungeon height  
        private const int MaxLevels = 16;      // Max dungeon levels
        private const int MaxEntities = 4096;  // Max concurrent entities
        private const int CellCapacity = 8;    // Max entities per cell (most cells have 0-2)

        // Compact entity position: packed into 32 bits
        // [level:4][y:12][x:12][valid:4] - supports 16 levels, 4096x4096 coords
        private readonly uint[] _entityPositions;

        // Direct-indexed grid cells: [mortonCode] -> first entity in cell chain
        // Using ushort for entity indices (max 65535 entities)
        private readonly ushort[] _gridFirstEntity;

        // Entity chain links for cells with multiple entities
        private readonly ushort[] _entityNextInCell;

        // Reverse lookup: entity -> cell morton code
        private readonly uint[] _entityCell;

        // Stats for monitoring
        private int _entityCount;
        private int _maxCellOccupancy;

        public MortonSpatialGrid()
        {
            _entityPositions = new uint[MaxEntities];
            _gridFirstEntity = new ushort[MaxWidth * MaxHeight * MaxLevels];
            _entityNextInCell = new ushort[MaxEntities];
            _entityCell = new uint[MaxEntities];

            // Initialize grid with "no entity" marker (0xFFFF)
            Array.Fill(_gridFirstEntity, ushort.MaxValue);
            Array.Fill(_entityNextInCell, ushort.MaxValue);
        }

        /// <summary>
        /// Morton encode 2D coordinates. Interleaves bits of x and y for spatial locality.
        /// Uses magic-number bit manipulation for branchless O(1) encoding.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MortonEncode2D(uint x, uint y)
        {
            // Spread bits of x: 0b11111111 -> 0b0101010101010101
            x = (x | (x << 8)) & 0x00FF00FF;
            x = (x | (x << 4)) & 0x0F0F0F0F;
            x = (x | (x << 2)) & 0x33333333;
            x = (x | (x << 1)) & 0x55555555;

            // Spread bits of y
            y = (y | (y << 8)) & 0x00FF00FF;
            y = (y | (y << 4)) & 0x0F0F0F0F;
            y = (y | (y << 2)) & 0x33333333;
            y = (y | (y << 1)) & 0x55555555;

            return x | (y << 1);
        }

        /// <summary>
        /// Decode morton code back to x,y coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MortonDecode2D(uint morton, out uint x, out uint y)
        {
            x = morton & 0x55555555;
            x = (x | (x >> 1)) & 0x33333333;
            x = (x | (x >> 2)) & 0x0F0F0F0F;
            x = (x | (x >> 4)) & 0x00FF00FF;
            x = (x | (x >> 8)) & 0x0000FFFF;

            y = (morton >> 1) & 0x55555555;
            y = (y | (y >> 1)) & 0x33333333;
            y = (y | (y >> 2)) & 0x0F0F0F0F;
            y = (y | (y >> 4)) & 0x00FF00FF;
            y = (y | (y >> 8)) & 0x0000FFFF;
        }

        /// <summary>
        /// Pack position into compact 32-bit format.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackPosition(int x, int y, int level)
        {
            return ((uint)(level & 0xF) << 28) | ((uint)(y & 0xFFF) << 16) | ((uint)(x & 0xFFF) << 4) | 0x1;
        }

        /// <summary>
        /// Unpack position from compact format.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UnpackPosition(uint packed, out int x, out int y, out int level)
        {
            level = (int)(packed >> 28) & 0xF;
            y = (int)(packed >> 16) & 0xFFF;
            x = (int)(packed >> 4) & 0xFFF;
        }

        /// <summary>
        /// Get grid index for position (combines morton code with level offset).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetGridIndex(int x, int y, int level)
        {
            uint morton = MortonEncode2D((uint)(x & 0xFF), (uint)(y & 0xFF));
            return morton + (uint)(level * MaxWidth * MaxHeight);
        }

        /// <summary>
        /// Update entity position. O(1) amortized.
        /// </summary>
        public void UpdatePosition(int entityIndex, int x, int y, int level)
        {
            if (entityIndex < 0 || entityIndex >= MaxEntities)
                return;

            ushort entity = (ushort)entityIndex;
            uint newPacked = PackPosition(x, y, level);

            // Check if position unchanged
            uint oldPacked = _entityPositions[entity];
            if (oldPacked == newPacked)
                return;

            // Remove from old cell if present
            if ((oldPacked & 0x1) != 0)
            {
                UnpackPosition(oldPacked, out int oldX, out int oldY, out int oldLevel);
                RemoveFromCell(entity, oldX, oldY, oldLevel);
            }

            // Add to new cell
            _entityPositions[entity] = newPacked;
            AddToCell(entity, x, y, level);
            _entityCount = Math.Max(_entityCount, entityIndex + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddToCell(ushort entity, int x, int y, int level)
        {
            uint gridIdx = GetGridIndex(x, y, level);
            _entityCell[entity] = gridIdx;

            // Link into cell's entity chain (linked list insert at head)
            _entityNextInCell[entity] = _gridFirstEntity[gridIdx];
            _gridFirstEntity[gridIdx] = entity;
        }

        private void RemoveFromCell(ushort entity, int x, int y, int level)
        {
            uint gridIdx = GetGridIndex(x, y, level);

            // Unlink from cell's entity chain
            ushort prev = ushort.MaxValue;
            ushort current = _gridFirstEntity[gridIdx];

            while (current != ushort.MaxValue)
            {
                if (current == entity)
                {
                    if (prev == ushort.MaxValue)
                        _gridFirstEntity[gridIdx] = _entityNextInCell[current];
                    else
                        _entityNextInCell[prev] = _entityNextInCell[current];

                    _entityNextInCell[current] = ushort.MaxValue;
                    return;
                }

                prev = current;
                current = _entityNextInCell[current];
            }
        }

        /// <summary>
        /// Remove entity from grid entirely.
        /// </summary>
        public void Remove(int entityIndex)
        {
            if (entityIndex < 0 || entityIndex >= MaxEntities)
                return;

            ushort entity = (ushort)entityIndex;
            uint packed = _entityPositions[entity];

            if ((packed & 0x1) != 0)
            {
                UnpackPosition(packed, out int x, out int y, out int level);
                RemoveFromCell(entity, x, y, level);
            }

            _entityPositions[entity] = 0;
        }

        /// <summary>
        /// O(1) check if any entities at position. Returns count and first entity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetEntitiesAt(int x, int y, int level, out int firstEntity, out int count)
        {
            uint gridIdx = GetGridIndex(x, y, level);
            ushort current = _gridFirstEntity[gridIdx];

            if (current == ushort.MaxValue)
            {
                firstEntity = -1;
                count = 0;
                return false;
            }

            firstEntity = current;
            count = 1;

            // Count entities (usually 1-2, rarely more)
            while (_entityNextInCell[current] != ushort.MaxValue)
            {
                current = _entityNextInCell[current];
                count++;
            }

            return true;
        }

        /// <summary>
        /// Iterate entities at a cell. Use with GetNextEntityInCell.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetFirstEntityAt(int x, int y, int level)
        {
            uint gridIdx = GetGridIndex(x, y, level);
            ushort first = _gridFirstEntity[gridIdx];
            return first == ushort.MaxValue ? -1 : first;
        }

        /// <summary>
        /// Get next entity in cell chain. Returns -1 when done.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNextEntityInCell(int currentEntity)
        {
            if (currentEntity < 0 || currentEntity >= MaxEntities)
                return -1;

            ushort next = _entityNextInCell[currentEntity];
            return next == ushort.MaxValue ? -1 : next;
        }

        /// <summary>
        /// Get cached position for entity. O(1).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPosition(int entityIndex, out int x, out int y, out int level)
        {
            if (entityIndex < 0 || entityIndex >= MaxEntities)
            {
                x = y = level = 0;
                return false;
            }

            uint packed = _entityPositions[entityIndex];
            if ((packed & 0x1) == 0)
            {
                x = y = level = 0;
                return false;
            }

            UnpackPosition(packed, out x, out y, out level);
            return true;
        }

        /// <summary>
        /// Find closest entity using expanding square search pattern.
        /// Leverages Morton code's spatial locality for cache-efficient iteration.
        /// </summary>
        public int FindClosestEntity(int centerX, int centerY, int level, int maxRange, int excludeEntity = -1)
        {
            int closestEntity = -1;
            int closestDistSq = int.MaxValue;

            // Expanding square search - check cells in morton order for cache locality
            for (int ring = 0; ring <= maxRange; ring++)
            {
                // Only check perimeter of ring (inner cells already checked)
                int minX = Math.Max(0, centerX - ring);
                int maxX = Math.Min(MaxWidth - 1, centerX + ring);
                int minY = Math.Max(0, centerY - ring);
                int maxY = Math.Min(MaxHeight - 1, centerY + ring);

                // Check ring perimeter
                for (int x = minX; x <= maxX; x++)
                {
                    CheckCell(x, minY);
                    if (ring > 0)
                        CheckCell(x, maxY);
                }

                if (ring > 0)
                {
                    for (int y = minY + 1; y < maxY; y++)
                    {
                        CheckCell(minX, y);
                        CheckCell(maxX, y);
                    }
                }

                // Early exit if we found something in this ring
                if (closestEntity >= 0 && closestDistSq <= ring * ring)
                    break;
            }

            return closestEntity;

            void CheckCell(int cx, int cy)
            {
                int entity = GetFirstEntityAt(cx, cy, level);
                while (entity >= 0)
                {
                    if (entity != excludeEntity && TryGetPosition(entity, out int ex, out int ey, out _))
                    {
                        int dx = ex - centerX;
                        int dy = ey - centerY;
                        int distSq = dx * dx + dy * dy;

                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            closestEntity = entity;
                        }
                    }

                    entity = GetNextEntityInCell(entity);
                }
            }
        }

        /// <summary>
        /// Clear all data.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_entityPositions, 0, _entityPositions.Length);
            Array.Fill(_gridFirstEntity, ushort.MaxValue);
            Array.Fill(_entityNextInCell, ushort.MaxValue);
            Array.Clear(_entityCell, 0, _entityCell.Length);
            _entityCount = 0;
        }

        public (int entities, int cells) GetStats() => (_entityCount, _maxCellOccupancy);
    }
}
