using System.Collections.Generic;
using DunGen.ECS.Components;
using DunGen.ECS.Exploration;
using DunGen.Simulation.RNG;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DunGen.ECS.Generation
{
    /// <summary>
    /// Simple procedural dungeon generator using seed-based room placement.
    /// Generates a rectangular dungeon with simple room layout.
    /// </summary>
    public class SimpleDungeonGenerator
    {
        private readonly DeterministicRNG _rng;

        public SimpleDungeonGenerator(int seed)
        {
            _rng = new DeterministicRNG(seed);
        }

        /// <summary>Generate a dungeon level and its summary metadata.</summary>
        public DungeonLevelComponent GenerateLevel(int levelNumber, int width = 80, int height = 24)
        {
            // Create 3-5 rooms
            int roomCount = _rng.RollDice(3) + 2;  // 2-5 rooms
            
            int seedEnemyVariance = (int)(_rng.Seed % 3);
            int seedLootVariance = (int)((_rng.Seed / 3) % 2);

            var level = new DungeonLevelComponent
            {
                LevelNumber = levelNumber,
                Width = width,
                Height = height,
                Seed = (int)_rng.Seed,
                EnemyCount = roomCount * 2 + seedEnemyVariance,  // ~2 enemies per room
                LootCount = roomCount + seedLootVariance,        // ~1 loot per room
                IsGenerated = true
            };

            return level;
        }

        public List<DungeonTile> GenerateTiles(int levelNumber, int width = 80, int height = 24)
        {
            var tiles = new List<DungeonTile>(width * height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    tiles.Add(new DungeonTile
                    {
                        TileType = isBorder ? 0u : 1u,
                        LevelNumber = levelNumber,
                        GridX = x,
                        GridY = y,
                        IsWalkable = !isBorder
                    });
                }
            }

            return tiles;
        }

        /// <summary>Get a random walkable position.</summary>
        public (int x, int y) GetRandomSpawnPosition(int width, int height)
        {
            int x = _rng.RollDice(width - 4) + 2;   // Leave border
            int y = _rng.RollDice(height - 4) + 2;
            return (x, y);
        }

        /// <summary>Check if position is walkable inside the generated dungeon bounds.</summary>
        public bool IsWalkable(int x, int y, int width, int height)
        {
            return x > 0 && x < width - 1 && y > 0 && y < height - 1;
        }

        /// <summary>Get distance between two points (Manhattan).</summary>
        public int GetDistance(int x1, int y1, int x2, int y2)
        {
            return Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);
        }

        /// <summary>Simple pathfinding - move one step towards target.</summary>
        public (int dx, int dy) GetMoveTowards(int x1, int y1, int x2, int y2)
        {
            int dx = 0, dy = 0;
            
            if (x1 < x2) dx = 1;
            else if (x1 > x2) dx = -1;
            
            if (y1 < y2) dy = 1;
            else if (y1 > y2) dy = -1;
            
            return (dx, dy);
        }
    }

    /// <summary>
    /// Generates initial dungeon levels and populates with enemies/loot.
    /// </summary>
    public partial class DungeonGenerationSystem : SystemBase
    {
        private SimpleDungeonGenerator _generator;
        private int _currentSeed = 12345;

        public void Initialize(int seed)
        {
            _currentSeed = seed;
            _generator = new SimpleDungeonGenerator(seed);
        }

        protected override void OnUpdate()
        {
            if (_generator == null)
                return;

            var query = EntityManager.CreateEntityQuery(typeof(DungeonLevelComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var levels = query.ToComponentDataArray<DungeonLevelComponent>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var level = levels[i];
                if (level.IsGenerated)
                    continue;

                level = _generator.GenerateLevel(level.LevelNumber, level.Width, level.Height);
                EntityManager.SetComponentData(entities[i], level);

                foreach (var tile in _generator.GenerateTiles(level.LevelNumber, level.Width, level.Height))
                {
                    var tileEntity = EntityManager.CreateEntity(typeof(DungeonTile));
                    EntityManager.SetComponentData(tileEntity, tile);
                }
            }
        }
    }
}
