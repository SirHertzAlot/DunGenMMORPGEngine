import type {
  DungeonExportEntry,
  DungeonLibraryEntry,
  DungeonLibraryExport,
} from "../types/dungeon3d";

export function exportDungeonLibrary(entries: DungeonLibraryEntry[]): void {
  const dungeons: DungeonExportEntry[] = entries.map((entry) => ({
    seed: entry.seed,
    width: entry.dungeon.width,
    height: entry.dungeon.height,
    grid: entry.dungeon.cells,
    rooms: entry.dungeon.rooms,
    spawnPoints: entry.dungeon.spawnPoints.map((sp) => ({
      x: sp.position.x,
      y: sp.position.y,
      type: sp.type,
      name: sp.name,
    })),
    bossRoom: entry.dungeon.bossRoom ?? null,
  }));

  const payload: DungeonLibraryExport = {
    metadata: {
      exportDate: new Date().toISOString(),
      version: "1.0",
      count: dungeons.length,
    },
    dungeons,
  };

  const json = JSON.stringify(payload, null, 2);
  const blob = new Blob([json], { type: "application/json" });
  const url = URL.createObjectURL(blob);

  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = "dungeon-library.json";
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);

  // Clean up object URL after a short delay
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
