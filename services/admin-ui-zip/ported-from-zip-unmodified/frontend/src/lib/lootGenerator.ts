import { debugLogger } from './debugLogger';
import { 
  RPGDatasetService, 
  type WeaponCategory, 
  type AttackType,
  type SentientPersonality,
  type PlayerArchetype,
  type FactionAlignment
} from './rpgDataset';

/**
 * Loot Attribute Categories
 */
export type AttributeCategory = 'attack' | 'defense' | 'elemental' | 'special' | 'abilities';

/**
 * Loot Tier Levels
 */
export type LootTier = 'common' | 'rare' | 'epic' | 'legendary';

/**
 * Attribute Definition
 */
export interface LootAttribute {
  category: AttributeCategory;
  name: string;
  value: number | string;
  description: string;
}

/**
 * RPG Dataset Context for Generated Loot
 */
export interface RPGDatasetContext {
  weaponCategory?: WeaponCategory;
  attackTypes?: AttackType[];
  sentientPersonality?: SentientPersonality;
  factionAlignment?: string;
  archetypeCompatibility?: PlayerArchetype[];
  weaponAttackCompatible: boolean;
}

/**
 * Generated Loot Item
 */
export interface GeneratedLoot {
  id: string;
  name: string;
  tier: LootTier;
  isExcellent: boolean;
  attributes: LootAttribute[];
  timestamp: number;
  rpgContext?: RPGDatasetContext;
}

/**
 * Loot Generation Request
 */
export interface LootGenerationRequest {
  id: string;
  tier: LootTier;
  itemType: string;
  seed?: number;
  requestExcellent?: boolean;
  timestamp: number;
  status: 'pending' | 'processing' | 'completed' | 'failed';
}

/**
 * Name Uniqueness Statistics
 */
export interface NameUniquenessStats {
  totalGenerated: number;
  uniqueNames: number;
  duplicatesFound: number;
  duplicatesResolved: number;
  fallbackPatternsUsed: Record<string, number>;
}

/**
 * World Loot Table Statistics
 */
export interface WorldLootTableStats {
  totalItems: number;
  commonCount: number;
  rareCount: number;
  epicCount: number;
  legendaryCount: number;
  excellentCount: number;
  weaponCategoryDistribution: Record<string, number>;
  factionAlignmentDistribution: Record<string, number>;
  archetypeCompatibilityCount: number;
  nameUniqueness: NameUniquenessStats;
}

/**
 * Attribute Pool Definitions
 */
const ATTRIBUTE_POOLS: Record<AttributeCategory, Array<{ name: string; valueRange: [number, number]; description: string }>> = {
  attack: [
    { name: 'Physical Damage', valueRange: [10, 100], description: 'Increases physical attack damage' },
    { name: 'Critical Strike Chance', valueRange: [5, 25], description: 'Chance to deal critical damage' },
    { name: 'Attack Speed', valueRange: [5, 30], description: 'Increases attack speed percentage' },
    { name: 'Armor Penetration', valueRange: [10, 40], description: 'Ignores enemy armor' },
    { name: 'Life Steal', valueRange: [3, 15], description: 'Heals based on damage dealt' },
    { name: 'Bleed Damage', valueRange: [8, 35], description: 'Causes bleeding damage over time' },
    { name: 'Execute Damage', valueRange: [15, 50], description: 'Bonus damage to low health enemies' },
  ],
  defense: [
    { name: 'Physical Armor', valueRange: [20, 150], description: 'Reduces physical damage taken' },
    { name: 'Magic Resistance', valueRange: [15, 100], description: 'Reduces magical damage taken' },
    { name: 'Health Regeneration', valueRange: [5, 50], description: 'Regenerates health per second' },
    { name: 'Block Chance', valueRange: [5, 20], description: 'Chance to block incoming attacks' },
    { name: 'Damage Reduction', valueRange: [5, 25], description: 'Reduces all damage taken by percentage' },
    { name: 'Shield Strength', valueRange: [30, 120], description: 'Increases shield capacity' },
    { name: 'Evasion', valueRange: [5, 20], description: 'Chance to evade attacks completely' },
  ],
  elemental: [
    { name: 'Fire Damage', valueRange: [15, 80], description: 'Adds fire damage to attacks' },
    { name: 'Ice Damage', valueRange: [15, 80], description: 'Adds ice damage and slows enemies' },
    { name: 'Lightning Damage', valueRange: [15, 80], description: 'Adds lightning damage with chain effect' },
    { name: 'Poison Damage', valueRange: [10, 60], description: 'Applies poison damage over time' },
    { name: 'Elemental Resistance', valueRange: [10, 40], description: 'Resists all elemental damage' },
    { name: 'Chaos Damage', valueRange: [20, 90], description: 'Unpredictable chaos damage' },
    { name: 'Holy Damage', valueRange: [18, 75], description: 'Divine damage effective against undead' },
  ],
  special: [
    { name: 'Movement Speed', valueRange: [10, 40], description: 'Increases movement speed' },
    { name: 'Experience Gain', valueRange: [10, 50], description: 'Increases experience gained' },
    { name: 'Gold Find', valueRange: [20, 100], description: 'Increases gold dropped by enemies' },
    { name: 'Magic Find', valueRange: [15, 75], description: 'Increases chance of rare item drops' },
    { name: 'Durability', valueRange: [50, 200], description: 'Increases item durability' },
    { name: 'Luck', valueRange: [10, 50], description: 'Increases critical and rare drop chances' },
    { name: 'Thorns', valueRange: [15, 60], description: 'Reflects damage back to attackers' },
  ],
  abilities: [
    { name: 'Cooldown Reduction', valueRange: [10, 30], description: 'Reduces ability cooldowns globally' },
    { name: 'Mana Efficiency', valueRange: [15, 40], description: 'Reduces mana cost of abilities' },
    { name: 'Ally Summoning', valueRange: [1, 3], description: 'Can summon allies to fight' },
    { name: 'Area Effect Radius', valueRange: [20, 60], description: 'Increases area of effect abilities' },
    { name: 'Lifesteal Aura', valueRange: [5, 15], description: 'Grants lifesteal to nearby allies' },
    { name: 'Spell Power', valueRange: [20, 80], description: 'Increases magical ability damage' },
    { name: 'Ultimate Charge', valueRange: [10, 30], description: 'Faster ultimate ability charging' },
  ],
};

/**
 * Tier Configuration
 */
const TIER_CONFIG: Record<LootTier, { attributeCount: number; multiplier: number; excellentChance: number }> = {
  common: { attributeCount: 1, multiplier: 1.0, excellentChance: 0.05 },
  rare: { attributeCount: 2, multiplier: 1.5, excellentChance: 0.10 },
  epic: { attributeCount: 3, multiplier: 2.0, excellentChance: 0.15 },
  legendary: { attributeCount: 4, multiplier: 3.0, excellentChance: 0.25 },
};

/**
 * Excellent Version Special Properties
 */
const EXCELLENT_PROPERTIES = [
  { name: 'Double Abilities', description: 'All ability attributes are doubled' },
  { name: 'Stackable Buffs', description: 'Buffs can stack multiple times' },
  { name: 'Damage Resistance', description: 'Additional 10% damage resistance' },
  { name: 'Elemental Interaction', description: 'Elemental damage triggers special effects' },
  { name: 'Rare Special Property', description: 'Unique effect based on item type' },
  { name: 'World Breaking Power', description: 'Grants game-changing abilities' },
];

/**
 * Name Uniqueness Tracker
 */
class NameUniquenessTracker {
  private usedNames: Set<string> = new Set();
  private duplicatesFound: number = 0;
  private duplicatesResolved: number = 0;
  private fallbackPatternsUsed: Record<string, number> = {};

  /**
   * Check if name is unique and register it
   */
  registerName(name: string): boolean {
    if (this.usedNames.has(name)) {
      this.duplicatesFound++;
      return false;
    }
    this.usedNames.add(name);
    return true;
  }

  /**
   * Generate unique name with fallback patterns
   */
  ensureUniqueName(baseName: string, rpgContext?: RPGDatasetContext, tier?: LootTier, isExcellent?: boolean): string {
    let uniqueName = baseName;
    let attempt = 0;

    // Try base name first
    if (this.registerName(uniqueName)) {
      return uniqueName;
    }

    // Fallback pattern 1: Add weapon category
    if (rpgContext?.weaponCategory && attempt < 5) {
      uniqueName = `${baseName} [${rpgContext.weaponCategory}]`;
      if (this.registerName(uniqueName)) {
        this.duplicatesResolved++;
        this.trackFallbackPattern('weapon-category');
        return uniqueName;
      }
      attempt++;
    }

    // Fallback pattern 2: Add faction alignment
    if (rpgContext?.factionAlignment && attempt < 5) {
      const factionName = rpgContext.factionAlignment.replace(/_/g, ' ');
      uniqueName = `${baseName} of ${factionName}`;
      if (this.registerName(uniqueName)) {
        this.duplicatesResolved++;
        this.trackFallbackPattern('faction-alignment');
        return uniqueName;
      }
      attempt++;
    }

    // Fallback pattern 3: Add tier designation
    if (tier && attempt < 5) {
      uniqueName = `${baseName} (${tier.toUpperCase()})`;
      if (this.registerName(uniqueName)) {
        this.duplicatesResolved++;
        this.trackFallbackPattern('tier-designation');
        return uniqueName;
      }
      attempt++;
    }

    // Fallback pattern 4: Add excellent marker
    if (isExcellent && attempt < 5) {
      uniqueName = `${baseName} ★`;
      if (this.registerName(uniqueName)) {
        this.duplicatesResolved++;
        this.trackFallbackPattern('excellent-marker');
        return uniqueName;
      }
      attempt++;
    }

    // Fallback pattern 5: Add attack type
    if (rpgContext?.attackTypes && rpgContext.attackTypes.length > 0 && attempt < 5) {
      uniqueName = `${baseName} (${rpgContext.attackTypes[0]})`;
      if (this.registerName(uniqueName)) {
        this.duplicatesResolved++;
        this.trackFallbackPattern('attack-type');
        return uniqueName;
      }
      attempt++;
    }

    // Fallback pattern 6: Add numeric suffix
    let counter = 1;
    while (counter < 10000) {
      uniqueName = `${baseName} #${counter}`;
      if (this.registerName(uniqueName)) {
        this.duplicatesResolved++;
        this.trackFallbackPattern('numeric-suffix');
        return uniqueName;
      }
      counter++;
    }

    // Last resort: timestamp
    uniqueName = `${baseName} [${Date.now()}]`;
    this.registerName(uniqueName);
    this.duplicatesResolved++;
    this.trackFallbackPattern('timestamp');
    return uniqueName;
  }

  /**
   * Track fallback pattern usage
   */
  private trackFallbackPattern(pattern: string): void {
    this.fallbackPatternsUsed[pattern] = (this.fallbackPatternsUsed[pattern] || 0) + 1;
  }

  /**
   * Get uniqueness statistics
   */
  getStats(): NameUniquenessStats {
    return {
      totalGenerated: this.usedNames.size + this.duplicatesFound,
      uniqueNames: this.usedNames.size,
      duplicatesFound: this.duplicatesFound,
      duplicatesResolved: this.duplicatesResolved,
      fallbackPatternsUsed: { ...this.fallbackPatternsUsed },
    };
  }

  /**
   * Clear tracker
   */
  clear(): void {
    this.usedNames.clear();
    this.duplicatesFound = 0;
    this.duplicatesResolved = 0;
    this.fallbackPatternsUsed = {};
  }
}

// Global name tracker instance
const globalNameTracker = new NameUniquenessTracker();

/**
 * Deterministic RNG using mulberry32
 */
function mulberry32(seed: number): () => number {
  return function() {
    let t = seed += 0x6D2B79F5;
    t = Math.imul(t ^ t >>> 15, t | 1);
    t ^= t + Math.imul(t ^ t >>> 7, t | 61);
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

/**
 * Generate RPG Dataset Context for loot item
 */
function generateRPGContext(rng: () => number): RPGDatasetContext {
  // Select random weapon category
  const weaponCategories = RPGDatasetService.getAllWeaponCategories();
  const weaponCategory = weaponCategories[Math.floor(rng() * weaponCategories.length)];
  
  // Get valid attack types for weapon
  const validAttacks = RPGDatasetService.getValidAttacksForWeapon(weaponCategory);
  const attackTypes = validAttacks.slice(0, Math.floor(rng() * validAttacks.length) + 1);
  
  // Select random sentient personality (20% chance)
  let sentientPersonality: SentientPersonality | undefined;
  if (rng() < 0.2) {
    const personalities = RPGDatasetService.getAllSentientPersonalities();
    sentientPersonality = personalities[Math.floor(rng() * personalities.length)];
  }
  
  // Select random faction alignment
  const factions = RPGDatasetService.getAllFactions();
  const faction = factions[Math.floor(rng() * factions.length)];
  const factionAlignment = faction.id;
  
  // Determine archetype compatibility
  const archetypes = RPGDatasetService.getAllArchetypes();
  const archetypeCompatibility = archetypes
    .filter(archetype => RPGDatasetService.isArchetypeFactionCompatible(archetype.id, factionAlignment))
    .map(archetype => archetype.id);
  
  // Verify weapon-attack compatibility
  const weaponAttackCompatible = attackTypes.every(attack => 
    RPGDatasetService.isWeaponAttackCompatible(weaponCategory, attack)
  );
  
  return {
    weaponCategory,
    attackTypes,
    sentientPersonality,
    factionAlignment,
    archetypeCompatibility,
    weaponAttackCompatible,
  };
}

/**
 * Generate item name based on tier, excellent status, and RPG context
 */
function generateItemName(tier: LootTier, isExcellent: boolean, rpgContext: RPGDatasetContext, rng: () => number): string {
  const prefixes = {
    common: ['Simple', 'Basic', 'Standard', 'Plain', 'Crude', 'Worn'],
    rare: ['Enhanced', 'Superior', 'Fine', 'Quality', 'Refined', 'Polished'],
    epic: ['Masterwork', 'Exquisite', 'Grand', 'Magnificent', 'Pristine', 'Flawless'],
    legendary: ['Mythical', 'Divine', 'Celestial', 'Ancient', 'Eternal', 'Transcendent'],
  };
  
  const weaponNames: Record<WeaponCategory, string> = {
    sword: 'Sword',
    axe: 'Axe',
    bow: 'Bow',
    staff: 'Staff',
    dagger: 'Dagger',
    mace: 'Mace',
    spear: 'Spear',
    wand: 'Wand',
    crossbow: 'Crossbow',
    hammer: 'Hammer',
    shield: 'Shield',
  };
  
  const prefix = prefixes[tier][Math.floor(rng() * prefixes[tier].length)];
  const weaponType = rpgContext.weaponCategory ? weaponNames[rpgContext.weaponCategory] : 'Item';
  
  let name = isExcellent ? `Excellent ${prefix} ${weaponType}` : `${prefix} ${weaponType}`;
  
  // Add faction prefix if applicable (30% chance)
  if (rpgContext.factionAlignment && rng() < 0.3) {
    const faction = RPGDatasetService.getFaction(rpgContext.factionAlignment);
    if (faction) {
      name = `${faction.name} ${name}`;
    }
  }
  
  return name;
}

/**
 * Generate loot attributes based on tier with RPG Dataset integration and name uniqueness validation
 */
export function generateLootAttributes(
  tier: LootTier,
  seed: number = Date.now(),
  requestExcellent: boolean = false,
  nameTracker?: NameUniquenessTracker
): GeneratedLoot {
  const rng = mulberry32(seed);
  const config = TIER_CONFIG[tier];
  
  // Generate RPG Dataset context
  const rpgContext = generateRPGContext(rng);
  
  // Determine if excellent
  const isExcellent = requestExcellent || rng() < config.excellentChance;
  
  // Select attribute categories (one per tier level, no duplicates)
  const categories: AttributeCategory[] = ['attack', 'defense', 'elemental', 'special', 'abilities'];
  const selectedCategories: AttributeCategory[] = [];
  
  for (let i = 0; i < config.attributeCount; i++) {
    const availableCategories = categories.filter(c => !selectedCategories.includes(c));
    if (availableCategories.length === 0) break;
    
    const categoryIndex = Math.floor(rng() * availableCategories.length);
    selectedCategories.push(availableCategories[categoryIndex]);
  }
  
  // Generate attributes
  const attributes: LootAttribute[] = [];
  
  selectedCategories.forEach(category => {
    const pool = ATTRIBUTE_POOLS[category];
    const attrIndex = Math.floor(rng() * pool.length);
    const attrDef = pool[attrIndex];
    
    // Calculate value with tier multiplier
    const baseValue = attrDef.valueRange[0] + 
      (attrDef.valueRange[1] - attrDef.valueRange[0]) * rng();
    let value = Math.round(baseValue * config.multiplier);
    
    // Apply faction bonuses if applicable
    if (rpgContext.factionAlignment) {
      const factionInfluence = RPGDatasetService.getFactionInfluence(rpgContext.factionAlignment);
      if (factionInfluence) {
        if (category === 'attack') {
          value = Math.round(value * (1 + factionInfluence.attackBonus / 100));
        } else if (category === 'defense') {
          value = Math.round(value * (1 + factionInfluence.defenseBonus / 100));
        }
      }
    }
    
    // Double abilities for excellent items
    if (isExcellent && category === 'abilities') {
      value *= 2;
    }
    
    attributes.push({
      category,
      name: attrDef.name,
      value,
      description: attrDef.description,
    });
  });
  
  // Add excellent properties
  if (isExcellent) {
    const excellentPropIndex = Math.floor(rng() * EXCELLENT_PROPERTIES.length);
    const excellentProp = EXCELLENT_PROPERTIES[excellentPropIndex];
    
    attributes.push({
      category: 'special',
      name: excellentProp.name,
      value: 'Active',
      description: excellentProp.description,
    });
  }
  
  // Add sentient personality trait if applicable
  if (rpgContext.sentientPersonality) {
    attributes.push({
      category: 'special',
      name: `Sentient: ${rpgContext.sentientPersonality}`,
      value: 'Awakening',
      description: `This item has a ${rpgContext.sentientPersonality} personality and can evolve`,
    });
  }
  
  // Generate base name
  const baseName = generateItemName(tier, isExcellent, rpgContext, rng);
  
  // Ensure name uniqueness
  const tracker = nameTracker || globalNameTracker;
  const uniqueName = tracker.ensureUniqueName(baseName, rpgContext, tier, isExcellent);
  
  return {
    id: `loot_${seed}_${Date.now()}`,
    name: uniqueName,
    tier,
    isExcellent,
    attributes,
    timestamp: Date.now(),
    rpgContext,
  };
}

/**
 * Generate massive world loot table with RPG Dataset integration and name uniqueness validation
 */
export async function generateMassiveLootTable(
  itemCount: number,
  onProgress?: (progress: number) => void
): Promise<{ items: GeneratedLoot[]; stats: WorldLootTableStats }> {
  debugLogger.info('world-loot', `Starting massive loot table generation with name uniqueness validation: ${itemCount} items`);
  
  // Create dedicated name tracker for this generation
  const nameTracker = new NameUniquenessTracker();
  
  const items: GeneratedLoot[] = [];
  const batchSize = 1000;
  const batches = Math.ceil(itemCount / batchSize);
  
  // Tier distribution for world generation
  const tierDistribution: LootTier[] = [
    ...Array(60).fill('common'),
    ...Array(25).fill('rare'),
    ...Array(12).fill('epic'),
    ...Array(3).fill('legendary'),
  ];
  
  for (let batch = 0; batch < batches; batch++) {
    const batchStart = batch * batchSize;
    const batchEnd = Math.min((batch + 1) * batchSize, itemCount);
    const batchItems: GeneratedLoot[] = [];
    
    for (let i = batchStart; i < batchEnd; i++) {
      const tier = tierDistribution[Math.floor(Math.random() * tierDistribution.length)];
      const seed = Date.now() + i;
      const item = generateLootAttributes(tier, seed, false, nameTracker);
      batchItems.push(item);
    }
    
    items.push(...batchItems);
    
    // Update progress
    const progress = ((batch + 1) / batches) * 100;
    if (onProgress) {
      onProgress(progress);
    }
    
    // Allow UI to update between batches
    await new Promise(resolve => setTimeout(resolve, 0));
  }
  
  // Get name uniqueness statistics
  const nameUniqueness = nameTracker.getStats();
  
  debugLogger.success('world-loot', `Name uniqueness validation complete`, {
    duplicatesFound: nameUniqueness.duplicatesFound,
    duplicatesResolved: nameUniqueness.duplicatesResolved,
    fallbackPatterns: nameUniqueness.fallbackPatternsUsed,
  });
  
  // Calculate statistics with RPG Dataset context
  const weaponCategoryDistribution: Record<string, number> = {};
  const factionAlignmentDistribution: Record<string, number> = {};
  let archetypeCompatibilityCount = 0;
  
  items.forEach(item => {
    if (item.rpgContext) {
      if (item.rpgContext.weaponCategory) {
        weaponCategoryDistribution[item.rpgContext.weaponCategory] = 
          (weaponCategoryDistribution[item.rpgContext.weaponCategory] || 0) + 1;
      }
      if (item.rpgContext.factionAlignment) {
        factionAlignmentDistribution[item.rpgContext.factionAlignment] = 
          (factionAlignmentDistribution[item.rpgContext.factionAlignment] || 0) + 1;
      }
      if (item.rpgContext.archetypeCompatibility && item.rpgContext.archetypeCompatibility.length > 0) {
        archetypeCompatibilityCount++;
      }
    }
  });
  
  const stats: WorldLootTableStats = {
    totalItems: items.length,
    commonCount: items.filter(i => i.tier === 'common').length,
    rareCount: items.filter(i => i.tier === 'rare').length,
    epicCount: items.filter(i => i.tier === 'epic').length,
    legendaryCount: items.filter(i => i.tier === 'legendary').length,
    excellentCount: items.filter(i => i.isExcellent).length,
    weaponCategoryDistribution,
    factionAlignmentDistribution,
    archetypeCompatibilityCount,
    nameUniqueness,
  };
  
  debugLogger.success('world-loot', `World loot table generated with RPG Dataset and name uniqueness: ${items.length} items`, { stats });
  
  return { items, stats };
}

/**
 * Export world loot table to YAML with RPG Dataset context
 */
export function exportWorldLootTableYAML(items: GeneratedLoot[]): string {
  let yaml = `# World Loot Table with RPG Dataset Integration and Name Uniqueness Validation\n`;
  yaml += `# Generated: ${new Date().toISOString()}\n`;
  yaml += `# Total Items: ${items.length}\n\n`;
  yaml += `worldLootTable:\n`;
  
  items.forEach((item, index) => {
    yaml += `  - id: "${item.id}"\n`;
    yaml += `    name: "${item.name}"\n`;
    yaml += `    tier: ${item.tier}\n`;
    yaml += `    isExcellent: ${item.isExcellent}\n`;
    yaml += `    timestamp: ${item.timestamp}\n`;
    
    if (item.rpgContext) {
      yaml += `    rpgContext:\n`;
      if (item.rpgContext.weaponCategory) {
        yaml += `      weaponCategory: ${item.rpgContext.weaponCategory}\n`;
      }
      if (item.rpgContext.attackTypes && item.rpgContext.attackTypes.length > 0) {
        yaml += `      attackTypes: [${item.rpgContext.attackTypes.join(', ')}]\n`;
      }
      if (item.rpgContext.sentientPersonality) {
        yaml += `      sentientPersonality: ${item.rpgContext.sentientPersonality}\n`;
      }
      if (item.rpgContext.factionAlignment) {
        yaml += `      factionAlignment: ${item.rpgContext.factionAlignment}\n`;
      }
      if (item.rpgContext.archetypeCompatibility && item.rpgContext.archetypeCompatibility.length > 0) {
        yaml += `      archetypeCompatibility: [${item.rpgContext.archetypeCompatibility.join(', ')}]\n`;
      }
      yaml += `      weaponAttackCompatible: ${item.rpgContext.weaponAttackCompatible}\n`;
    }
    
    yaml += `    attributes:\n`;
    item.attributes.forEach(attr => {
      yaml += `      - category: ${attr.category}\n`;
      yaml += `        name: "${attr.name}"\n`;
      yaml += `        value: ${attr.value}\n`;
      yaml += `        description: "${attr.description}"\n`;
    });
    
    if (index < items.length - 1) {
      yaml += `\n`;
    }
  });
  
  return yaml;
}

/**
 * Export world loot table to JSON with RPG Dataset context
 */
export function exportWorldLootTableJSON(items: GeneratedLoot[]): string {
  return JSON.stringify({
    metadata: {
      generated: new Date().toISOString(),
      totalItems: items.length,
      version: '2.0.0',
      rpgDatasetIntegrated: true,
      nameUniquenessValidated: true,
    },
    worldLootTable: items,
  }, null, 2);
}

/**
 * Convert loot to YAML format with RPG Dataset context
 */
export function lootToYAML(loot: GeneratedLoot): string {
  let yaml = `# ${loot.name}\n`;
  yaml += `id: "${loot.id}"\n`;
  yaml += `name: "${loot.name}"\n`;
  yaml += `tier: ${loot.tier}\n`;
  yaml += `isExcellent: ${loot.isExcellent}\n`;
  yaml += `timestamp: ${loot.timestamp}\n`;
  
  if (loot.rpgContext) {
    yaml += `rpgContext:\n`;
    if (loot.rpgContext.weaponCategory) {
      yaml += `  weaponCategory: ${loot.rpgContext.weaponCategory}\n`;
    }
    if (loot.rpgContext.attackTypes && loot.rpgContext.attackTypes.length > 0) {
      yaml += `  attackTypes: [${loot.rpgContext.attackTypes.join(', ')}]\n`;
    }
    if (loot.rpgContext.sentientPersonality) {
      yaml += `  sentientPersonality: ${loot.rpgContext.sentientPersonality}\n`;
    }
    if (loot.rpgContext.factionAlignment) {
      yaml += `  factionAlignment: ${loot.rpgContext.factionAlignment}\n`;
    }
    if (loot.rpgContext.archetypeCompatibility && loot.rpgContext.archetypeCompatibility.length > 0) {
      yaml += `  archetypeCompatibility: [${loot.rpgContext.archetypeCompatibility.join(', ')}]\n`;
    }
    yaml += `  weaponAttackCompatible: ${loot.rpgContext.weaponAttackCompatible}\n`;
  }
  
  yaml += `attributes:\n`;
  loot.attributes.forEach(attr => {
    yaml += `  - category: ${attr.category}\n`;
    yaml += `    name: "${attr.name}"\n`;
    yaml += `    value: ${attr.value}\n`;
    yaml += `    description: "${attr.description}"\n`;
  });
  
  return yaml;
}

/**
 * Convert loot to JSON format
 */
export function lootToJSON(loot: GeneratedLoot): string {
  return JSON.stringify(loot, null, 2);
}

/**
 * Loot Generation Queue Handler
 */
export class LootGenerationQueue {
  private queue: LootGenerationRequest[] = [];
  private processing: boolean = false;
  private completedItems: Map<string, GeneratedLoot> = new Map();
  
  constructor() {
    debugLogger.info('loot-queue', 'Loot generation queue initialized with RPG Dataset integration and name uniqueness validation');
  }
  
  /**
   * Add generation request to queue
   */
  enqueue(tier: LootTier, itemType: string, seed?: number, requestExcellent?: boolean): string {
    const request: LootGenerationRequest = {
      id: `req_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
      tier,
      itemType,
      seed: seed || Date.now(),
      requestExcellent: requestExcellent || false,
      timestamp: Date.now(),
      status: 'pending',
    };
    
    this.queue.push(request);
    
    debugLogger.info('loot-queue', `Request enqueued: ${request.id}`, {
      tier,
      itemType,
      queueSize: this.queue.length,
    });
    
    // Start processing if not already running
    if (!this.processing) {
      this.processQueue();
    }
    
    return request.id;
  }
  
  /**
   * Process queue asynchronously
   */
  private async processQueue(): Promise<void> {
    if (this.processing || this.queue.length === 0) {
      return;
    }
    
    this.processing = true;
    debugLogger.info('loot-queue', 'Starting queue processing with RPG Dataset integration and name uniqueness validation');
    
    while (this.queue.length > 0) {
      const request = this.queue.shift();
      if (!request) continue;
      
      request.status = 'processing';
      debugLogger.info('loot-queue', `Processing request: ${request.id}`);
      
      try {
        // Simulate async processing delay
        await new Promise(resolve => setTimeout(resolve, 100));
        
        const loot = generateLootAttributes(
          request.tier,
          request.seed,
          request.requestExcellent
        );
        
        this.completedItems.set(request.id, loot);
        request.status = 'completed';
        
        debugLogger.success('loot-queue', `Request completed: ${request.id}`, {
          lootName: loot.name,
          isExcellent: loot.isExcellent,
          rpgContext: loot.rpgContext,
        });
      } catch (error: any) {
        request.status = 'failed';
        debugLogger.error('loot-queue', `Request failed: ${request.id}`, {
          error: error.message,
        });
      }
    }
    
    this.processing = false;
    debugLogger.info('loot-queue', 'Queue processing completed');
  }
  
  /**
   * Get completed loot item
   */
  getCompletedItem(requestId: string): GeneratedLoot | null {
    return this.completedItems.get(requestId) || null;
  }
  
  /**
   * Get all completed items
   */
  getAllCompletedItems(): GeneratedLoot[] {
    return Array.from(this.completedItems.values());
  }
  
  /**
   * Get queue status
   */
  getQueueStatus(): {
    queueSize: number;
    processing: boolean;
    completedCount: number;
  } {
    return {
      queueSize: this.queue.length,
      processing: this.processing,
      completedCount: this.completedItems.size,
    };
  }
  
  /**
   * Clear completed items
   */
  clearCompleted(): void {
    const count = this.completedItems.size;
    this.completedItems.clear();
    debugLogger.info('loot-queue', `Cleared ${count} completed items`);
  }
  
  /**
   * Clear entire queue
   */
  clearQueue(): void {
    const count = this.queue.length;
    this.queue = [];
    debugLogger.info('loot-queue', `Cleared ${count} pending requests`);
  }
}

// Singleton instance
export const lootGenerationQueue = new LootGenerationQueue();
