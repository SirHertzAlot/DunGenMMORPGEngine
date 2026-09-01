/**
 * Mob Factory
 *
 * Creates ECS entities (Map-based components, matching types/runtime.ts Entity)
 * for dungeon mobs with all required components pre-populated.
 */

import { MasteryTier } from "../types/mastery";
import type { Entity, Masterable } from "../types/runtime";

export type MobTypeName = "goblin" | "skeleton" | "orc" | "troll" | "wraith";

export const MOB_TYPES: MobTypeName[] = [
  "goblin",
  "skeleton",
  "orc",
  "troll",
  "wraith",
];

export const MOB_DANGER_RATINGS: Record<MobTypeName, number> = {
  goblin: 1,
  skeleton: 2,
  orc: 3,
  troll: 4,
  wraith: 5,
};

const MOB_STATS: Record<
  MobTypeName,
  { hp: number; speed: number; damage: number; name: string }
> = {
  goblin: { hp: 60, speed: 5.5, damage: 8, name: "Goblin" },
  skeleton: { hp: 80, speed: 4.5, damage: 10, name: "Skeleton" },
  orc: { hp: 120, speed: 3.5, damage: 18, name: "Orc Warrior" },
  troll: { hp: 180, speed: 2.5, damage: 25, name: "Cave Troll" },
  wraith: { hp: 50, speed: 6.5, damage: 12, name: "Shadow Wraith" },
};

export interface MobSpawnConfig {
  id: string;
  type: MobTypeName;
  x: number;
  z: number;
}

let mobCounter = 0;

/**
 * Creates a mob ECS entity with Map-based components.
 * Compatible with types/runtime.ts Entity interface.
 */
export function createMobEntity(config: MobSpawnConfig): Entity {
  const stats = MOB_STATS[config.type];
  const dangerRating = MOB_DANGER_RATINGS[config.type];
  const now = Date.now();

  console.log(
    `[MobFactory] Creating ${config.type} (${config.id}) at world (${config.x}, ${config.z})`,
  );

  const components = new Map<string, any>();

  components.set("Transform", {
    position: { x: config.x, y: 0, z: config.z },
    rotation: { x: 0, y: 0, z: 0 },
    scale: { x: 3, y: 3, z: 3 },
  });

  components.set("Health", {
    max: stats.hp,
    current: stats.hp,
    regenerationRate: 0,
    hp: stats.hp,
    maxHp: stats.hp,
  });

  components.set("AI", {
    behaviorType: config.type,
    mobType: config.type,
    aggroRadius: 20,
    patrolPath: [],
    currentState: "idle",
    state: "idle",
    attackRange: 6,
    attackDamage: stats.damage,
    attackCooldown: 1500,
    lastAttackTime: 0,
  });

  components.set("WanderBehavior", {
    targetPosition: null,
    wanderRadius: 30,
    speed: stats.speed,
    state: "idle",
    idleTimer: 0,
    stuckTimer: 0,
    idleDuration: 2,
    lastMoveTime: now,
    waitInterval: 2000 + Math.random() * 3000,
    targetX: null,
    targetZ: null,
  });

  components.set("CollisionBody", {
    boundingBox: { width: 3, height: 3, depth: 3 },
    isGrounded: true,
    previousPosition: { x: config.x, y: 0, z: config.z },
    width: 3,
    height: 3,
    depth: 3,
    lastValidX: config.x,
    lastValidZ: config.z,
    previousX: config.x,
    previousZ: config.z,
  });

  components.set("CombatTarget", {
    targetEntityId: null,
    attackRange: 6,
    attackDamage: stats.damage,
    attackCooldown: 1500,
    lastAttackTimestamp: 0,
    isEngaged: false,
  });

  components.set("DangerRating", {
    rating: dangerRating,
    dangerRating,
  });

  components.set("BuffDebuffState", {
    activeBuffs: [],
    activeDebuffs: [],
  });

  components.set("MobMeta", {
    type: config.type,
    name: stats.name,
    level: dangerRating,
  });

  // Lootable component — keyed to mob type for tier weighting
  components.set("Lootable", {
    lootTableRef: config.type,
    dropCountMin: 1,
    dropCountMax: 3,
  });

  // Masterable component — only for entities that also have CombatTarget
  if (components.has("CombatTarget")) {
    const masterable: Masterable = {
      masteryPoints: 0,
      masteryLevel: 0,
      masteryTier: MasteryTier.Apprentice,
      unlockedSkillIds: [],
      lastRollTimestamp: 0,
      actionType: "combat",
    };
    components.set("masterable", masterable);
  }

  const entity: Entity = {
    id: config.id,
    components,
    active: true,
    createdAt: now,
    updatedAt: now,
  };

  console.log(
    `[MobFactory] Entity ${config.id} created with components: ${Array.from(components.keys()).join(", ")}`,
  );
  return entity;
}

/**
 * Legacy createMob — accepts (runtime, worldX, worldZ, mobType?) or (position, seed?).
 * Returns the created entity ID.
 */
export function createMob(
  runtimeOrPosition: any,
  worldXOrSeed?: number,
  worldZ?: number,
  mobType?: string,
): string {
  let spawnX: number;
  let spawnZ: number;
  let type: MobTypeName;

  if (
    runtimeOrPosition &&
    typeof runtimeOrPosition.createEntity === "function"
  ) {
    spawnX = worldXOrSeed ?? 0;
    spawnZ = worldZ ?? 0;
    type = (mobType as MobTypeName) ?? "goblin";
  } else {
    const pos = runtimeOrPosition as { x: number; y: number; z: number };
    spawnX = pos.x;
    spawnZ = pos.z;
    type = "goblin";
  }

  const id = `mob-${type}-${Date.now()}-${++mobCounter}`;
  const entity = createMobEntity({ id, type, x: spawnX, z: spawnZ });

  if (runtimeOrPosition && typeof runtimeOrPosition.addEntity === "function") {
    runtimeOrPosition.addEntity(entity);
  } else if (
    runtimeOrPosition &&
    typeof runtimeOrPosition.createEntity === "function"
  ) {
    // Old API: use addEntity if available
    if (typeof runtimeOrPosition.addEntity === "function") {
      runtimeOrPosition.addEntity(entity);
    }
  }

  return id;
}

export function destroyAllMobs(runtime: any, mobIds: string[]): void {
  for (const id of mobIds) {
    try {
      if (typeof runtime.destroyEntity === "function") {
        runtime.destroyEntity(id);
      } else if (typeof runtime.removeEntity === "function") {
        runtime.removeEntity(id);
      }
    } catch {
      // Entity may already be gone
    }
  }
}
