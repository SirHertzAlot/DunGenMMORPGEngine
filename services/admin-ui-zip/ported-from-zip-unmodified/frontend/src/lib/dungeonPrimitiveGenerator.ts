// Module for generating cube primitive instances for dungeon rendering

import type { CubeInstance, DungeonRenderData } from "../types/dungeon3d";
import {
  cellValueToType,
  getColorForCellType,
  isValidDungeonCell,
  isWalkable,
  isWall,
} from "./dungeonColorParser";
import {
  calculateDungeonBounds,
  getFloorCubePosition,
  getWallCubePosition,
} from "./dungeonGridMapper";
import type { DungeonData } from "./rotjsDungeonGenerator";

/**
 * Generates floor cube instances for all walkable cells
 * Includes boss_floor cells (value 4) with their distinct color
 * @param dungeonData - Generated dungeon data
 * @returns Array of floor cube instances
 */
export function generateFloorCubes(dungeonData: DungeonData): CubeInstance[] {
  const floorCubes: CubeInstance[] = [];
  const { width, height, cells } = dungeonData;

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const cellValue = cells[y][x];

      // Only render walkable cells that are valid dungeon cells
      if (isWalkable(cellValue) && isValidDungeonCell(cellValue, x, y, cells)) {
        const position = getFloorCubePosition(x, y);
        const cellType = cellValueToType(cellValue);
        const color = getColorForCellType(cellType);

        floorCubes.push({
          x: position.x,
          y: position.y,
          z: position.z,
          color,
          type: "floor",
        });
      }
    }
  }

  return floorCubes;
}

/**
 * Generates wall cube instances (3 cubes stacked vertically)
 * Only generates walls adjacent to walkable areas, not empty space
 * @param dungeonData - Generated dungeon data
 * @returns Array of wall cube instances
 */
export function generateWallCubes(dungeonData: DungeonData): CubeInstance[] {
  const wallCubes: CubeInstance[] = [];
  const { width, height, cells } = dungeonData;
  const wallColor = getColorForCellType("wall");

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const cellValue = cells[y][x];

      // Only render walls that are adjacent to walkable areas (valid dungeon cells)
      if (isWall(cellValue) && isValidDungeonCell(cellValue, x, y, cells)) {
        // Stack 3 cubes vertically for each wall cell
        for (let stackLevel = 0; stackLevel < 3; stackLevel++) {
          const position = getWallCubePosition(x, y, stackLevel);

          wallCubes.push({
            x: position.x,
            y: position.y,
            z: position.z,
            color: wallColor,
            type: "wall",
          });
        }
      }
    }
  }

  return wallCubes;
}

/**
 * Generates all cube instances for dungeon rendering
 * Filters out empty space that should not be rendered
 * @param dungeonData - Generated dungeon data
 * @returns Complete render data with floor cubes, wall cubes, and bounds
 */
export function generateDungeonRenderData(
  dungeonData: DungeonData,
): DungeonRenderData {
  const floorCubes = generateFloorCubes(dungeonData);
  const wallCubes = generateWallCubes(dungeonData);
  const bounds = calculateDungeonBounds(dungeonData.width, dungeonData.height);

  return {
    floorCubes,
    wallCubes,
    bounds,
  };
}
