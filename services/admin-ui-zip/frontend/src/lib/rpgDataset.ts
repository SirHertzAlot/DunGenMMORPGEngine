/**
 * RPG Dataset - Static Reference Dictionary for Context-Aware Loot Generation
 * 
 * This module provides a comprehensive RPG dataset that serves as a static reference
 * for weapon-attack compatibility, sentient item attributes, faction influences,
 * and player archetype alignment.
 */

import { debugLogger } from './debugLogger';

/**
 * Weapon Categories
 */
export type WeaponCategory = 
  | 'sword' | 'axe' | 'bow' | 'staff' | 'dagger' | 'mace' 
  | 'spear' | 'wand' | 'crossbow' | 'hammer' | 'shield';

/**
 * Attack Types
 */
export type AttackType = 
  | 'slash' | 'pierce' | 'blunt' | 'magic' | 'ranged' 
  | 'elemental' | 'holy' | 'dark' | 'poison' | 'physical';

/**
 * Weapon-Attack Compatibility Matrix
 */
export const WEAPON_ATTACK_COMPATIBILITY: Record<WeaponCategory, AttackType[]> = {
  sword: ['slash', 'pierce', 'physical'],
  axe: ['slash', 'blunt', 'physical'],
  bow: ['ranged', 'pierce', 'physical'],
  staff: ['magic', 'blunt', 'elemental'],
  dagger: ['pierce', 'slash', 'poison'],
  mace: ['blunt', 'holy', 'physical'],
  spear: ['pierce', 'physical'],
  wand: ['magic', 'elemental', 'dark'],
  crossbow: ['ranged', 'pierce', 'physical'],
  hammer: ['blunt', 'physical'],
  shield: ['blunt', 'physical'],
};

/**
 * Sentient Item Personality Types
 */
export type SentientPersonality = 
  | 'noble' | 'chaotic' | 'wise' | 'vengeful' | 'protective' 
  | 'ambitious' | 'mysterious' | 'playful' | 'ancient' | 'corrupted';

/**
 * Sentient Item Behavior States
 */
export type SentientBehaviorState = 
  | 'dormant' | 'awakening' | 'active' | 'bonded' | 'conflicted' 
  | 'rebellious' | 'ascended' | 'corrupting' | 'evolving';

/**
 * Sentient Item Evolution Paths
 */
export interface SentientEvolutionPath {
  stage: number;
  name: string;
  requirements: string[];
  bonuses: string[];
  conflicts: SentientPersonality[];
}

export const SENTIENT_EVOLUTION_PATHS: Record<SentientPersonality, SentientEvolutionPath[]> = {
  noble: [
    {
      stage: 1,
      name: 'Honorable Awakening',
      requirements: ['Complete 10 honorable quests', 'Spare 5 enemies'],
      bonuses: ['Holy damage +20%', 'Charisma +5'],
      conflicts: ['chaotic', 'vengeful', 'corrupted'],
    },
    {
      stage: 2,
      name: 'Champion of Light',
      requirements: ['Defeat 3 dark lords', 'Protect 20 innocents'],
      bonuses: ['Holy damage +40%', 'Ally healing aura', 'Charisma +10'],
      conflicts: ['chaotic', 'vengeful', 'corrupted'],
    },
  ],
  chaotic: [
    {
      stage: 1,
      name: 'Unpredictable Force',
      requirements: ['Make 15 random choices', 'Cause 10 unexpected outcomes'],
      bonuses: ['Random critical strikes', 'Chaos damage +15%'],
      conflicts: ['noble', 'wise', 'protective'],
    },
    {
      stage: 2,
      name: 'Avatar of Entropy',
      requirements: ['Embrace 5 major chaos events', 'Reject 10 orders'],
      bonuses: ['Chaos damage +35%', 'Reality distortion', 'Unpredictable effects'],
      conflicts: ['noble', 'wise', 'protective'],
    },
  ],
  wise: [
    {
      stage: 1,
      name: 'Sage Companion',
      requirements: ['Learn 20 spells', 'Solve 10 puzzles'],
      bonuses: ['Magic power +25%', 'Mana efficiency +15%'],
      conflicts: ['chaotic', 'vengeful', 'playful'],
    },
    {
      stage: 2,
      name: 'Ancient Oracle',
      requirements: ['Master 50 spells', 'Unlock 5 secrets'],
      bonuses: ['Magic power +50%', 'Foresight ability', 'Wisdom +15'],
      conflicts: ['chaotic', 'vengeful', 'playful'],
    },
  ],
  vengeful: [
    {
      stage: 1,
      name: 'Wrath Unleashed',
      requirements: ['Defeat 25 enemies', 'Never retreat'],
      bonuses: ['Damage vs enemies +30%', 'Rage mode'],
      conflicts: ['noble', 'protective', 'wise'],
    },
    {
      stage: 2,
      name: 'Harbinger of Retribution',
      requirements: ['Defeat 100 enemies', 'Complete revenge quest'],
      bonuses: ['Damage vs enemies +60%', 'Execute ability', 'Strength +20'],
      conflicts: ['noble', 'protective', 'wise'],
    },
  ],
  protective: [
    {
      stage: 1,
      name: 'Guardian Spirit',
      requirements: ['Protect 15 allies', 'Block 50 attacks'],
      bonuses: ['Defense +25%', 'Shield allies'],
      conflicts: ['vengeful', 'chaotic', 'corrupted'],
    },
    {
      stage: 2,
      name: 'Eternal Sentinel',
      requirements: ['Save 50 lives', 'Never let ally die'],
      bonuses: ['Defense +50%', 'Invulnerability aura', 'Constitution +15'],
      conflicts: ['vengeful', 'chaotic', 'corrupted'],
    },
  ],
  ambitious: [
    {
      stage: 1,
      name: 'Rising Power',
      requirements: ['Gain 10 levels', 'Acquire 5 rare items'],
      bonuses: ['All stats +10%', 'Experience gain +20%'],
      conflicts: ['wise', 'protective'],
    },
    {
      stage: 2,
      name: 'Ascendant Force',
      requirements: ['Reach max level', 'Defeat legendary boss'],
      bonuses: ['All stats +25%', 'Power overwhelming', 'Leadership +10'],
      conflicts: ['wise', 'protective'],
    },
  ],
  mysterious: [
    {
      stage: 1,
      name: 'Enigmatic Presence',
      requirements: ['Discover 10 secrets', 'Complete hidden quests'],
      bonuses: ['Stealth +30%', 'Illusion magic'],
      conflicts: ['noble', 'playful'],
    },
    {
      stage: 2,
      name: 'Shadow Walker',
      requirements: ['Uncover 25 secrets', 'Master stealth'],
      bonuses: ['Stealth +60%', 'Phase shift', 'Perception +15'],
      conflicts: ['noble', 'playful'],
    },
  ],
  playful: [
    {
      stage: 1,
      name: 'Trickster Awakening',
      requirements: ['Play 20 pranks', 'Confuse 15 enemies'],
      bonuses: ['Luck +20%', 'Confusion spells'],
      conflicts: ['wise', 'vengeful', 'ancient'],
    },
    {
      stage: 2,
      name: 'Master of Mischief',
      requirements: ['Execute 50 tricks', 'Win through deception'],
      bonuses: ['Luck +40%', 'Reality bending', 'Charisma +10'],
      conflicts: ['wise', 'vengeful', 'ancient'],
    },
  ],
  ancient: [
    {
      stage: 1,
      name: 'Timeless Awakening',
      requirements: ['Exist for 100 years', 'Witness 10 eras'],
      bonuses: ['All resistances +15%', 'Ancient knowledge'],
      conflicts: ['playful', 'ambitious'],
    },
    {
      stage: 2,
      name: 'Eternal Witness',
      requirements: ['Exist for 1000 years', 'Master time magic'],
      bonuses: ['All resistances +35%', 'Time manipulation', 'Wisdom +20'],
      conflicts: ['playful', 'ambitious'],
    },
  ],
  corrupted: [
    {
      stage: 1,
      name: 'Tainted Essence',
      requirements: ['Commit 10 dark acts', 'Embrace corruption'],
      bonuses: ['Dark damage +30%', 'Life drain'],
      conflicts: ['noble', 'protective', 'wise'],
    },
    {
      stage: 2,
      name: 'Void Incarnate',
      requirements: ['Fully embrace darkness', 'Corrupt 5 souls'],
      bonuses: ['Dark damage +65%', 'Soul harvest', 'Corruption aura'],
      conflicts: ['noble', 'protective', 'wise'],
    },
  ],
};

/**
 * Faction Alignments
 */
export type FactionAlignment = 
  | 'lawful_good' | 'neutral_good' | 'chaotic_good'
  | 'lawful_neutral' | 'true_neutral' | 'chaotic_neutral'
  | 'lawful_evil' | 'neutral_evil' | 'chaotic_evil';

/**
 * Faction Ideologies
 */
export type FactionIdeology = 
  | 'militaristic' | 'mercantile' | 'religious' | 'scholarly' 
  | 'nature' | 'technological' | 'mystical' | 'anarchist';

/**
 * Faction Relationship Stances
 */
export type RelationshipStance = 
  | 'allied' | 'friendly' | 'neutral' | 'unfriendly' 
  | 'hostile' | 'at_war' | 'trade_partner' | 'vassal';

/**
 * Faction Definition
 */
export interface Faction {
  id: string;
  name: string;
  alignment: FactionAlignment;
  ideology: FactionIdeology;
  territoryBonuses: {
    attackBonus: number;
    defenseBonus: number;
    resourceBonus: number;
  };
  relationships: Record<string, RelationshipStance>;
  preferredWeapons: WeaponCategory[];
  preferredAttacks: AttackType[];
}

/**
 * Faction Database
 */
export const FACTIONS: Record<string, Faction> = {
  knights_of_valor: {
    id: 'knights_of_valor',
    name: 'Knights of Valor',
    alignment: 'lawful_good',
    ideology: 'militaristic',
    territoryBonuses: {
      attackBonus: 15,
      defenseBonus: 25,
      resourceBonus: 10,
    },
    relationships: {
      shadow_syndicate: 'hostile',
      merchant_guild: 'trade_partner',
      arcane_circle: 'friendly',
    },
    preferredWeapons: ['sword', 'shield', 'mace'],
    preferredAttacks: ['slash', 'holy', 'physical'],
  },
  shadow_syndicate: {
    id: 'shadow_syndicate',
    name: 'Shadow Syndicate',
    alignment: 'chaotic_neutral',
    ideology: 'anarchist',
    territoryBonuses: {
      attackBonus: 30,
      defenseBonus: 10,
      resourceBonus: 20,
    },
    relationships: {
      knights_of_valor: 'hostile',
      merchant_guild: 'unfriendly',
      arcane_circle: 'neutral',
    },
    preferredWeapons: ['dagger', 'bow', 'crossbow'],
    preferredAttacks: ['pierce', 'poison', 'ranged'],
  },
  merchant_guild: {
    id: 'merchant_guild',
    name: 'Merchant Guild',
    alignment: 'true_neutral',
    ideology: 'mercantile',
    territoryBonuses: {
      attackBonus: 5,
      defenseBonus: 15,
      resourceBonus: 40,
    },
    relationships: {
      knights_of_valor: 'trade_partner',
      shadow_syndicate: 'unfriendly',
      arcane_circle: 'trade_partner',
    },
    preferredWeapons: ['staff', 'dagger', 'crossbow'],
    preferredAttacks: ['blunt', 'ranged', 'physical'],
  },
  arcane_circle: {
    id: 'arcane_circle',
    name: 'Arcane Circle',
    alignment: 'neutral_good',
    ideology: 'mystical',
    territoryBonuses: {
      attackBonus: 35,
      defenseBonus: 20,
      resourceBonus: 15,
    },
    relationships: {
      knights_of_valor: 'friendly',
      shadow_syndicate: 'neutral',
      merchant_guild: 'trade_partner',
    },
    preferredWeapons: ['staff', 'wand'],
    preferredAttacks: ['magic', 'elemental', 'dark'],
  },
  nature_wardens: {
    id: 'nature_wardens',
    name: 'Nature Wardens',
    alignment: 'neutral_good',
    ideology: 'nature',
    territoryBonuses: {
      attackBonus: 20,
      defenseBonus: 30,
      resourceBonus: 25,
    },
    relationships: {
      knights_of_valor: 'friendly',
      shadow_syndicate: 'unfriendly',
      merchant_guild: 'neutral',
    },
    preferredWeapons: ['bow', 'spear', 'staff'],
    preferredAttacks: ['ranged', 'pierce', 'elemental'],
  },
};

/**
 * Player Archetypes
 */
export type PlayerArchetype = 
  | 'warrior' | 'mage' | 'rogue' | 'ranger' | 'paladin' 
  | 'necromancer' | 'bard' | 'monk' | 'druid' | 'warlock';

/**
 * Playstyle Preferences
 */
export type PlaystylePreference = 
  | 'aggressive' | 'defensive' | 'balanced' | 'stealth' 
  | 'support' | 'tank' | 'dps' | 'control' | 'hybrid';

/**
 * Archetype Definition
 */
export interface ArchetypeDefinition {
  id: PlayerArchetype;
  name: string;
  primaryStats: string[];
  preferredWeapons: WeaponCategory[];
  preferredAttacks: AttackType[];
  playstyles: PlaystylePreference[];
  compatibleFactions: string[];
  incompatibleFactions: string[];
}

/**
 * Player Archetype Database
 */
export const PLAYER_ARCHETYPES: Record<PlayerArchetype, ArchetypeDefinition> = {
  warrior: {
    id: 'warrior',
    name: 'Warrior',
    primaryStats: ['Strength', 'Constitution', 'Defense'],
    preferredWeapons: ['sword', 'axe', 'hammer', 'shield'],
    preferredAttacks: ['slash', 'blunt', 'physical'],
    playstyles: ['aggressive', 'tank', 'dps'],
    compatibleFactions: ['knights_of_valor', 'merchant_guild'],
    incompatibleFactions: ['shadow_syndicate'],
  },
  mage: {
    id: 'mage',
    name: 'Mage',
    primaryStats: ['Intelligence', 'Wisdom', 'Mana'],
    preferredWeapons: ['staff', 'wand'],
    preferredAttacks: ['magic', 'elemental', 'dark'],
    playstyles: ['control', 'dps', 'balanced'],
    compatibleFactions: ['arcane_circle', 'merchant_guild'],
    incompatibleFactions: ['nature_wardens'],
  },
  rogue: {
    id: 'rogue',
    name: 'Rogue',
    primaryStats: ['Agility', 'Dexterity', 'Luck'],
    preferredWeapons: ['dagger', 'bow', 'crossbow'],
    preferredAttacks: ['pierce', 'poison', 'ranged'],
    playstyles: ['stealth', 'dps', 'aggressive'],
    compatibleFactions: ['shadow_syndicate', 'merchant_guild'],
    incompatibleFactions: ['knights_of_valor'],
  },
  ranger: {
    id: 'ranger',
    name: 'Ranger',
    primaryStats: ['Dexterity', 'Perception', 'Agility'],
    preferredWeapons: ['bow', 'crossbow', 'spear'],
    preferredAttacks: ['ranged', 'pierce', 'physical'],
    playstyles: ['balanced', 'dps', 'support'],
    compatibleFactions: ['nature_wardens', 'merchant_guild'],
    incompatibleFactions: [],
  },
  paladin: {
    id: 'paladin',
    name: 'Paladin',
    primaryStats: ['Strength', 'Charisma', 'Constitution'],
    preferredWeapons: ['sword', 'mace', 'shield'],
    preferredAttacks: ['slash', 'holy', 'physical'],
    playstyles: ['tank', 'support', 'balanced'],
    compatibleFactions: ['knights_of_valor'],
    incompatibleFactions: ['shadow_syndicate', 'arcane_circle'],
  },
  necromancer: {
    id: 'necromancer',
    name: 'Necromancer',
    primaryStats: ['Intelligence', 'Charisma', 'Mana'],
    preferredWeapons: ['staff', 'wand'],
    preferredAttacks: ['dark', 'magic', 'poison'],
    playstyles: ['control', 'dps', 'support'],
    compatibleFactions: ['shadow_syndicate'],
    incompatibleFactions: ['knights_of_valor', 'nature_wardens'],
  },
  bard: {
    id: 'bard',
    name: 'Bard',
    primaryStats: ['Charisma', 'Dexterity', 'Luck'],
    preferredWeapons: ['dagger', 'bow'],
    preferredAttacks: ['slash', 'magic', 'ranged'],
    playstyles: ['support', 'hybrid', 'balanced'],
    compatibleFactions: ['merchant_guild', 'arcane_circle'],
    incompatibleFactions: [],
  },
  monk: {
    id: 'monk',
    name: 'Monk',
    primaryStats: ['Agility', 'Wisdom', 'Constitution'],
    preferredWeapons: ['staff', 'spear'],
    preferredAttacks: ['blunt', 'physical', 'holy'],
    playstyles: ['balanced', 'dps', 'tank'],
    compatibleFactions: ['nature_wardens', 'knights_of_valor'],
    incompatibleFactions: ['shadow_syndicate'],
  },
  druid: {
    id: 'druid',
    name: 'Druid',
    primaryStats: ['Wisdom', 'Constitution', 'Mana'],
    preferredWeapons: ['staff', 'spear'],
    preferredAttacks: ['elemental', 'magic', 'physical'],
    playstyles: ['support', 'hybrid', 'control'],
    compatibleFactions: ['nature_wardens'],
    incompatibleFactions: ['shadow_syndicate', 'arcane_circle'],
  },
  warlock: {
    id: 'warlock',
    name: 'Warlock',
    primaryStats: ['Intelligence', 'Charisma', 'Mana'],
    preferredWeapons: ['wand', 'staff'],
    preferredAttacks: ['dark', 'magic', 'elemental'],
    playstyles: ['dps', 'control', 'aggressive'],
    compatibleFactions: ['arcane_circle', 'shadow_syndicate'],
    incompatibleFactions: ['knights_of_valor', 'nature_wardens'],
  },
};

/**
 * RPG Dataset Service
 */
export class RPGDatasetService {
  /**
   * Check if weapon-attack combination is valid
   */
  static isWeaponAttackCompatible(weapon: WeaponCategory, attack: AttackType): boolean {
    const compatibleAttacks = WEAPON_ATTACK_COMPATIBILITY[weapon];
    return compatibleAttacks.includes(attack);
  }

  /**
   * Get valid attack types for a weapon
   */
  static getValidAttacksForWeapon(weapon: WeaponCategory): AttackType[] {
    return WEAPON_ATTACK_COMPATIBILITY[weapon] || [];
  }

  /**
   * Get sentient evolution path for personality
   */
  static getSentientEvolutionPath(personality: SentientPersonality): SentientEvolutionPath[] {
    return SENTIENT_EVOLUTION_PATHS[personality] || [];
  }

  /**
   * Check if two sentient personalities conflict
   */
  static doPersonalitiesConflict(personality1: SentientPersonality, personality2: SentientPersonality): boolean {
    const paths = SENTIENT_EVOLUTION_PATHS[personality1];
    if (!paths || paths.length === 0) return false;
    
    return paths.some(path => path.conflicts.includes(personality2));
  }

  /**
   * Get faction by ID
   */
  static getFaction(factionId: string): Faction | null {
    return FACTIONS[factionId] || null;
  }

  /**
   * Get faction influence bonuses
   */
  static getFactionInfluence(factionId: string): {
    attackBonus: number;
    defenseBonus: number;
    resourceBonus: number;
  } | null {
    const faction = FACTIONS[factionId];
    return faction ? faction.territoryBonuses : null;
  }

  /**
   * Get faction relationship stance
   */
  static getFactionRelationship(faction1Id: string, faction2Id: string): RelationshipStance | null {
    const faction = FACTIONS[faction1Id];
    if (!faction) return null;
    
    return faction.relationships[faction2Id] || 'neutral';
  }

  /**
   * Get archetype definition
   */
  static getArchetype(archetypeId: PlayerArchetype): ArchetypeDefinition | null {
    return PLAYER_ARCHETYPES[archetypeId] || null;
  }

  /**
   * Check if archetype is compatible with faction
   */
  static isArchetypeFactionCompatible(archetypeId: PlayerArchetype, factionId: string): boolean {
    const archetype = PLAYER_ARCHETYPES[archetypeId];
    if (!archetype) return false;
    
    if (archetype.incompatibleFactions.includes(factionId)) return false;
    if (archetype.compatibleFactions.length === 0) return true;
    
    return archetype.compatibleFactions.includes(factionId);
  }

  /**
   * Get recommended weapons for archetype
   */
  static getArchetypeWeapons(archetypeId: PlayerArchetype): WeaponCategory[] {
    const archetype = PLAYER_ARCHETYPES[archetypeId];
    return archetype ? archetype.preferredWeapons : [];
  }

  /**
   * Get recommended attacks for archetype
   */
  static getArchetypeAttacks(archetypeId: PlayerArchetype): AttackType[] {
    const archetype = PLAYER_ARCHETYPES[archetypeId];
    return archetype ? archetype.preferredAttacks : [];
  }

  /**
   * Get all factions
   */
  static getAllFactions(): Faction[] {
    return Object.values(FACTIONS);
  }

  /**
   * Get all archetypes
   */
  static getAllArchetypes(): ArchetypeDefinition[] {
    return Object.values(PLAYER_ARCHETYPES);
  }

  /**
   * Get all weapon categories
   */
  static getAllWeaponCategories(): WeaponCategory[] {
    return Object.keys(WEAPON_ATTACK_COMPATIBILITY) as WeaponCategory[];
  }

  /**
   * Get all sentient personalities
   */
  static getAllSentientPersonalities(): SentientPersonality[] {
    return Object.keys(SENTIENT_EVOLUTION_PATHS) as SentientPersonality[];
  }

  /**
   * Log dataset initialization
   */
  static initialize(): void {
    debugLogger.info('rpg-dataset', 'RPG Dataset initialized', {
      factions: Object.keys(FACTIONS).length,
      archetypes: Object.keys(PLAYER_ARCHETYPES).length,
      weaponCategories: Object.keys(WEAPON_ATTACK_COMPATIBILITY).length,
      sentientPersonalities: Object.keys(SENTIENT_EVOLUTION_PATHS).length,
    });
  }
}

// Initialize on module load
RPGDatasetService.initialize();

