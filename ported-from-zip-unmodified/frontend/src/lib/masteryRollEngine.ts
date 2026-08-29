import { mulberry32 } from "../core/utils/seededRng";
import {
  MASTERY_ROLL_THRESHOLDS,
  type MasteryRollResultData,
} from "../types/mastery";

/**
 * Roll a single d15 using a seeded RNG
 */
function rollD15(rng: () => number): number {
  return Math.floor(rng() * 15) + 1;
}

/**
 * Perform a 2d15 mastery advancement roll.
 * Returns roll results and how many skills were unlocked.
 */
export function rollMasteryAdvancement(seed: number): MasteryRollResultData {
  const rng = mulberry32(seed);
  const roll1 = rollD15(rng);
  const roll2 = rollD15(rng);
  const total = roll1 + roll2;

  let skillsUnlocked = 0;
  if (total >= MASTERY_ROLL_THRESHOLDS.FIVE_SKILLS) {
    skillsUnlocked = 5;
  } else if (total >= MASTERY_ROLL_THRESHOLDS.TWO_SKILLS) {
    skillsUnlocked = 2;
  } else if (total >= MASTERY_ROLL_THRESHOLDS.ONE_SKILL) {
    skillsUnlocked = 1;
  }

  return { roll1, roll2, total, skillsUnlocked };
}

/**
 * Determine which skills to unlock from a mastery tree based on current progress and roll result.
 * Returns IDs of newly unlocked skills.
 */
export function determineUnlockedSkills(
  allSkillIds: string[],
  currentlyUnlockedIds: string[],
  skillsToUnlock: number,
): string[] {
  const lockedSkills = allSkillIds.filter(
    (id) => !currentlyUnlockedIds.includes(id),
  );
  return lockedSkills.slice(0, skillsToUnlock);
}
