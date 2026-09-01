// Utility module for mapping 2D dungeon cells to 3D grid positions
// Uses a 10:1 ratio where each 2D cell = 10x10x10 units in 3D space

import type { SpawnPoint } from "../types/dungeon3d";

export const CELL_SIZE = 10; // Each 2D cell is 10x10x10 units in 3D
export const WALL_HEIGHT = 30; // Walls are 3 cubes high (3 * 10 units)
export const FLOOR_HEIGHT = 10; // Floor cube height
export const SPAWN_MARKER_Y = 8; // Spawn markers float above floor level

export interface GridPosition3D {
  x: number;
  y: number;
  z: number;
}

/**
 * Converts 2D dungeon cell coordinates to 3D grid position
 * @param x - 2D cell x coordinate
 * @param y - 2D cell y coordinate (maps to z in 3D)
 * @returns 3D position with center of the cell
 */
export function cellTo3DPosition(x: number, y: number): GridPosition3D {
  return {
    x: x * CELL_SIZE,
    y: 0, // Ground level
    z: y * CELL_SIZE,
  };
}

/**
 * Calculates the bounding box for a dungeon in 3D space
 * @param width - Dungeon width in cells
 * @param height - Dungeon height in cells
 * @returns Min and max coordinates for the dungeon bounds
 */
export function calculateDungeonBounds(width: number, height: number) {
  return {
    minX: 0,
    maxX: width * CELL_SIZE,
    minZ: 0,
    maxZ: height * CELL_SIZE,
    centerX: (width * CELL_SIZE) / 2,
    centerZ: (height * CELL_SIZE) / 2,
  };
}

/**
 * Gets the position for a wall cube at a specific stack level
 * @param x - 2D cell x coordinate
 * @param y - 2D cell y coordinate
 * @param stackLevel - Vertical stack level (0, 1, or 2 for 3 cubes high)
 * @returns 3D position for the wall cube center
 */
export function getWallCubePosition(
  x: number,
  y: number,
  stackLevel: number,
): GridPosition3D {
  const basePos = cellTo3DPosition(x, y);
  return {
    x: basePos.x,
    y: CELL_SIZE / 2 + stackLevel * CELL_SIZE, // Center of each cube: 5, 15, 25
    z: basePos.z,
  };
}

/**
 * Gets the position for a floor cube (top face at ground level)
 * @param x - 2D cell x coordinate
 * @param y - 2D cell y coordinate
 * @returns 3D position for the floor cube center
 */
export function getFloorCubePosition(x: number, y: number): GridPosition3D {
  const basePos = cellTo3DPosition(x, y);
  return {
    x: basePos.x,
    y: -CELL_SIZE / 2, // Center at -5 so top face is at y=0
    z: basePos.z,
  };
}

/**
 * Converts a 2D spawn point grid position to 3D world coordinates
 * with appropriate y-elevation for marker placement above floor level
 * @param spawnPoint - Spawn point with 2D grid position
 * @returns 3D position compatible with Three.js (x, y, z)
 */
export function mapSpawnPointTo3D(spawnPoint: SpawnPoint): GridPosition3D {
  return {
    x: spawnPoint.position.x * CELL_SIZE,
    y: SPAWN_MARKER_Y, // Elevated above floor so sphere is visible
    z: spawnPoint.position.y * CELL_SIZE,
  };
}

/**
 * Converts Three.js world coordinates back to dungeon grid cell coordinates.
 * This is the inverse of cellTo3DPosition / cellTo3DPosition.
 * @param worldX - World X coordinate
 * @param worldZ - World Z coordinate
 * @returns Grid cell { col, row }
 */
export function worldToCell(
  worldX: number,
  worldZ: number,
): { col: number; row: number } {
  return {
    col: Math.floor(worldX / CELL_SIZE),
    row: Math.floor(worldZ / CELL_SIZE),
  };
}
