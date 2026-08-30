#if !UNITY_5_3_OR_NEWER
namespace Authoritative.Services
{
    /// <summary>
    /// Canonical tag vocabularies shared across generators, persistence and the
    /// REST/action layer. Values must stay in harmony with the Unity client
    /// catalog (NpcPersonalityGenerator bias cases and GameSession emitters);
    /// PersistenceContractTests asserts: server enemy archetypes are a subset of
    /// the Unity NPC bias cases, and the world-event types match what the Unity
    /// client emits.
    /// </summary>
    public static class PersistenceTagCatalog
    {
        /// <summary>Enemy archetype strings written to dungeon_enemies.archetype.</summary>
        public static readonly string[] EnemyArchetypes =
        {
            "goblin", "skeleton", "cultist", "wolf", "bandit"
        };

        /// <summary>Item type strings written to dungeon_loot.item_type (pipeline world).</summary>
        public static readonly string[] LootItemTypes =
        {
            "sword", "shield", "potion", "bow", "staff", "armor"
        };

        /// <summary>Item type used by the fallback world factory; not part of the loot store vocabulary.</summary>
        public const string FallbackLootItemType = "trinket";

        /// <summary>Item types recognized by mastery beyond the loot-store vocabulary.</summary>
        public static readonly string[] MasteryExtraItemTypes =
        {
            "accessory", "dagger"
        };

        /// <summary>Event type emitted by the Unity client for per-entity component snapshots.</summary>
        public const string EntityStateSnapshotEventType = "entity.state.snapshot";

        /// <summary>Prefix used to group system lifecycle events (e.g. system.execute).</summary>
        public const string SystemEventTypePrefix = "system.";
    }
}
#endif