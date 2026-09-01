import { Button } from "@/components/ui/button";
import { X } from "lucide-react";
import { memo, useEffect, useRef } from "react";
import type { DungeonLibraryEntry } from "../types/dungeon3d";

interface DungeonLibraryCardProps {
  entry: DungeonLibraryEntry;
  isSelected: boolean;
  onSelect: (entry: DungeonLibraryEntry) => void;
  onRemove: (id: string) => void;
}

const THUMBNAIL_SIZE = 120;

const CANVAS_COLORS = {
  wall: "#2a2d38",
  floor: "#d4c9a0",
  corridor: "#b09a6e",
  door: "#8b5e3c",
  boss_floor: "#7a1a1a",
  spawnEntrance: "#ef4444",
  spawnBoss: "#f59e0b",
};

function renderThumbnail(
  canvas: HTMLCanvasElement,
  entry: DungeonLibraryEntry,
) {
  const ctx = canvas.getContext("2d");
  if (!ctx) return;

  const { width, height, cells, spawnPoints } = entry.dungeon;
  const cellSize = Math.max(
    1,
    Math.floor(THUMBNAIL_SIZE / Math.max(width, height)),
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
    }
  }

  // Draw spawn markers (only if cellSize >= 2)
  if (cellSize >= 2 && spawnPoints) {
    for (const spawn of spawnPoints) {
      const { x, y } = spawn.position;
      const cx = x * cellSize + cellSize / 2;
      const cy = y * cellSize + cellSize / 2;
      const radius = Math.max(cellSize * 0.6, 1.5);
      ctx.beginPath();
      ctx.arc(cx, cy, radius, 0, Math.PI * 2);
      ctx.fillStyle =
        spawn.type === "dungeon_entrance"
          ? CANVAS_COLORS.spawnEntrance
          : CANVAS_COLORS.spawnBoss;
      ctx.fill();
    }
  }
}

const DungeonLibraryCard = memo(function DungeonLibraryCard({
  entry,
  isSelected,
  onSelect,
  onRemove,
}: DungeonLibraryCardProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    if (canvasRef.current) {
      renderThumbnail(canvasRef.current, entry);
    }
  }, [entry]);

  return (
    <button
      type="button"
      className={`group relative flex cursor-pointer flex-col gap-1.5 rounded-lg border p-2 text-left transition-all hover:border-primary/60 hover:bg-accent/40 w-full ${
        isSelected
          ? "border-primary bg-primary/10 ring-1 ring-primary/40"
          : "border-border bg-card"
      }`}
      onClick={() => onSelect(entry)}
      onKeyDown={(e) => e.key === "Enter" && onSelect(entry)}
      aria-label={`Select ${entry.label}`}
    >
      {/* Remove button */}
      <Button
        variant="ghost"
        size="icon"
        className="absolute right-1 top-1 z-10 h-5 w-5 opacity-0 transition-opacity group-hover:opacity-100"
        onClick={(e) => {
          e.stopPropagation();
          onRemove(entry.id);
        }}
        aria-label={`Remove ${entry.label}`}
      >
        <X className="h-3 w-3" />
      </Button>

      {/* Thumbnail */}
      <div className="flex items-center justify-center overflow-hidden rounded bg-black">
        <canvas
          ref={canvasRef}
          style={{
            imageRendering: "pixelated",
            maxWidth: THUMBNAIL_SIZE,
            maxHeight: THUMBNAIL_SIZE,
          }}
        />
      </div>

      {/* Metadata */}
      <div className="min-w-0">
        <p className="truncate text-xs font-semibold leading-tight">
          {entry.label}
        </p>
        <p className="truncate text-[10px] text-muted-foreground">
          {entry.dungeon.width}×{entry.dungeon.height}
        </p>
        <p className="truncate font-mono text-[10px] text-muted-foreground">
          seed: {entry.seed}
        </p>
      </div>
    </button>
  );
});

export default DungeonLibraryCard;
