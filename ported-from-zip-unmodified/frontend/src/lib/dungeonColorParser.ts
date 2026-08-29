// Utility module for parsing dungeon cell types and colors
// Based on rot.js default coloring system

import { CELL_BOSS_FLOOR } from "../types/dungeon3d";

export interface DungeonColors {
  wall: string;
  floor: string;
  corridor: string;
  door: string;
  boss_floor: string;
}

// rot.js default color scheme for dungeon generation
export const DUNGEON_COLORS: DungeonColors = {
  wall: "oklch(0.25 0.02 240)", // Dark gray-blue
  floor: "oklch(0.85 0.05 80)", // Light beige (room floor)
  corridor: "oklch(0.70 0.08 60)", // Medium tan
  door: "oklch(0.60 0.15 30)", // Brown
  boss_floor: "oklch(0.45 0.18 15)", // Deep crimson-red for boss room
};

export type CellType = "wall" | "floor" | "corridor" | "door" | "boss_floor";

/**
 * Gets the color for a specific cell type
 * @param cellType - Type of dungeon cell
 * @returns OKLCH color string
 */
export function getColorForCellType(cellType: CellType): string {
  return DUNGEON_COLORS[cellType];
}

/**
 * Converts numeric cell value to cell type
 * @param cellValue - Numeric value from dungeon generation (0-4)
 * @returns Cell type string
 */
export function cellValueToType(cellValue: number): CellType {
  switch (cellValue) {
    case 0:
      return "wall";
    case 1:
      return "floor";
    case 2:
      return "corridor";
    case 3:
      return "door";
    case CELL_BOSS_FLOOR:
      return "boss_floor";
    default:
      return "wall";
  }
}

/**
 * Checks if a cell is walkable (not a wall)
 * @param cellValue - Numeric value from dungeon generation
 * @returns True if the cell is walkable
 */
export function isWalkable(cellValue: number): boolean {
  return cellValue !== 0;
}

/**
 * Checks if a cell is a wall
 * @param cellValue - Numeric value from dungeon generation
 * @returns True if the cell is a wall
 */
export function isWall(cellValue: number): boolean {
  return cellValue === 0;
}

/**
 * Checks if a cell represents a valid dungeon cell (not empty space)
 * A valid dungeon cell is either walkable OR a wall adjacent to walkable areas
 * @param cellValue - Numeric value from dungeon generation
 * @param x - X coordinate of the cell
 * @param y - Y coordinate of the cell
 * @param cells - Full dungeon grid
 * @returns True if the cell should be rendered in 3D
 */
export function isValidDungeonCell(
  cellValue: number,
  x: number,
  y: number,
  cells: number[][],
): boolean {
  // If it's walkable (floor, corridor, door, boss_floor), it's valid
  if (isWalkable(cellValue)) {
    return true;
  }

  // If it's a wall (value 0), only render if adjacent to walkable cells
  if (isWall(cellValue)) {
    return isAdjacentToWalkable(x, y, cells);
  }

  return false;
}

/**
 * Checks if a wall cell is adjacent to any walkable cells
 * @param x - X coordinate of the wall cell
 * @param y - Y coordinate of the wall cell
 * @param cells - Full dungeon grid
 * @returns True if adjacent to at least one walkable cell
 */
function isAdjacentToWalkable(
  x: number,
  y: number,
  cells: number[][],
): boolean {
  const height = cells.length;
  const width = cells[0]?.length || 0;

  // Check all 8 adjacent cells (including diagonals for better wall coverage)
  const directions = [
    [-1, -1],
    [0, -1],
    [1, -1],
    [-1, 0],
    [1, 0],
    [-1, 1],
    [0, 1],
    [1, 1],
  ];

  for (const [dx, dy] of directions) {
    const checkX = x + dx;
    const checkY = y + dy;

    // Check bounds
    if (checkX >= 0 && checkX < width && checkY >= 0 && checkY < height) {
      if (isWalkable(cells[checkY][checkX])) {
        return true;
      }
    }
  }

  return false;
}
