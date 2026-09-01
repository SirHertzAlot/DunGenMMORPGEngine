// Mastery system types for object-based skill trees

// ── Mastery Tier enum ──────────────────────────────────────────────────────────

export const MasteryTier = {
  Apprentice: 1,
  Journeyman: 2,
  Craftsman: 3,
  Master: 4,
  Grandmaster: 5,
  Legendary: 6,
  God: 7,
} as const;

export type MasteryTier = (typeof MasteryTier)[keyof typeof MasteryTier];

/** XP required to advance FROM each tier to the next. God tier has no cap (Infinity). */
export const MASTERY_TIER_XP_THRESHOLDS: Record<MasteryTier, number> = {
  [MasteryTier.Apprentice]: 100,
  [MasteryTier.Journeyman]: 300,
  [MasteryTier.Craftsman]: 750,
  [MasteryTier.Master]: 1800,
  [MasteryTier.Grandmaster]: 4500,
  [MasteryTier.Legendary]: 10000,
  [MasteryTier.God]: Number.POSITIVE_INFINITY,
};

export const MASTERY_TIER_LABELS: Record<MasteryTier, string> = {
  [MasteryTier.Apprentice]: "Apprentice",
  [MasteryTier.Journeyman]: "Journeyman",
  [MasteryTier.Craftsman]: "Craftsman",
  [MasteryTier.Master]: "Master",
  [MasteryTier.Grandmaster]: "Grandmaster",
  [MasteryTier.Legendary]: "Legendary",
  [MasteryTier.God]: "God",
};

/** Hex colors — NO oklch/rgb, Three.js compatible. */
export const MASTERY_TIER_COLORS: Record<MasteryTier, string> = {
  [MasteryTier.Apprentice]: "#9ca3af", // gray
  [MasteryTier.Journeyman]: "#60a5fa", // blue-400
  [MasteryTier.Craftsman]: "#3b82f6", // blue-500
  [MasteryTier.Master]: "#1d4ed8", // blue-700
  [MasteryTier.Grandmaster]: "#a855f7", // purple-500
  [MasteryTier.Legendary]: "#f59e0b", // amber-500 (gold)
  [MasteryTier.God]: "#f8fafc", // near-white (radiant)
};

export type WeaponObjectTypeKey =
  | "sword"
  | "staff"
  | "bow"
  | "axe"
  | "dagger"
  | "shield"
  | "wand"
  | "spear"
  | "mace"
  | "custom";

export interface MasterySkillData {
  id: string;
  objectType: WeaponObjectTypeKey;
  name: string;
  description: string;
  requiredMasteryLevel: number;
  isUnlocked: boolean;
  icon?: string;
}

export interface MasteryTreeData {
  objectType: WeaponObjectTypeKey;
  skills: MasterySkillData[];
}

export interface CharacterMasteryProgressData {
  characterId: string;
  objectType: WeaponObjectTypeKey;
  masteryPoints: number;
  masteryLevel: number;
  unlockedSkillIds: string[];
}

export interface ItemInstanceMasteryData {
  characterId: string;
  itemInstanceId: string;
  objectType: WeaponObjectTypeKey;
  masteryPoints: number;
  masteryLevel: number;
}

export interface MasteryRollResultData {
  roll1: number;
  roll2: number;
  total: number;
  skillsUnlocked: number; // 0, 1, 2, or 5
}

// Thresholds: 15-24 = 1 skill, 25-29 = 2 skills, 30 = 5 skills
export const MASTERY_ROLL_THRESHOLDS = {
  ONE_SKILL: 15,
  TWO_SKILLS: 25,
  FIVE_SKILLS: 30,
} as const;
