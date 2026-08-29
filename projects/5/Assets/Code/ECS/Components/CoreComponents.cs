using Unity.Entities;

namespace DunGen.ECS.Components
{
    /// <summary>Component: 3D world position.</summary>
    public struct Position : IComponentData
    {
        public float X;
        public float Y;
        public float Z;
    }

    /// <summary>Component: Character/Entity name.</summary>
    public struct Name : IComponentData
    {
        public static string Values { get; set; } // Using static as workaround for fixed string
    }

    /// <summary>Component: Health points.</summary>
    public struct Health : IComponentData
    {
        public int Current;
        public int Max;
    }

    /// <summary>Component: Character statistics (D&D ability scores).</summary>
    public struct Stats : IComponentData
    {
        public int Strength;      // STR
        public int Dexterity;     // DEX
        public int Constitution;  // CON
        public int Intelligence;  // INT
        public int Wisdom;        // WIS
        public int Charisma;      // CHA

        public int GetModifier(int abilityScore) => (abilityScore - 10) / 2;

        public int GetStrengthMod() => GetModifier(Strength);
        public int GetDexterityMod() => GetModifier(Dexterity);
        public int GetConstitutionMod() => GetModifier(Constitution);
        public int GetIntelligenceMod() => GetModifier(Intelligence);
        public int GetWisdomMod() => GetModifier(Wisdom);
        public int GetCharismaMod() => GetModifier(Charisma);
    }

    /// <summary>Component: Armor class (defense value).</summary>
    public struct ArmorClass : IComponentData
    {
        public int Value;
    }

    /// <summary>Component: Experience points and level.</summary>
    public struct Experience : IComponentData
    {
        public uint Current;
        public uint NextLevelThreshold;
        public int Level;
    }

    /// <summary>Component: Marks entity as a player character.</summary>
    public struct PlayerComponent : IComponentData
    {
    }

    /// <summary>Component: Marks entity as an NPC/enemy.</summary>
    public struct NPCComponent : IComponentData
    {
        public uint Faction; // Faction ID for grouping alliances/enemies
    }

    /// <summary>Component: Velocity for movement.</summary>
    public struct Velocity : IComponentData
    {
        public float X;
        public float Y;
        public float Z;
    }

    /// <summary>Component: Entity equipped items/inventory slot.</summary>
    public struct InventorySlot : IComponentData
    {
        public Entity ItemEntity;
        public int SlotIndex;
    }

    /// <summary>Component: Mana/Magic resource.</summary>
    public struct Mana : IComponentData
    {
        public int Current;
        public int Max;
    }

    /// <summary>Component: Status effect (poison, stun, buff, etc.).</summary>
    public struct StatusEffect : IComponentData
    {
        public uint EffectType;
        public int Duration; // frames remaining
        public int Power;
    }

    /// <summary>Component: Turn-based action queue.</summary>
    public struct ActionQueue : IComponentData
    {
        public uint ActionType;  // Type of action (Move, Attack, Cast, etc.)
        public int Priority;     // Priority in turn order
        public uint FrameQueued;
    }

    /// <summary>Component: Combat ready state (in combat or not).</summary>
    public struct CombatState : IComponentData
    {
        public bool InCombat;
        public uint CombatRoundNumber;
        public int TurnOrder; // Lower = acts sooner
    }

    /// <summary>Component: Items dropped by defeated enemies or placed in world.</summary>
    public struct LootItem : IComponentData
    {
        public uint ItemId;
        public int Rarity; // 0=common, 1=uncommon, 2=rare, 3=legendary
        public int Value;
    }

    /// <summary>Component: Tile type in dungeon grid.</summary>
    public struct DungeonTile : IComponentData
    {
        public uint TileType; // 0=wall, 1=floor, 2=door, 3=stairs, etc.
        public int LevelNumber;
        public int GridX;
        public int GridY;
        public bool IsWalkable;
    }
}
