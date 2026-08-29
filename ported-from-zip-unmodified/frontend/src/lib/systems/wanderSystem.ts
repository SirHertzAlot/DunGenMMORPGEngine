/**
 * Wander System
 *
 * Moves mob entities to random walkable positions within the dungeon.
 * Uses module-level dungeon grid reference set by setWanderDungeonData.
 * Reads/writes Transform via Map API (entity.components.get/set).
 */

import { mulberry32 } from "../../core/utils/seededRng";
import type { Entity, System } from "../../types/runtime";
import { isWalkable } from "../dungeonColorParser";
import { worldToCell } from "../dungeonGridMapper";

// Shared dungeon cells reference — set by the scene before systems run
let dungeonCells: number[][] = [];
let dungeonWidth = 0;
let dungeonHeight = 0;

export function setWanderDungeonData(
  cells: number[][],
  width: number,
  height: number,
): void {
  dungeonCells = cells;
  dungeonWidth = width;
  dungeonHeight = height;
  console.log(
    `[WanderSystem] Dungeon grid set: ${height} rows x ${width} cols`,
  );
}

// Alias used by Dungeon3DScene
export function setWanderDungeonGrid(cells: number[][]): void {
  const height = cells.length;
  const width = cells[0]?.length ?? 0;
  setWanderDungeonData(cells, width, height);
}

// Per-entity RNG seeds
const entityRngMap = new Map<string, () => number>();

function getRng(entityId: string): () => number {
  if (!entityRngMap.has(entityId)) {
    let hash = 0;
    for (let i = 0; i < entityId.length; i++) {
      hash = (hash << 5) - hash + entityId.charCodeAt(i);
      hash |= 0;
    }
    entityRngMap.set(entityId, mulberry32(Math.abs(hash)));
  }
  return entityRngMap.get(entityId)!;
}

const MAX_STUCK_TIME = 4000; // ms
const IDLE_PAUSE_MIN = 1000; // ms
const IDLE_PAUSE_MAX = 3000; // ms
const ARRIVAL_THRESHOLD = 1.5;

function pickWalkableTarget(
  currentX: number,
  currentZ: number,
  wanderRadius: number,
  rng: () => number,
): { x: number; z: number } | null {
  if (dungeonCells.length === 0) return null;

  for (let attempt = 0; attempt < 20; attempt++) {
    const angle = rng() * Math.PI * 2;
    const dist = (0.3 + rng() * 0.7) * wanderRadius;
    const targetX = currentX + Math.cos(angle) * dist;
    const targetZ = currentZ + Math.sin(angle) * dist;

    const { col, row } = worldToCell(targetX, targetZ);

    if (col < 0 || col >= dungeonWidth || row < 0 || row >= dungeonHeight)
      continue;

    const cellVal = dungeonCells[row]?.[col];
    if (cellVal !== undefined && isWalkable(cellVal)) {
      return { x: targetX, z: targetZ };
    }
  }
  return null;
}

export const WanderSystem: System = {
  id: "wander-system",
  name: "Wander System",
  priority: 10,
  requiredComponents: ["Transform", "WanderBehavior"],
  enabled: true,

  execute(entities: Entity[], deltaTime: number): void {
    if (dungeonCells.length === 0) {
      return;
    }

    const now = Date.now();

    for (const entity of entities) {
      if (!entity.active) continue;
      if (
        !entity.components.has("Transform") ||
        !entity.components.has("WanderBehavior")
      )
        continue;

      const transform = entity.components.get("Transform") as any;
      const wander = entity.components.get("WanderBehavior") as any;
      const rng = getRng(entity.id);

      if (!transform?.position) continue;

      if (wander.state === "idle") {
        // Initialize lastMoveTime if missing
        if (!wander.lastMoveTime) wander.lastMoveTime = now;

        const elapsed = now - wander.lastMoveTime;
        const pauseDuration =
          wander.waitInterval ??
          IDLE_PAUSE_MIN + rng() * (IDLE_PAUSE_MAX - IDLE_PAUSE_MIN);

        if (elapsed >= pauseDuration) {
          const target = pickWalkableTarget(
            transform.position.x,
            transform.position.z,
            wander.wanderRadius ?? 30,
            rng,
          );
          if (target) {
            wander.targetPosition = {
              x: target.x,
              y: transform.position.y,
              z: target.z,
            };
            wander.targetX = target.x;
            wander.targetZ = target.z;
            wander.state = "moving";
            wander.stuckTimer = now;
            wander.waitInterval =
              IDLE_PAUSE_MIN + rng() * (IDLE_PAUSE_MAX - IDLE_PAUSE_MIN);
          }
        }
      } else if (wander.state === "moving") {
        const targetX: number =
          wander.targetX ?? wander.targetPosition?.x ?? transform.position.x;
        const targetZ: number =
          wander.targetZ ?? wander.targetPosition?.z ?? transform.position.z;

        // Stuck detection
        if (!wander.stuckTimer) wander.stuckTimer = now;
        const stuckElapsed = now - wander.stuckTimer;
        if (stuckElapsed > MAX_STUCK_TIME) {
          wander.state = "idle";
          wander.targetPosition = null;
          wander.targetX = null;
          wander.targetZ = null;
          wander.stuckTimer = 0;
          wander.lastMoveTime = now;
          continue;
        }

        const dx = targetX - transform.position.x;
        const dz = targetZ - transform.position.z;
        const dist = Math.sqrt(dx * dx + dz * dz);

        if (dist < ARRIVAL_THRESHOLD) {
          // Arrived
          transform.position.x = targetX;
          transform.position.z = targetZ;
          wander.state = "idle";
          wander.targetPosition = null;
          wander.targetX = null;
          wander.targetZ = null;
          wander.stuckTimer = 0;
          wander.lastMoveTime = now;
        } else {
          // Move toward target — deltaTime is in ms from RuntimeLoop
          const speedUnitsPerMs = (wander.speed ?? 5) / 1000;
          const step = speedUnitsPerMs * deltaTime;
          const nx = dx / dist;
          const nz = dz / dist;
          transform.position.x += nx * Math.min(step, dist);
          transform.position.z += nz * Math.min(step, dist);
        }
      }
    }
  },
};
