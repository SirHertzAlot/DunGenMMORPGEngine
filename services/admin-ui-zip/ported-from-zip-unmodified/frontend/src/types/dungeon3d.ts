// TypeScript types for 3D dungeon rendering

export interface CubeInstance {
  x: number;
  y: number;
  z: number;
  color: string;
  type: "floor" | "wall";
}

export interface DungeonCell3D {
  x: number;
  y: number;
  cellType: "wall" | "floor" | "corridor" | "door" | "boss_floor";
  color: string;
}

export interface DungeonRenderData {
  floorCubes: CubeInstance[];
  wallCubes: CubeInstance[];
  bounds: {
    minX: number;
    maxX: number;
    minZ: number;
    maxZ: number;
    centerX: number;
    centerZ: number;
  };
}

export interface SpawnPoint {
  name: string;
  position: { x: number; y: number };
  type: "dungeon_entrance" | "boss_entrance";
}

// Constants for 3D rendering
export const CELL_SIZE = 10; // Each 2D cell = 10x10x10 units
export const WALL_HEIGHT = 30; // Walls are 3 cubes high
export const FLOOR_HEIGHT = 10; // Floor cube height

// Cell value constants
export const CELL_BOSS_FLOOR = 4; // Boss room floor cell value

// ---- Dungeon Library Export Types ----

export interface DungeonExportEntry {
  seed: number;
  width: number;
  height: number;
  grid: number[][];
  rooms: Array<{ x: number; y: number; width: number; height: number }>;
  spawnPoints: Array<{ x: number; y: number; type: string; name: string }>;
  bossRoom: { x: number; y: number; width: number; height: number } | null;
}

export interface DungeonLibraryExport {
  metadata: {
    exportDate: string;
    version: string;
    count: number;
  };
  dungeons: DungeonExportEntry[];
}

// Library entry wraps DungeonData with an id and optional label
export interface DungeonLibraryEntry {
  id: string;
  label: string;
  seed: number;
  dungeon: import("../lib/rotjsDungeonGenerator").DungeonData;
  generatedAt: number;
}
