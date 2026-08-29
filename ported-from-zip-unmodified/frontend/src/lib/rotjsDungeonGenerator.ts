// Custom implementation of dungeon generation inspired by rot.js Digger algorithm
// This implementation does not require the rot-js library

import type { SpawnPoint } from "../types/dungeon3d";
import { CELL_BOSS_FLOOR } from "../types/dungeon3d";

export interface DungeonGenerationParams {
  width: number;
  height: number;
  roomWidthMin: number;
  roomWidthMax: number;
  roomHeightMin: number;
  roomHeightMax: number;
  corridorLength: [number, number];
  dugPercentage: number;
  timeLimit: number;
  seed?: number;
}

export interface DungeonCell {
  x: number;
  y: number;
  type: "wall" | "floor" | "corridor" | "door" | "boss_floor";
}

export interface DungeonData {
  width: number;
  height: number;
  cells: number[][]; // 0 = wall, 1 = floor, 2 = corridor, 3 = door, 4 = boss_floor
  rooms: Array<{ x: number; y: number; width: number; height: number }>;
  bossRoom?: { x: number; y: number; width: number; height: number };
  spawnPoints: SpawnPoint[];
}

interface Room {
  x: number;
  y: number;
  width: number;
  height: number;
}

// Simple seeded random number generator
class SeededRandom {
  private seed: number;

  constructor(seed: number) {
    this.seed = seed;
  }

  next(): number {
    this.seed = (this.seed * 9301 + 49297) % 233280;
    return this.seed / 233280;
  }

  nextInt(min: number, max: number): number {
    return Math.floor(this.next() * (max - min + 1)) + min;
  }
}

export function generateDungeon(params: DungeonGenerationParams): DungeonData {
  const {
    width,
    height,
    roomWidthMin,
    roomWidthMax,
    roomHeightMin,
    roomHeightMax,
    corridorLength: _corridorLength,
    dugPercentage,
    seed,
  } = params;

  const rng = new SeededRandom(seed || Date.now());

  // Initialize grid with walls (0)
  const cells: number[][] = Array(height)
    .fill(null)
    .map(() => Array(width).fill(0));

  const rooms: Room[] = [];
  const totalCells = width * height;
  const targetDugCells = Math.floor(totalCells * dugPercentage);
  let dugCells = 0;

  // Helper function to check if a room fits
  const canPlaceRoom = (
    x: number,
    y: number,
    w: number,
    h: number,
  ): boolean => {
    if (x < 1 || y < 1 || x + w >= width - 1 || y + h >= height - 1) {
      return false;
    }

    // Check if area is clear (with 1-tile buffer)
    for (let dy = -1; dy <= h; dy++) {
      for (let dx = -1; dx <= w; dx++) {
        const checkX = x + dx;
        const checkY = y + dy;
        if (checkX >= 0 && checkX < width && checkY >= 0 && checkY < height) {
          if (cells[checkY][checkX] !== 0) {
            return false;
          }
        }
      }
    }
    return true;
  };

  // Helper function to dig a room
  const digRoom = (room: Room): void => {
    for (let y = room.y; y < room.y + room.height; y++) {
      for (let x = room.x; x < room.x + room.width; x++) {
        if (cells[y][x] === 0) {
          cells[y][x] = 1; // Floor
          dugCells++;
        }
      }
    }
  };

  // Helper function to dig a corridor
  const digCorridor = (
    x1: number,
    y1: number,
    x2: number,
    y2: number,
  ): void => {
    // L-shaped corridor
    const midX = rng.next() > 0.5 ? x2 : x1;

    // Horizontal segment
    const startX = Math.min(x1, midX);
    const endX = Math.max(x1, midX);
    for (let x = startX; x <= endX; x++) {
      if (x >= 0 && x < width && y1 >= 0 && y1 < height) {
        if (cells[y1][x] === 0) {
          cells[y1][x] = 2; // Corridor
          dugCells++;
        }
      }
    }

    // Vertical segment
    const startY = Math.min(y1, y2);
    const endY = Math.max(y1, y2);
    for (let y = startY; y <= endY; y++) {
      if (midX >= 0 && midX < width && y >= 0 && y < height) {
        if (cells[y][midX] === 0) {
          cells[y][midX] = 2; // Corridor
          dugCells++;
        }
      }
    }
  };

  // Generate rooms
  let attempts = 0;
  const maxAttempts = 500;

  while (dugCells < targetDugCells && attempts < maxAttempts) {
    attempts++;

    const roomWidth = rng.nextInt(roomWidthMin, roomWidthMax);
    const roomHeight = rng.nextInt(roomHeightMin, roomHeightMax);
    const roomX = rng.nextInt(1, width - roomWidth - 2);
    const roomY = rng.nextInt(1, height - roomHeight - 2);

    if (canPlaceRoom(roomX, roomY, roomWidth, roomHeight)) {
      const newRoom: Room = {
        x: roomX,
        y: roomY,
        width: roomWidth,
        height: roomHeight,
      };

      digRoom(newRoom);

      // Connect to previous room with corridor
      if (rooms.length > 0) {
        const prevRoom = rooms[rooms.length - 1];
        const prevCenterX = Math.floor(prevRoom.x + prevRoom.width / 2);
        const prevCenterY = Math.floor(prevRoom.y + prevRoom.height / 2);
        const newCenterX = Math.floor(newRoom.x + newRoom.width / 2);
        const newCenterY = Math.floor(newRoom.y + newRoom.height / 2);

        digCorridor(prevCenterX, prevCenterY, newCenterX, newCenterY);
      }

      rooms.push(newRoom);
    }
  }

  // Add doors at room entrances (where corridor meets room)
  for (let y = 1; y < height - 1; y++) {
    for (let x = 1; x < width - 1; x++) {
      if (cells[y][x] === 2) {
        // Check if adjacent to a room floor
        const neighbors = [
          cells[y - 1]?.[x] || 0,
          cells[y + 1]?.[x] || 0,
          cells[y]?.[x - 1] || 0,
          cells[y]?.[x + 1] || 0,
        ];
        if (neighbors.includes(1)) {
          // Adjacent to room floor
          cells[y][x] = 3; // Mark as door
        }
      }
    }
  }

  // ---- BOSS ROOM GENERATION ----
  // Find the furthest room from the first room (dungeon entrance)
  let bossRoom: Room | undefined;
  let bossRoomData:
    | { x: number; y: number; width: number; height: number }
    | undefined;

  if (rooms.length > 0) {
    const firstRoom = rooms[0];
    const firstCenterX = firstRoom.x + firstRoom.width / 2;
    const firstCenterY = firstRoom.y + firstRoom.height / 2;

    // Find the room furthest from the first room
    let _furthestRoom = rooms[rooms.length - 1];
    let maxDist = 0;
    for (const room of rooms) {
      const cx = room.x + room.width / 2;
      const cy = room.y + room.height / 2;
      const dist = Math.sqrt(
        (cx - firstCenterX) ** 2 + (cy - firstCenterY) ** 2,
      );
      if (dist > maxDist) {
        maxDist = dist;
        _furthestRoom = room;
      }
    }

    // Determine boss room size (larger than regular rooms)
    const bossW = Math.min(roomWidthMax + 4, width - 4);
    const bossH = Math.min(roomHeightMax + 4, height - 4);

    // Place boss room at the far end of the map from the dungeon entrance
    // We pick a corner/edge opposite to the first room
    const firstRoomCenterX = firstRoom.x + firstRoom.width / 2;
    const firstRoomCenterY = firstRoom.y + firstRoom.height / 2;

    // Determine which corner is furthest from the first room
    const corners = [
      { bx: 2, by: 2 },
      { bx: width - bossW - 2, by: 2 },
      { bx: 2, by: height - bossH - 2 },
      { bx: width - bossW - 2, by: height - bossH - 2 },
    ];

    let bestCorner = corners[0];
    let bestCornerDist = 0;
    for (const corner of corners) {
      const cx = corner.bx + bossW / 2;
      const cy = corner.by + bossH / 2;
      const dist = Math.sqrt(
        (cx - firstRoomCenterX) ** 2 + (cy - firstRoomCenterY) ** 2,
      );
      if (dist > bestCornerDist) {
        bestCornerDist = dist;
        bestCorner = corner;
      }
    }

    // Try to place boss room at the best corner, with a gap from regular dungeon
    // We need to ensure it doesn't overlap with any existing dungeon cells
    const bossX = bestCorner.bx;
    const bossY = bestCorner.by;

    // Check if boss room area is clear (with 2-tile buffer to ensure disconnection)
    let canPlace = true;
    for (let dy = -2; dy <= bossH + 1; dy++) {
      for (let dx = -2; dx <= bossW + 1; dx++) {
        const checkX = bossX + dx;
        const checkY = bossY + dy;
        if (checkX >= 0 && checkX < width && checkY >= 0 && checkY < height) {
          if (cells[checkY][checkX] !== 0) {
            canPlace = false;
            break;
          }
        }
      }
      if (!canPlace) break;
    }

    if (canPlace) {
      // Dig boss room with boss_floor cell type (4)
      for (let ry = bossY; ry < bossY + bossH; ry++) {
        for (let rx = bossX; rx < bossX + bossW; rx++) {
          if (rx >= 0 && rx < width && ry >= 0 && ry < height) {
            cells[ry][rx] = CELL_BOSS_FLOOR;
          }
        }
      }
      bossRoom = { x: bossX, y: bossY, width: bossW, height: bossH };
      bossRoomData = bossRoom;
    } else {
      // Fallback: try to find a clear area by scanning from the furthest corner
      // Try all four corners in order of distance
      const sortedCorners = [...corners].sort((a, b) => {
        const distA = Math.sqrt(
          (a.bx + bossW / 2 - firstRoomCenterX) ** 2 +
            (a.by + bossH / 2 - firstRoomCenterY) ** 2,
        );
        const distB = Math.sqrt(
          (b.bx + bossW / 2 - firstRoomCenterX) ** 2 +
            (b.by + bossH / 2 - firstRoomCenterY) ** 2,
        );
        return distB - distA;
      });

      for (const corner of sortedCorners) {
        let clear = true;
        for (let dy = -2; dy <= bossH + 1; dy++) {
          for (let dx = -2; dx <= bossW + 1; dx++) {
            const checkX = corner.bx + dx;
            const checkY = corner.by + dy;
            if (
              checkX >= 0 &&
              checkX < width &&
              checkY >= 0 &&
              checkY < height
            ) {
              if (cells[checkY][checkX] !== 0) {
                clear = false;
                break;
              }
            }
          }
          if (!clear) break;
        }

        if (clear) {
          for (let ry = corner.by; ry < corner.by + bossH; ry++) {
            for (let rx = corner.bx; rx < corner.bx + bossW; rx++) {
              if (rx >= 0 && rx < width && ry >= 0 && ry < height) {
                cells[ry][rx] = CELL_BOSS_FLOOR;
              }
            }
          }
          bossRoom = {
            x: corner.bx,
            y: corner.by,
            width: bossW,
            height: bossH,
          };
          bossRoomData = bossRoom;
          break;
        }
      }
    }
  }

  // ---- SPAWN POINTS ----
  const spawnPoints: SpawnPoint[] = [];

  // Spawn point 1: Dungeon entrance — center of the first room
  if (rooms.length > 0) {
    const firstRoom = rooms[0];
    spawnPoints.push({
      name: "Dungeon Entrance",
      position: {
        x: Math.floor(firstRoom.x + firstRoom.width / 2),
        y: Math.floor(firstRoom.y + firstRoom.height / 2),
      },
      type: "dungeon_entrance",
    });
  }

  // Spawn point 2: Boss room entrance — center of the boss room
  if (bossRoom) {
    spawnPoints.push({
      name: "Boss Room Entrance",
      position: {
        x: Math.floor(bossRoom.x + bossRoom.width / 2),
        y: Math.floor(bossRoom.y + bossRoom.height / 2),
      },
      type: "boss_entrance",
    });
  }

  return {
    width,
    height,
    cells,
    rooms,
    bossRoom: bossRoomData,
    spawnPoints,
  };
}

export function getDefaultDungeonParams(): DungeonGenerationParams {
  return {
    width: 80,
    height: 50,
    roomWidthMin: 5,
    roomWidthMax: 12,
    roomHeightMin: 5,
    roomHeightMax: 12,
    corridorLength: [3, 7],
    dugPercentage: 0.3,
    timeLimit: 5000,
    seed: Math.floor(Math.random() * 1000000),
  };
}
