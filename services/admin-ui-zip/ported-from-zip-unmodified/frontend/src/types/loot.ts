/**
 * Loot-specific types for the ECS loot system.
 * Kept separate to avoid circular imports with runtime.ts.
 */

export type LootTierName = "common" | "rare" | "epic" | "legendary";

/** Weight map for loot tier selection (values are relative weights). */
export interface LootTierWeights {
  common: number;
  rare: number;
  epic: number;
  legendary: number;
}

/** Serializable loot item data stored inside a LootItem component. */
export interface LootItemData {
  id: string;
  name: string;
  tier: LootTierName;
  isExcellent: boolean;
  attributes: Array<{
    category: string;
    name: string;
    value: number | string;
    description: string;
  }>;
}

/** Entry in a player's inventory (extends LootItemData for display). */
export type LootInventoryItem = LootItemData;

/** Mob-type keyed tier weight config. */
export const MOB_LOOT_TIER_WEIGHTS: Record<string, LootTierWeights> = {
  goblin: { common: 70, rare: 22, epic: 7, legendary: 1 },
  skeleton: { common: 65, rare: 25, epic: 8, legendary: 2 },
  orc: { common: 55, rare: 30, epic: 12, legendary: 3 },
  troll: { common: 45, rare: 30, epic: 18, legendary: 7 },
  wraith: { common: 35, rare: 30, epic: 22, legendary: 13 },
};

/** Default tier weights for unknown mob types. */
export const DEFAULT_TIER_WEIGHTS: LootTierWeights = {
  common: 60,
  rare: 25,
  epic: 12,
  legendary: 3,
};

/** Returns a tier sampled from the given weights. */
export function sampleTier(weights: LootTierWeights): LootTierName {
  const total =
    weights.common + weights.rare + weights.epic + weights.legendary;
  const roll = Math.random() * total;
  let acc = 0;
  acc += weights.common;
  if (roll < acc) return "common";
  acc += weights.rare;
  if (roll < acc) return "rare";
  acc += weights.epic;
  if (roll < acc) return "epic";
  return "legendary";
}
