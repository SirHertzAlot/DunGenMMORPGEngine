import { useEffect, useRef } from "react";
import type { DungeonData } from "../lib/rotjsDungeonGenerator";

interface Dungeon2DPreviewProps {
  dungeonData: DungeonData;
  className?: string;
}

const CANVAS_COLORS = {
  wall: "#2a2d38",
  floor: "#d4c9a0",
  corridor: "#b09a6e",
  door: "#8b5e3c",
  boss_floor: "#7a1a1a",
  spawnEntrance: "#ef4444",
  spawnBoss: "#f59e0b",
};

export default function Dungeon2DPreview({
  dungeonData,
  className = "",
}: Dungeon2DPreviewProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const { width, height, cells, spawnPoints } = dungeonData;

    const maxCanvasWidth = canvas.clientWidth || 800;
    const maxCanvasHeight = canvas.clientHeight || 400;
    const cellSize = Math.min(
      Math.floor(maxCanvasWidth / width),
      Math.floor(maxCanvasHeight / height),
      12,
    );

    canvas.width = width * cellSize;
    canvas.height = height * cellSize;

    ctx.fillStyle = "#000000";
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const cellType = cells[y][x];

        switch (cellType) {
          case 0:
            ctx.fillStyle = CANVAS_COLORS.wall;
            break;
          case 1:
            ctx.fillStyle = CANVAS_COLORS.floor;
            break;
          case 2:
            ctx.fillStyle = CANVAS_COLORS.corridor;
            break;
          case 3:
            ctx.fillStyle = CANVAS_COLORS.door;
            break;
          case 4:
            ctx.fillStyle = CANVAS_COLORS.boss_floor;
            break;
          default:
            ctx.fillStyle = "#000000";
        }

        ctx.fillRect(x * cellSize, y * cellSize, cellSize, cellSize);

        if (cellType !== 0) {
          ctx.strokeStyle = "rgba(0, 0, 0, 0.1)";
          ctx.lineWidth = 1;
          ctx.strokeRect(x * cellSize, y * cellSize, cellSize, cellSize);
        }
      }
    }

    // Draw spawn point markers
    if (spawnPoints && spawnPoints.length > 0) {
      for (const spawn of spawnPoints) {
        const { x, y } = spawn.position;
        const cx = x * cellSize + cellSize / 2;
        const cy = y * cellSize + cellSize / 2;
        const radius = Math.max(cellSize * 0.7, 3);

        const markerColor =
          spawn.type === "dungeon_entrance"
            ? CANVAS_COLORS.spawnEntrance
            : CANVAS_COLORS.spawnBoss;

        // Outer ring
        ctx.beginPath();
        ctx.arc(cx, cy, radius + 1.5, 0, Math.PI * 2);
        ctx.fillStyle = "rgba(255,255,255,0.85)";
        ctx.fill();

        // Colored fill
        ctx.beginPath();
        ctx.arc(cx, cy, radius, 0, Math.PI * 2);
        ctx.fillStyle = markerColor;
        ctx.fill();

        // Inner dot
        ctx.beginPath();
        ctx.arc(cx, cy, radius * 0.35, 0, Math.PI * 2);
        ctx.fillStyle = "rgba(255,255,255,0.9)";
        ctx.fill();
      }
    }
  }, [dungeonData]);

  return (
    <div className={`flex flex-col gap-2 ${className}`}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold">2D Dungeon Layout</h3>
        <div className="flex flex-wrap gap-3 text-xs">
          <div className="flex items-center gap-1">
            <div
              className="h-3 w-3 rounded-sm"
              style={{ backgroundColor: CANVAS_COLORS.floor }}
            />
            <span>Room</span>
          </div>
          <div className="flex items-center gap-1">
            <div
              className="h-3 w-3 rounded-sm"
              style={{ backgroundColor: CANVAS_COLORS.corridor }}
            />
            <span>Corridor</span>
          </div>
          <div className="flex items-center gap-1">
            <div
              className="h-3 w-3 rounded-sm"
              style={{ backgroundColor: CANVAS_COLORS.door }}
            />
            <span>Door</span>
          </div>
          <div className="flex items-center gap-1">
            <div
              className="h-3 w-3 rounded-sm"
              style={{ backgroundColor: CANVAS_COLORS.boss_floor }}
            />
            <span>Boss Room</span>
          </div>
          <div className="flex items-center gap-1">
            <div
              className="h-3 w-3 rounded-full"
              style={{ backgroundColor: CANVAS_COLORS.spawnEntrance }}
            />
            <span>Spawn</span>
          </div>
          <div className="flex items-center gap-1">
            <div
              className="h-3 w-3 rounded-full"
              style={{ backgroundColor: CANVAS_COLORS.spawnBoss }}
            />
            <span>Boss Entry</span>
          </div>
        </div>
      </div>
      <div className="flex items-center justify-center overflow-hidden rounded-lg border border-border bg-black p-4">
        <canvas
          ref={canvasRef}
          className="max-h-[400px] max-w-full"
          style={{ imageRendering: "pixelated" }}
        />
      </div>
      <p className="text-xs text-muted-foreground">
        Dimensions: {dungeonData.width}×{dungeonData.height} cells | Rooms:{" "}
        {dungeonData.rooms.length} |{" "}
        {dungeonData.bossRoom ? (
          <span className="text-amber-500">
            Boss Room at ({dungeonData.bossRoom.x}, {dungeonData.bossRoom.y})
          </span>
        ) : (
          <span className="text-muted-foreground">No boss room</span>
        )}{" "}
        | Spawn points: {dungeonData.spawnPoints.length}
      </p>
    </div>
  );
}
