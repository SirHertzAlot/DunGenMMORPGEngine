/**
 * Seeded RNG utilities for deterministic generation
 * Uses mulberry32 algorithm for high-quality pseudo-random numbers
 */

/**
 * mulberry32 - A simple, fast, and high-quality 32-bit PRNG
 * Returns a deterministic pseudo-random number generator function
 * @param seed - Initial seed value for deterministic generation
 * @returns Function that returns pseudo-random numbers in range [0, 1)
 */
export function mulberry32(seed: number): () => number {
  return () => {
    // biome-ignore lint/suspicious/noAssignInExpressions: mulberry32 PRNG algorithm requires in-expression seed increment
    // biome-ignore lint/style/noParameterAssign: mulberry32 algorithm mutates seed by design
    let t = (seed += 0x6d2b79f5);
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * Create a deterministic hash from a string
 * @param str - Input string to hash
 * @returns 32-bit integer hash
 */
export function hashString(str: string): number {
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    const char = str.charCodeAt(i);
    hash = (hash << 5) - hash + char;
    hash = hash & hash; // Convert to 32-bit integer
  }
  return Math.abs(hash);
}

/**
 * Generate a deterministic ID from seed and counter
 * @param seed - Base seed value
 * @param counter - Counter value for uniqueness
 * @param prefix - Optional prefix for the ID
 * @returns Deterministic ID string
 */
export function generateDeterministicId(
  seed: number,
  counter: number,
  prefix = "entity",
): string {
  return `${prefix}_${seed}_${counter}`;
}
