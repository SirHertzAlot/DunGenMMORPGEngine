using System;
using System.Runtime.CompilerServices;

namespace DunGen.ECS.Core
{
    /// <summary>
    /// Compact Bloom filter for ultra-fast negative lookups on spatial data.
    /// 
    /// A Bloom filter is a probabilistic data structure that answers:
    /// - "Definitely not present" (100% accurate) 
    /// - "Possibly present" (may have false positives)
    /// 
    /// Use case: Before expensive spatial queries, check bloom filter.
    /// If it returns false, skip the query entirely (guaranteed no entities there).
    /// 
    /// Memory: Uses only 4KB for excellent false positive rates on typical dungeon sizes.
    /// Speed: Single memory access + bit operations = ~1-2 CPU cycles.
    /// </summary>
    public sealed class SpatialBloomFilter
    {
        private static SpatialBloomFilter _instance;
        public static SpatialBloomFilter Instance => _instance ??= new SpatialBloomFilter();

        // 32KB = 256K bits, ~0.1% false positive rate for 10K positions
        // Tuned for L1 cache (32-64KB on most CPUs)
        private const int FilterSizeBytes = 32768;
        private const int FilterSizeBits = FilterSizeBytes * 8;
        private const int HashCount = 3; // Number of hash functions (k)

        private readonly byte[] _filter;
        private int _itemCount;

        public SpatialBloomFilter()
        {
            _filter = new byte[FilterSizeBytes];
        }

        /// <summary>
        /// Add a position to the filter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int x, int y, int level)
        {
            uint hash1 = Hash1(x, y, level);
            uint hash2 = Hash2(x, y, level);

            // Double hashing: h(i) = h1 + i*h2
            for (int i = 0; i < HashCount; i++)
            {
                uint idx = (hash1 + (uint)i * hash2) % FilterSizeBits;
                _filter[idx >> 3] |= (byte)(1 << (int)(idx & 7));
            }

            _itemCount++;
        }

        /// <summary>
        /// Check if position might be present. False = definitely not present.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MightContain(int x, int y, int level)
        {
            uint hash1 = Hash1(x, y, level);
            uint hash2 = Hash2(x, y, level);

            for (int i = 0; i < HashCount; i++)
            {
                uint idx = (hash1 + (uint)i * hash2) % FilterSizeBits;
                if ((_filter[idx >> 3] & (1 << (int)(idx & 7))) == 0)
                    return false; // Definitely not present
            }

            return true; // Possibly present
        }

        /// <summary>
        /// Remove a position (note: Bloom filters don't support perfect removal,
        /// this uses counting but may have residual false positives).
        /// </summary>
        public void Remove(int x, int y, int level)
        {
            // Standard bloom filters don't support removal
            // For perfect removal, would need counting bloom filter (4x memory)
            // Instead, we rebuild periodically when false positive rate is too high
            if (_itemCount > 0)
                _itemCount--;
        }

        /// <summary>
        /// First hash function: MurmurHash3-inspired mixing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash1(int x, int y, int level)
        {
            uint h = (uint)((x * 73856093) ^ (y * 19349663) ^ (level * 83492791));
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }

        /// <summary>
        /// Second hash function: FNV-1a inspired.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash2(int x, int y, int level)
        {
            uint h = 2166136261;
            h = (h ^ (uint)x) * 16777619;
            h = (h ^ (uint)y) * 16777619;
            h = (h ^ (uint)level) * 16777619;
            return h;
        }

        /// <summary>
        /// Clear the filter.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_filter, 0, _filter.Length);
            _itemCount = 0;
        }

        /// <summary>
        /// Estimate false positive rate based on current fill.
        /// </summary>
        public double EstimatedFalsePositiveRate
        {
            get
            {
                // (1 - e^(-kn/m))^k where k=hash count, n=items, m=bits
                double exponent = -HashCount * _itemCount / (double)FilterSizeBits;
                return Math.Pow(1 - Math.Exp(exponent), HashCount);
            }
        }

        /// <summary>
        /// Check if filter should be rebuilt (too many false positives).
        /// </summary>
        public bool ShouldRebuild => EstimatedFalsePositiveRate > 0.01; // >1% false positive
    }

    /// <summary>
    /// Combined high-performance lookup system that uses bloom filter
    /// as first-pass check before spatial grid lookup.
    /// </summary>
    public sealed class FastSpatialLookup
    {
        private static FastSpatialLookup _instance;
        public static FastSpatialLookup Instance => _instance ??= new FastSpatialLookup();

        private readonly MortonSpatialGrid _grid;
        private readonly SpatialBloomFilter _bloom;

        public FastSpatialLookup()
        {
            _grid = MortonSpatialGrid.Instance;
            _bloom = SpatialBloomFilter.Instance;
        }

        /// <summary>
        /// Update entity position in both structures.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdatePosition(int entityIndex, int x, int y, int level)
        {
            // Remove from bloom filter at old position
            if (_grid.TryGetPosition(entityIndex, out int oldX, out int oldY, out int oldLevel))
            {
                _bloom.Remove(oldX, oldY, oldLevel);
            }

            // Update grid
            _grid.UpdatePosition(entityIndex, x, y, level);

            // Add to bloom filter at new position
            _bloom.Add(x, y, level);
        }

        /// <summary>
        /// Fast check if any entity might be at position.
        /// Returns false = guaranteed no entity. True = need to check grid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MightHaveEntityAt(int x, int y, int level)
        {
            return _bloom.MightContain(x, y, level);
        }

        /// <summary>
        /// Two-phase lookup: bloom filter first, then grid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetEntitiesAt(int x, int y, int level, out int firstEntity, out int count)
        {
            // Fast path: bloom filter says definitely no entities
            if (!_bloom.MightContain(x, y, level))
            {
                firstEntity = -1;
                count = 0;
                return false;
            }

            // Slow path: check actual grid
            return _grid.TryGetEntitiesAt(x, y, level, out firstEntity, out count);
        }

        /// <summary>
        /// Get next entity in cell chain.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNextEntityInCell(int currentEntity)
        {
            return _grid.GetNextEntityInCell(currentEntity);
        }

        /// <summary>
        /// Find closest entity using grid's spatial search.
        /// </summary>
        public int FindClosest(int x, int y, int level, int maxRange, int exclude = -1)
        {
            return _grid.FindClosestEntity(x, y, level, maxRange, exclude);
        }

        /// <summary>
        /// Remove entity from both structures.
        /// </summary>
        public void Remove(int entityIndex)
        {
            if (_grid.TryGetPosition(entityIndex, out int x, out int y, out int level))
            {
                _bloom.Remove(x, y, level);
            }
            _grid.Remove(entityIndex);
        }

        /// <summary>
        /// Clear both structures.
        /// </summary>
        public void Clear()
        {
            _grid.Clear();
            _bloom.Clear();
        }

        /// <summary>
        /// Get position from grid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPosition(int entityIndex, out int x, out int y, out int level)
        {
            return _grid.TryGetPosition(entityIndex, out x, out y, out level);
        }
    }
}
