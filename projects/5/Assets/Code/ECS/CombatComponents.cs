using Unity.Entities;

namespace DunGen.ECS.Combat
{
    /// <summary>
    /// Core combat component tracking health, armor class, and combat state.
    /// Every combatant in the engine must have this component.
    /// </summary>
    public struct CombatComponent : IComponentData
    {
        /// <summary>Current health points. Health <= 0 means defeated.</summary>
        public int CurrentHealth;
        
        /// <summary>Maximum health points for this combatant.</summary>
        public int MaxHealth;
        
        /// <summary>Armor class - defense rating (higher is better).</summary>
        public int ArmorClass;
        
        /// <summary>Is this entity currently engaged in combat?</summary>
        public bool IsInCombat;
        
        /// <summary>Is this entity dead (health <= 0)?</summary>
        public bool IsDead => CurrentHealth <= 0;
        
        /// <summary>Current round number in this combat (starts at 1).</summary>
        public int CurrentRound;
        
        /// <summary>Has this entity already acted this round?</summary>
        public bool HasActedThisRound;
        
        /// <summary>Raw seed for initializing combat-specific RNG if needed.</summary>
        public uint CombatSeed;
        
        /// <summary>Unique identifier for the current combat session.</summary>
        public int CombatSessionId;
    }

    /// <summary>
    /// Tracks initiative and turn order for a combat participant.
    /// Populated when combat starts via InitiativeRoll.
    /// </summary>
    public struct InitiativeComponent : IComponentData
    {
        /// <summary>Initiative score (d20 + DEX modifier).</summary>
        public int InitiativeScore;
        
        /// <summary>Position in the turn queue (0 = first to act).</summary>
        public int TurnOrder;
        
        /// <summary>The d20 roll that determined initiative.</summary>
        public int D20Roll;
        
        /// <summary>DEX modifier added to d20 roll.</summary>
        public int DexModifier;
    }

    /// <summary>
    /// Combat-specific statistics derived from character attributes.
    /// Links a combatant's base stats to their combat effectiveness.
    /// </summary>
    public struct CombatStatsComponent : IComponentData
    {
        /// <summary>Strength modifier (added to melee attacks and melee damage).</summary>
        public int StrengthModifier;
        
        /// <summary>Dexterity modifier (added to ranged attacks, initiative, AC).</summary>
        public int DexterityModifier;
        
        /// <summary>Constitution modifier (added to health/defense).</summary>
        public int ConstitutionModifier;
        
        /// <summary>Intelligence modifier (added to spell attack rolls and spell save DC).</summary>
        public int IntelligenceModifier;
        
        /// <summary>Wisdom modifier (added to perception and spell save resists).</summary>
        public int WisdomModifier;
        
        /// <summary>Charisma modifier (added to social checks and some spells).</summary>
        public int CharismaModifier;
        
        /// <summary>Proficiency bonus (added to weapon attacks with proficiency).</summary>
        public int ProficiencyBonus;
        
        /// <summary>Current mana/spell slots remaining.</summary>
        public int CurrentMana;
        
        /// <summary>Maximum mana/spell slots.</summary>
        public int MaxMana;
        
        /// <summary>Number of actions available this turn.</summary>
        public int ActionsRemaining;
    }

    /// <summary>
    /// Damage resistances and vulnerabilities that modify incoming damage.
    /// Resistance halves damage, vulnerability doubles it.
    /// </summary>
    public struct DamageProfileComponent : IComponentData
    {
        // Damage type flags (could use enum, but component data must be blittable)
        /// <summary>Bitmask for physical damage resistance.</summary>
        public bool ResistPhysical;
        
        /// <summary>Bitmask for fire damage resistance.</summary>
        public bool ResistFire;
        
        /// <summary>Bitmask for cold damage resistance.</summary>
        public bool ResistCold;
        
        /// <summary>Bitmask for lightning damage resistance.</summary>
        public bool ResistLightning;
        
        /// <summary>Bitmask for acid damage resistance.</summary>
        public bool ResistAcid;
        
        /// <summary>Bitmask for poison damage resistance.</summary>
        public bool ResistPoison;
        
        /// <summary>Bitmask for psychic damage resistance.</summary>
        public bool ResistPsychic;
        
        /// <summary>Bitmask for radiant damage resistance.</summary>
        public bool ResistRadiant;
        
        /// <summary>Bitmask for necrotic damage resistance.</summary>
        public bool ResistNecrotic;
        
        /// <summary>Bitmask for force damage resistance.</summary>
        public bool ResistForce;
        
        // Vulnerabilities (take double damage)
        public bool VulnPhysical;
        public bool VulnFire;
        public bool VulnCold;
        public bool VulnLightning;
        public bool VulnAcid;
        public bool VulnPoison;
        public bool VulnPsychic;
        public bool VulnRadiant;
        public bool VulnNecrotic;
        public bool VulnForce;
        
        /// <summary>
        /// Calculate damage multiplier based on damage type and resistances.
        /// Returns 0.5 for resistance, 1.0 for normal, 2.0 for vulnerability.
        /// </summary>
        public float GetDamageMultiplier(string damageType)
        {
            return damageType switch
            {
                "Physical" => VulnPhysical ? 2.0f : (ResistPhysical ? 0.5f : 1.0f),
                "Fire" => VulnFire ? 2.0f : (ResistFire ? 0.5f : 1.0f),
                "Cold" => VulnCold ? 2.0f : (ResistCold ? 0.5f : 1.0f),
                "Lightning" => VulnLightning ? 2.0f : (ResistLightning ? 0.5f : 1.0f),
                "Acid" => VulnAcid ? 2.0f : (ResistAcid ? 0.5f : 1.0f),
                "Poison" => VulnPoison ? 2.0f : (ResistPoison ? 0.5f : 1.0f),
                "Psychic" => VulnPsychic ? 2.0f : (ResistPsychic ? 0.5f : 1.0f),
                "Radiant" => VulnRadiant ? 2.0f : (ResistRadiant ? 0.5f : 1.0f),
                "Necrotic" => VulnNecrotic ? 2.0f : (ResistNecrotic ? 0.5f : 1.0f),
                "Force" => VulnForce ? 2.0f : (ResistForce ? 0.5f : 1.0f),
                _ => 1.0f
            };
        }
    }

    /// <summary>
    /// Recent combat action stored for event replay and logging.
    /// Transient component, cleared each turn.
    /// </summary>
    public struct LastCombatActionComponent : IComponentData
    {
        /// <summary>Type of action (0=Attack, 1=Cast, 2=Move, etc).</summary>
        public int ActionType;
        
        /// <summary>Result of the action (0=Pending, 1=Success, 2=Failure).</summary>
        public int ActionResult;
        
        /// <summary>Damage/healing dealt (or 0 if not applicable).</summary>
        public int AmountDealt;
        
        /// <summary>Target entity, if applicable.</summary>
        public int TargetId;
        
        /// <summary>Timestamp in simulation ticks when action occurred.</summary>
        public uint Timestamp;
    }

    /// <summary>
    /// Stores the most recent d20 roll result for display and debugging.
    /// </summary>
    public struct RecentDiceRollComponent : IComponentData
    {
        /// <summary>The raw d20 roll (1-20).</summary>
        public int D20Result;
        
        /// <summary>Bonus applied to the roll.</summary>
        public int BonusApplied;
        
        /// <summary>Final result after bonus.</summary>
        public int FinalResult;
        
        /// <summary>What sort of roll? (0=Initiative, 1=Attack, 2=Saving, etc).</summary>
        public int RollType;
        
        /// <summary>Timestamp when this roll was made.</summary>
        public uint RollTimestamp;
    }
}
