using System;
using System.Collections.Generic;
using DunGen.ECS.Components;

namespace DunGen.ECS.Generation
{
    public sealed class DungeonLevel
    {
        public int LevelNumber { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsGenerated { get; set; }
        public int EnemyCount { get; set; }
        public int LootCount { get; set; }
    }

    /// <summary>
    /// Lightweight deterministic dungeon helper used by tests and GameSession movement checks.
    /// </summary>
    public sealed class SimpleDungeonGenerator
    {
        private readonly Random _rng;

        public SimpleDungeonGenerator(int seed)
        {
            _rng = new Random(seed);
        }

        public DungeonLevel GenerateLevel(int level, int width, int height)
        {
            return new DungeonLevel
            {
                LevelNumber = level,
                Width = width,
                Height = height,
                IsGenerated = true,
                EnemyCount = Math.Max(1, level + _rng.Next(1, 4)),
                LootCount = Math.Max(1, level + _rng.Next(0, 3))
            };
        }

        public DungeonLevel GenerateLevel(int level)
        {
            return GenerateLevel(level, 80, 24);
        }

        public (int x, int y) GetRandomSpawnPosition(int width, int height)
        {
            var x = _rng.Next(2, Math.Max(3, width - 2));
            var y = _rng.Next(2, Math.Max(3, height - 2));
            return (x, y);
        }

        public bool IsWalkable(int x, int y, int width, int height)
        {
            return x > 0 && y > 0 && x < width - 1 && y < height - 1;
        }

        public int GetDistance(int x1, int y1, int x2, int y2)
        {
            return Math.Abs(x2 - x1) + Math.Abs(y2 - y1);
        }

        public (int dx, int dy) GetMoveTowards(int x1, int y1, int x2, int y2)
        {
            var dx = Math.Sign(x2 - x1);
            var dy = Math.Sign(y2 - y1);
            return (dx, dy);
        }

        public List<DungeonTile> GenerateTiles(int level, int width, int height)
        {
            var tiles = new List<DungeonTile>(width * height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var walkable = IsWalkable(x, y, width, height);
                    tiles.Add(new DungeonTile
                    {
                        LevelNumber = level,
                        GridX = x,
                        GridY = y,
                        IsWalkable = walkable,
                        TileType = walkable ? 1u : 0u
                    });
                }
            }

            return tiles;
        }
    }
}
