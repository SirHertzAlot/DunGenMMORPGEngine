// React hook for managing 3D dungeon rendering state

import { useMemo } from "react";
import { generateDungeonRenderData } from "../lib/dungeonPrimitiveGenerator";
import type { DungeonData } from "../lib/rotjsDungeonGenerator";
import type { DungeonRenderData } from "../types/dungeon3d";

/**
 * Hook that generates and memoizes 3D dungeon render data
 * Validates dungeon data and filters out empty cells before rendering
 * @param dungeonData - Generated dungeon data
 * @returns Render data with floor cubes, wall cubes, and bounds
 */
export function useDungeon3DRenderer(
  dungeonData: DungeonData,
): DungeonRenderData {
  const renderData = useMemo(() => {
    // Generate render data with validation filtering
    return generateDungeonRenderData(dungeonData);
  }, [dungeonData]);

  return renderData;
}
