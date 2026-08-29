/**
 * Mastery System
 *
 * Awards XP to any entity with a Masterable component, handles tier
 * advancement rolls, and appends newly unlocked skill IDs.
 *
 * Opt-in pattern: entities without the Masterable component are silently
 * ignored — no side-effects, no errors.
 *
 * Usage:
 *   awardMasteryXP(entity, "sword_strike", 25);
 *   setMasteryCallbacks({ onMasteryAdvanced, onSkillsUnlocked });
 */

import type { MasteryTier } from "../../types/mastery";
import {
  MASTERY_TIER_XP_THRESHOLDS,
  MasteryTier as MasteryTierConst,
} from "../../types/mastery";
import type { Entity, Masterable } from "../../types/runtime";
import { debugLogger } from "../debugLogger";
import {
  determineUnlockedSkills,
  rollMasteryAdvancement,
} from "../masteryRollEngine";

// ── Callback types ─────────────────────────────────────────────────────────────

export type MasteryAdvancedCallback = (
  entityId: string,
  oldTier: MasteryTier,
  newTier: MasteryTier,
  rollTotal: number,
) => void;

export type SkillsUnlockedCallback = (
  entityId: string,
  newSkillIds: string[],
) => void;

// ── Module-level callbacks ──────────────────────────────────────────────────────

let onMasteryAdvanced: MasteryAdvancedCallback | null = null;
let onSkillsUnlocked: SkillsUnlockedCallback | null = null;

export function setMasteryCallbacks(cbs: {
  onMasteryAdvanced?: MasteryAdvancedCallback;
  onSkillsUnlocked?: SkillsUnlockedCallback;
}): void {
  if (cbs.onMasteryAdvanced !== undefined)
    onMasteryAdvanced = cbs.onMasteryAdvanced;
  if (cbs.onSkillsUnlocked !== undefined)
    onSkillsUnlocked = cbs.onSkillsUnlocked;
}

// ── Helpers ────────────────────────────────────────────────────────────────────

/** Returns the next tier above current, capped at God. */
function nextTier(tier: MasteryTier): MasteryTier {
  return Math.min(tier + 1, MasteryTierConst.God) as MasteryTier;
}

/**
 * Build a pool of candidate skill IDs for the given action type and tier.
 * The IDs are deterministic so the roll engine can slice them consistently.
 */
function buildSkillPool(actionType: string, tier: MasteryTier): string[] {
  // Generate 10 candidate skill IDs per action+tier combination.
  // Named with tier label so downstream UI can display them.
  const tierSuffix = tier.toString();
  return Array.from(
    { length: 10 },
    (_, i) => `${actionType}_tier${tierSuffix}_skill${i + 1}`,
  );
}

// ── Core function ──────────────────────────────────────────────────────────────

/**
 * Award XP to an entity's Masterable component.
 * - Does nothing if the entity lacks the Masterable component.
 * - Triggers advancement roll when XP threshold for the current tier is met.
 * - Logs all events via debugLogger.
 */
export function awardMasteryXP(
  entity: Entity,
  actionType: string,
  xp: number,
): void {
  const masterable = entity.components.get("Masterable") as
    | Masterable
    | undefined;

  // Opt-in check — silently skip non-masterable entities.
  if (!masterable) return;

  const prevPoints = masterable.masteryPoints;
  masterable.masteryPoints += xp;
  masterable.actionType = actionType;

  debugLogger.info(
    "mastery",
    `XP +${xp} → ${masterable.masteryPoints} pts [${actionType}]`,
    {
      entityId: entity.id,
      actionType,
      xp,
      totalPoints: masterable.masteryPoints,
      tier: masterable.masteryTier,
    },
  );

  // Check if we crossed the threshold for the current tier.
  const threshold = MASTERY_TIER_XP_THRESHOLDS[masterable.masteryTier];
  const crossedThreshold =
    prevPoints < threshold && masterable.masteryPoints >= threshold;

  if (!crossedThreshold || masterable.masteryTier === MasteryTierConst.God) {
    return;
  }

  // ── Advancement roll ──────────────────────────────────────────────────────

  const rollSeed = Date.now() ^ (entity.id.length * 0x9e3779b9);
  const rollResult = rollMasteryAdvancement(rollSeed);
  const oldTier = masterable.masteryTier;

  if (rollResult.skillsUnlocked > 0) {
    const skillPool = buildSkillPool(actionType, oldTier);
    const newSkillIds = determineUnlockedSkills(
      skillPool,
      masterable.unlockedSkillIds,
      rollResult.skillsUnlocked,
    );

    if (newSkillIds.length > 0) {
      masterable.unlockedSkillIds = [
        ...masterable.unlockedSkillIds,
        ...newSkillIds,
      ];

      debugLogger.success(
        "mastery",
        `Skills unlocked for entity ${entity.id}: ${newSkillIds.join(", ")}`,
        { entityId: entity.id, newSkillIds, rollTotal: rollResult.total },
      );

      onSkillsUnlocked?.(entity.id, newSkillIds);
    }

    // Advance tier and level
    const newTier = nextTier(oldTier);
    masterable.masteryTier = newTier;
    masterable.masteryLevel += 1;
    masterable.lastRollTimestamp = Date.now();
    // Reset XP accumulator for the new tier
    masterable.masteryPoints = 0;

    debugLogger.success(
      "mastery",
      `Tier advancement: ${entity.id} — Tier ${oldTier} → ${newTier} (roll ${rollResult.roll1}+${rollResult.roll2}=${rollResult.total})`,
      {
        entityId: entity.id,
        oldTier,
        newTier,
        rollResult,
        masteryLevel: masterable.masteryLevel,
      },
    );

    onMasteryAdvanced?.(entity.id, oldTier, newTier, rollResult.total);
  } else {
    // Roll didn't unlock anything — keep XP but note the failed roll
    masterable.lastRollTimestamp = Date.now();

    debugLogger.info(
      "mastery",
      `Advancement roll failed for ${entity.id} (roll ${rollResult.roll1}+${rollResult.roll2}=${rollResult.total}, need ≥15)`,
      { entityId: entity.id, rollResult, tier: oldTier },
    );
  }
}
