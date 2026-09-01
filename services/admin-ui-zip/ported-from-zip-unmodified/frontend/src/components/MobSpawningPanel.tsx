/**
 * MobSpawningPanel
 *
 * Collapsible debug panel for mob spawning control.
 * Includes mob type selector, spawn count input, Spawn / Pause / Clear buttons.
 * Uses createMobEntity to create Map-based ECS entities and adds them to the runtime.
 */

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  ChevronDown,
  ChevronUp,
  Pause,
  Play,
  Plus,
  Swords,
  Trash2,
} from "lucide-react";
import React, { useState } from "react";
import type { RuntimeManager } from "../core/runtime/runtimeManager";
import { MOB_TYPES, createMobEntity } from "../lib/mobFactory";
import type { MobTypeName } from "../lib/mobFactory";
import type { Entity } from "../types/runtime";

interface MobSpawningPanelProps {
  runtime: RuntimeManager;
  dungeonGrid: number[][];
  activeMobCount: number;
  isPaused: boolean;
  onPauseToggle: () => void;
  onClearAll: () => void;
  onSpawned: (entities: Entity[]) => void;
}

function getRandomFloorPosition(
  grid: number[][],
): { x: number; z: number } | null {
  const walkable: { row: number; col: number }[] = [];
  for (let row = 0; row < grid.length; row++) {
    for (let col = 0; col < (grid[row]?.length ?? 0); col++) {
      const v = grid[row][col];
      if (v === 1 || v === 2 || v === 4) walkable.push({ row, col });
    }
  }
  if (walkable.length === 0) return null;
  const pick = walkable[Math.floor(Math.random() * walkable.length)];
  // Place at center of cell
  return { x: pick.col * 10 + 5, z: pick.row * 10 + 5 };
}

export default function MobSpawningPanel({
  runtime,
  dungeonGrid,
  activeMobCount,
  isPaused,
  onPauseToggle,
  onClearAll,
  onSpawned,
}: MobSpawningPanelProps) {
  const [selectedType, setSelectedType] = useState<MobTypeName>(MOB_TYPES[0]);
  const [spawnCount, setSpawnCount] = useState(1);
  const [collapsed, setCollapsed] = useState(false);

  const handleSpawn = () => {
    if (dungeonGrid.length === 0) {
      console.warn("[MobSpawningPanel] No dungeon grid available");
      return;
    }
    const count = Math.max(1, Math.min(20, spawnCount));
    const spawned: Entity[] = [];

    for (let i = 0; i < count; i++) {
      const pos = getRandomFloorPosition(dungeonGrid);
      if (!pos) {
        console.warn("[MobSpawningPanel] No walkable cells found");
        break;
      }
      const id = `mob-${selectedType}-${Date.now()}-${i}`;
      console.log(
        `[MobSpawningPanel] Spawning ${selectedType} at (${pos.x}, ${pos.z})`,
      );
      const entity = createMobEntity({
        id,
        type: selectedType,
        x: pos.x,
        z: pos.z,
      });
      runtime.addEntity(entity);
      spawned.push(entity);
    }

    if (spawned.length > 0) {
      onSpawned(spawned);
    }
  };

  return (
    <div className="absolute bottom-4 right-4 z-30 w-56">
      <div className="bg-black/80 backdrop-blur-sm border border-white/20 rounded-xl shadow-xl overflow-hidden">
        <button
          type="button"
          className="w-full flex items-center justify-between px-3 py-2 text-white hover:bg-white/5 transition-colors"
          onClick={() => setCollapsed((c) => !c)}
        >
          <div className="flex items-center gap-2">
            <Swords className="w-4 h-4 text-orange-400" />
            <span className="text-sm font-semibold text-orange-400">
              Mob Spawner
            </span>
            <span className="text-xs text-white/40 bg-white/10 rounded-full px-1.5 py-0.5">
              {activeMobCount}
            </span>
          </div>
          {collapsed ? (
            <ChevronUp className="w-3.5 h-3.5 text-white/40" />
          ) : (
            <ChevronDown className="w-3.5 h-3.5 text-white/40" />
          )}
        </button>

        {!collapsed && (
          <div className="px-3 pb-3 space-y-2.5 border-t border-white/10 pt-2.5">
            <div className="space-y-1">
              <label
                htmlFor="mob-type-select"
                className="text-xs text-white/50"
              >
                Mob Type
              </label>
              <Select
                value={selectedType}
                onValueChange={(v) => setSelectedType(v as MobTypeName)}
              >
                <SelectTrigger
                  id="mob-type-select"
                  className="h-8 bg-white/5 border-white/20 text-white text-xs"
                >
                  <SelectValue />
                </SelectTrigger>
                <SelectContent className="bg-gray-900 border-white/20 text-white">
                  {MOB_TYPES.map((type) => (
                    <SelectItem
                      key={type}
                      value={type}
                      className="text-xs capitalize hover:bg-white/10"
                    >
                      {type}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1">
              <label
                htmlFor="mob-spawn-count"
                className="text-xs text-white/50"
              >
                Count (1–20)
              </label>
              <Input
                id="mob-spawn-count"
                type="number"
                min={1}
                max={20}
                value={spawnCount}
                onChange={(e) =>
                  setSpawnCount(
                    Math.max(
                      1,
                      Math.min(20, Number.parseInt(e.target.value) || 1),
                    ),
                  )
                }
                className="h-8 bg-white/5 border-white/20 text-white text-xs"
              />
            </div>

            <Button
              onClick={handleSpawn}
              className="w-full h-8 bg-orange-600 hover:bg-orange-500 text-white text-xs font-semibold"
              disabled={dungeonGrid.length === 0}
            >
              <Plus className="w-3.5 h-3.5 mr-1" />
              Spawn {spawnCount} {selectedType}
            </Button>

            <div className="w-full h-px bg-white/10" />

            <Button
              onClick={onPauseToggle}
              variant="outline"
              className="w-full h-8 border-white/20 bg-white/5 text-white hover:bg-white/10 text-xs"
            >
              {isPaused ? (
                <>
                  <Play className="w-3.5 h-3.5 mr-1 text-green-400" />
                  Resume All
                </>
              ) : (
                <>
                  <Pause className="w-3.5 h-3.5 mr-1 text-yellow-400" />
                  Pause All
                </>
              )}
            </Button>

            <Button
              onClick={onClearAll}
              variant="outline"
              className="w-full h-8 border-red-500/40 bg-red-500/10 text-red-400 hover:bg-red-500/20 hover:text-red-300 text-xs"
              disabled={activeMobCount === 0}
            >
              <Trash2 className="w-3.5 h-3.5 mr-1" />
              Clear All
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
