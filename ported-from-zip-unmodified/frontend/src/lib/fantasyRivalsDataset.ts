/**
 * Fantasy Rivals source-model dataset.
 *
 * Naming convention for procedural weapon IDs:
 *   frv.weapon.<owner_slug>.v<variant_2d>.<format>
 *
 * Examples:
 *   frv.weapon.ancient_queen.v01.fbx
 *   frv.weapon.mechanical_golem.v02.obj
 */

export type SourceFormat = 'fbx' | 'obj';

export interface FantasyRivalsModelName {
  sourceName: string;
  format: SourceFormat;
  folder: 'FBX' | 'OBJ' | 'Characters';
  canonicalModelId: string;
}

export interface FantasyRivalsWeaponDefinition {
  sourceName: string;
  format: SourceFormat;
  ownerSlug: string;
  ownerName: string;
  variant: number;
  proceduralWeaponId: string;
}

// Rival pack FBX folder names.
export const FANTASY_RIVALS_FBX_FILES = [
  'Characters.fbx',
  'Characters_BR.fbx',
  'FX_Runes.fbx',
  'Medusa_Snakes.fbx',
  'Mystic_Arms.fbx',
  'SM_Base_Dirt_01.fbx',
  'SM_Base_Dungeon_01.fbx',
  'SM_Base_Grass_01.fbx',
  'SM_Base_Mechanical_01.fbx',
  'SM_Base_Rock_01.fbx',
  'SM_Prop_Backharness_01.fbx',
  'SM_Prop_Backharness_02.fbx',
  'SM_Prop_Backharness_03.fbx',
  'SM_Prop_Bones_01.fbx',
  'SM_Prop_Mushroom_01.fbx',
  'SM_Prop_Mushroom_02.fbx',
  'SM_Prop_Pouch_01.fbx',
  'SM_Prop_Pouch_02.fbx',
  'SM_Prop_Pouch_03.fbx',
  'SM_Prop_Pouch_Bag_01.fbx',
  'SM_Prop_Skull_01.fbx',
  'SM_Prop_Tree_01.fbx',
  'SM_Prop_Tree_02.fbx',
  'SM_Prop_TrollHelmet_01.fbx',
  'SM_Wep_AncientQueen_01.fbx',
  'SM_Wep_AncientWarrior_01.fbx',
  'SM_Wep_BarbarianGiant_01.fbx',
  'SM_Wep_BigOrk_01.fbx',
  'SM_Wep_DarkElf_01.fbx',
  'SM_Wep_Dwarf_01.fbx',
  'SM_Wep_ElementalGolem_01.fbx',
  'SM_Wep_EvilGod_01.fbx',
  'SM_Wep_ForestGuardian_01.fbx',
  'SM_Wep_ForestWitch_01.fbx',
  'SM_Wep_FortGolem_01.fbx',
  'SM_Wep_MechanicalGolem_01.fbx',
  'SM_Wep_MechanicalGolem_02.fbx',
  'SM_Wep_Medusa_01.fbx',
  'SM_Wep_MutantGuy_01.fbx',
  'SM_Wep_Mystic_01.fbx',
  'SM_Wep_PigButcher_01.fbx',
  'SM_Wep_RedDemon_01.fbx',
  'SM_Wep_Slayer_01.fbx',
  'SM_Wep_SpiritDemon_01.fbx',
  'SM_Wep_Troll_01.fbx',
] as const;

// Rival pack OBJ folder names.
export const FANTASY_RIVALS_OBJ_FILES = [
  'SM_Base_Dirt_01.obj',
  'SM_Base_Dungeon_01.obj',
  'SM_Base_Grass_01.obj',
  'SM_Base_Mechanical_01.obj',
  'SM_Base_Rock_01.obj',
  'SM_Prop_Backharness_01.obj',
  'SM_Prop_Backharness_02.obj',
  'SM_Prop_Backharness_03.obj',
  'SM_Prop_Bones_01.obj',
  'SM_Prop_Mushroom_01.obj',
  'SM_Prop_Mushroom_02.obj',
  'SM_Prop_Pouch_01.obj',
  'SM_Prop_Pouch_02.obj',
  'SM_Prop_Pouch_03.obj',
  'SM_Prop_Pouch_Bag_01.obj',
  'SM_Prop_Skull_01.obj',
  'SM_Prop_Tree_01.obj',
  'SM_Prop_Tree_02.obj',
  'SM_Prop_TrollHelmet.obj',
  'SM_Wep_AncientQueen_01.obj',
  'SM_Wep_AncientWarrior_01.obj',
  'SM_Wep_BarbarianGiant_01.obj',
  'SM_Wep_BigOrk_01.obj',
  'SM_Wep_DarkElf_01.obj',
  'SM_Wep_Dwarf_01.obj',
  'SM_Wep_ElementalGolem_01.obj',
  'SM_Wep_EvilGod_01.obj',
  'SM_Wep_ForestGuardian_01.obj',
  'SM_Wep_ForestWitch_01.obj',
  'SM_Wep_FortGolem_01.obj',
  'SM_Wep_MechanicalGolem_01.obj',
  'SM_Wep_MechanicalGolem_02.obj',
  'SM_Wep_Medusa_01.obj',
  'SM_Wep_MutantGuy_01.obj',
  'SM_Wep_Mystic_01.obj',
  'SM_Wep_PigButcher_01.obj',
  'SM_Wep_RedDemon_01.obj',
  'SM_Wep_Slayer_01.obj',
  'SM_Wep_SpiritDemon_01.obj',
  'SM_Wep_Troll_01.obj',
] as const;

// Rival pack character FBX names (used to enrich enemy name pool).
export const FANTASY_RIVALS_CHARACTER_FBX_FILES = [
  'SK_Arms_Lower_01.fbx',
  'SK_Arms_Upper_01.fbx',
  'SK_BR_Character_BarbarianGiant_01.fbx',
  'SK_BR_Character_Big_Ork_01.fbx',
  'SK_BR_Character_Dwarf_01.fbx',
  'SK_BR_Character_ElementalGolem_01.fbx',
  'SK_BR_Character_FortGolem_01.fbx',
  'SK_BR_Character_MechanicalGolem_01.fbx',
  'SK_BR_Character_MutantGuy_01.fbx',
  'SK_BR_Character_Pig_Butcher_01.fbx',
  'SK_BR_Character_RedDemon_01.fbx',
  'SK_BR_Character_Slayer_01.fbx',
  'SK_BR_Character_Troll_01.fbx',
  'SK_Character_AncientQueen_01.fbx',
  'SK_Character_Ancient_Warrior_01.fbx',
  'SK_Character_DarkElf_01.fbx',
  'SK_Character_EvilGod_01.fbx',
  'SK_Character_ForestGuardian_01.fbx',
  'SK_Character_ForestWitch_01.fbx',
  'SK_Character_Medusa_01.fbx',
  'SK_Character_Mystic_01.fbx',
  'SK_Character_SpiritDemon.fbx',
  'SK_MedusaSnakes_01.fbx',
] as const;

function stripExtension(file: string): string {
  return file.replace(/\.[^.]+$/, '');
}

function normalizeSlug(input: string): string {
  return input
    .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
    .replace(/[^a-zA-Z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .replace(/_+/g, '_')
    .toLowerCase();
}

function parseWeaponFile(file: string): { ownerSlug: string; ownerName: string; variant: number } {
  const core = stripExtension(file);
  const match = core.match(/^SM_Wep_(.+?)_(\d+)$/i);
  if (!match) {
    return {
      ownerSlug: normalizeSlug(core),
      ownerName: core,
      variant: 1,
    };
  }

  const ownerRaw = match[1];
  const ownerName = ownerRaw
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/_/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();

  return {
    ownerSlug: normalizeSlug(ownerRaw),
    ownerName,
    variant: Number.parseInt(match[2], 10),
  };
}

function toFormat(file: string): SourceFormat {
  return file.toLowerCase().endsWith('.obj') ? 'obj' : 'fbx';
}

export const FANTASY_RIVALS_MODEL_NAMES: FantasyRivalsModelName[] = [
  ...FANTASY_RIVALS_FBX_FILES.map((file) => ({
    sourceName: file,
    format: 'fbx' as const,
    folder: 'FBX' as const,
    canonicalModelId: `frv.model.fbx.${normalizeSlug(stripExtension(file))}`,
  })),
  ...FANTASY_RIVALS_OBJ_FILES.map((file) => ({
    sourceName: file,
    format: 'obj' as const,
    folder: 'OBJ' as const,
    canonicalModelId: `frv.model.obj.${normalizeSlug(stripExtension(file))}`,
  })),
  ...FANTASY_RIVALS_CHARACTER_FBX_FILES.map((file) => ({
    sourceName: file,
    format: 'fbx' as const,
    folder: 'Characters' as const,
    canonicalModelId: `frv.model.character.${normalizeSlug(stripExtension(file))}`,
  })),
];

const ALL_WEAPON_FILES = [
  ...FANTASY_RIVALS_FBX_FILES.filter((file) => file.startsWith('SM_Wep_')),
  ...FANTASY_RIVALS_OBJ_FILES.filter((file) => file.startsWith('SM_Wep_')),
];

export const FANTASY_RIVALS_WEAPON_DEFINITIONS: FantasyRivalsWeaponDefinition[] = ALL_WEAPON_FILES
  .map((file) => {
    const format = toFormat(file);
    const parsed = parseWeaponFile(file);
    const v = String(parsed.variant).padStart(2, '0');

    return {
      sourceName: file,
      format,
      ownerSlug: parsed.ownerSlug,
      ownerName: parsed.ownerName,
      variant: parsed.variant,
      proceduralWeaponId: `frv.weapon.${parsed.ownerSlug}.v${v}.${format}`,
    };
  })
  .sort((a, b) => a.proceduralWeaponId.localeCompare(b.proceduralWeaponId));

// Enemy names extracted from both weapon ownership and character FBX names.
export const FANTASY_RIVALS_ENEMY_NAMES = Array.from(new Set([
  ...FANTASY_RIVALS_WEAPON_DEFINITIONS.map((x) => x.ownerName),
  ...FANTASY_RIVALS_CHARACTER_FBX_FILES
    .map((file) => stripExtension(file))
    .filter((name) => name.startsWith('SK_Character_') || name.startsWith('SK_BR_Character_'))
    .map((name) => name
      .replace(/^SK_BR_Character_/i, '')
      .replace(/^SK_Character_/i, '')
      .replace(/^SK_/i, '')
      .replace(/_[0-9]+$/, '')
      .replace(/_/g, ' ')
      .replace(/\s+/g, ' ')
      .trim()),
]))
  .map((name) => name
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim())
  .sort((a, b) => a.localeCompare(b));

export const FANTASY_RIVALS_NAMING_CONVENTION = {
  modelId: 'frv.model.<source_folder>.<normalized_source_name>',
  weaponId: 'frv.weapon.<owner_slug>.v<variant_2d>.<format>',
  examples: [
    'frv.model.fbx.sm_wep_ancient_queen_01',
    'frv.weapon.ancient_queen.v01.fbx',
    'frv.weapon.mechanical_golem.v02.obj',
  ],
} as const;
