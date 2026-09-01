/**
 * Collision System
 *
 * Prevents mobs from occupying dungeon wall cells.
 * Stores the previous valid position and reverts on collision.
 * Reads/writes Transform via Map API (entity.components.get).
 */

import type { Entity, System } from "../../types/runtime";
import { isWalkable } from "../dungeonColorParser";
import { worldToCell } from "../dungeonGridMapper";

let dungeonCells: number[][] = [];
let dungeonWidth = 0;
let dungeonHeight = 0;

export function setCollisionDungeonData(
  cells: number[][],
  width: number,
  height: number,
): void {
  dungeonCells = cells;
  dungeonWidth = width;
  dungeonHeight = height;
  console.log(
    `[CollisionSystem] Dungeon grid set: ${height} rows x ${width} cols`,
  );
}

// Alias used by Dungeon3DScene
export function setCollisionDungeonGrid(cells: number[][]): void {
  const height = cells.length;
  const width = cells[0]?.length ?? 0;
  setCollisionDungeonData(cells, width, height);
}

export const CollisionSystem: System = {
  id: "collision-system",
  name: "Collision System",
  priority: 20,
  requiredComponents: ["Transform", "CollisionBody"],
  enabled: true,

  execute(entities: Entity[], _deltaTime: number): void {
    if (dungeonCells.length === 0) return;

    for (const entity of entities) {
      if (!entity.active) continue;
      if (
        !entity.components.has("Transform") ||
        !entity.components.has("CollisionBody")
      )
        continue;

      const transform = entity.components.get("Transform") as any;
      const collision = entity.components.get("CollisionBody") as any;

      if (!transform?.position) continue;

      const { col, row } = worldToCell(
        transform.position.x,
        transform.position.z,
      );

      const outOfBounds =
        col < 0 || col >= dungeonWidth || row < 0 || row >= dungeonHeight;
      const cellVal = outOfBounds ? undefined : dungeonCells[row]?.[col];
      const inWall = cellVal === undefined || !isWalkable(cellVal);

      if (inWall) {
        // Revert to previous valid position
        const prevPos = collision.previousPosition;
        if (prevPos) {
          transform.position.x = prevPos.x;
          transform.position.y = prevPos.y;
          transform.position.z = prevPos.z;
        }

        // Reset wander so it picks a new target
        if (entity.components.has("WanderBehavior")) {
          const wander = entity.components.get("WanderBehavior") as any;
          wander.state = "idle";
          wander.targetPosition = null;
          wander.targetX = null;
          wander.targetZ = null;
          wander.stuckTimer = 0;
          wander.lastMoveTime = Date.now();
        }
      } else {
        // Store current position as valid previous
        collision.previousPosition = {
          x: transform.position.x,
          y: transform.position.y,
          z: transform.position.z,
        };
      }
    }
  },
};
