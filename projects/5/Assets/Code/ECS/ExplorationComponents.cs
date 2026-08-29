using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DunGen.ECS.Exploration
{
    /// <summary>Position in the dungeon - tile-based coordinates.</summary>
    public struct PositionComponent : IComponentData
    {
        public int X;
        public int Y;
        public int DungeonLevel;

        public bool Equals(PositionComponent other) => X == other.X && Y == other.Y && DungeonLevel == other.DungeonLevel;
    }

    /// <summary>Movement speed and pathfinding.</summary>
    public struct MovementComponent : IComponentData
    {
        public int MovementSpeed;  // Tiles per turn
        public int TilesMovedThisTurn;
    }

    /// <summary>Visibility - what the entity can see.</summary>
    public struct VisionComponent : IComponentData
    {
        public int VisionRange;  // Tiles
        public bool IsPlayerControlled;
    }

    /// <summary>Experience and leveling.</summary>
    public struct ExperienceComponent : IComponentData
    {
        public int CurrentXP;
        public int XPToNextLevel;
        public int Level;

        public bool CanLevelUp() => CurrentXP >= XPToNextLevel;

        public void LevelUp()
        {
            if (CanLevelUp())
            {
                CurrentXP -= XPToNextLevel;
                Level++;
                XPToNextLevel = Level * 100;  // Simplified formula
            }
        }
    }

    /// <summary>Loot drop on death.</summary>
    public struct LootTableComponent : IComponentData
    {
        public int GoldDropMin;
        public int GoldDropMax;
        public int LootTableId;  // Reference to loot table
        public bool DropOnDeath;
    }

    /// <summary>Dungeon level metadata.</summary>
    public struct DungeonLevelComponent : IComponentData
    {
        public int LevelNumber;
        public int Width;
        public int Height;
        public int Seed;
        public int EnemyCount;
        public int LootCount;
        public bool IsGenerated;
    }

    /// <summary>Item in inventory or on ground.</summary>
    public struct ItemComponent : IComponentData
    {
        public int ItemId;
        public FixedString64Bytes ItemName;
        public int Quantity;
        public bool IsEquipped;
        public bool IsOnGround;
    }

    /// <summary>Gold/currency.</summary>
    public struct CurrencyComponent : IComponentData
    {
        public int Gold;

        public void AddGold(int amount) => Gold += amount;
        public bool CanSpend(int amount) => Gold >= amount;
        public void Spend(int amount) { if (CanSpend(amount)) Gold -= amount; }
    }
}
