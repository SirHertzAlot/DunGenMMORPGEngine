import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";
import { Progress } from "@/components/ui/progress";
import {
  AlertCircle,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  Eye,
  Layers,
  MapPin,
  Pencil,
  Skull,
  Zap,
} from "lucide-react";
import React, { useState, useCallback, useEffect } from "react";
import ActionButtonsHUD from "../components/ActionButtonsHUD";
import CharacterStatsPanel from "../components/CharacterStatsPanel";
import ChatLogHUD from "../components/ChatLogHUD";
import Dungeon3DScene from "../components/Dungeon3DScene";
import DungeonControlPanel from "../components/DungeonControlPanel";
import DungeonLibrary from "../components/DungeonLibrary";
import DungeonToolbar from "../components/DungeonToolbar";
import type { CameraMode } from "../components/DungeonToolbar";
import InteractionPrompt from "../components/InteractionPrompt";
import InventoryHUD from "../components/InventoryHUD";
import MinimapHUD from "../components/MinimapHUD";
import PlayerStatusHUD from "../components/PlayerStatusHUD";
import SkillHotbar from "../components/SkillHotbar";
import { useBatchDungeonGenerator } from "../hooks/useBatchDungeonGenerator";
import { useFocusMode } from "../hooks/useFocusMode";
import {
  type DungeonData,
  type DungeonGenerationParams,
  generateDungeon,
  getDefaultDungeonParams,
} from "../lib/rotjsDungeonGenerator";
import type { DungeonLibraryEntry } from "../types/dungeon3d";
import type { InteractableObject } from "../types/interaction";
import type { LootItemData } from "../types/loot";

export type EditTool = "none" | "spawnPoint" | "bossRoom";

const BATCH_SIZES = [
  { label: "Small", count: 5, description: "5 dungeons" },
  { label: "Medium", count: 25, description: "25 dungeons" },
  { label: "Large", count: 100, description: "100 dungeons" },
] as const;

function deepCloneDungeon(dungeon: DungeonData): DungeonData {
  return {
    ...dungeon,
    cells: dungeon.cells.map((row) => [...row]),
    rooms: dungeon.rooms.map((r) => ({ ...r })),
    bossRoom: dungeon.bossRoom ? { ...dungeon.bossRoom } : undefined,
    spawnPoints: dungeon.spawnPoints.map((sp) => ({
      ...sp,
      position: { ...sp.position },
    })),
  };
}

export default function DungeonGeneratorPage() {
  const [params, setParams] = useState<DungeonGenerationParams>(
    getDefaultDungeonParams(),
  );
  const [_baseDungeon, setBaseDungeon] = useState<DungeonData>(() =>
    generateDungeon(getDefaultDungeonParams()),
  );
  const [editedDungeon, setEditedDungeon] = useState<DungeonData>(() =>
    deepCloneDungeon(generateDungeon(getDefaultDungeonParams())),
  );
  const [editMode, setEditMode] = useState(false);
  const [activeTool, setActiveTool] = useState<EditTool>("none");
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [cameraMode, setCameraMode] = useState<CameraMode>("orbit");
  const [mobIds, setMobIds] = useState<string[]>([]);
  const [activePrompt, setActivePrompt] = useState<InteractableObject | null>(
    null,
  );
  const [inventoryItems, setInventoryItems] = useState<LootItemData[]>([]);
  const [inventoryOpen, setInventoryOpen] = useState(false);
  const [charPanelOpen, setCharPanelOpen] = useState(false);
  const [skillsOpen, setSkillsOpen] = useState(false);

  // Focus mode — requires mobIds
  const focusMode = useFocusMode({ mobIds });

  // Library state
  const [libraryEntries, setLibraryEntries] = useState<DungeonLibraryEntry[]>(
    [],
  );
  const [selectedLibraryId, setSelectedLibraryId] = useState<string | null>(
    null,
  );
  const [libraryOpen, setLibraryOpen] = useState(false);

  const { batchState, generateBatch, cancelBatch } = useBatchDungeonGenerator();

  // Keyboard listeners: C toggles Character panel, Escape closes it
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Skip if focus is on an input/textarea
      const tag = (e.target as HTMLElement)?.tagName?.toLowerCase();
      if (tag === "input" || tag === "textarea" || tag === "select") return;

      if (e.key === "c" || e.key === "C") {
        setCharPanelOpen((v) => !v);
      } else if (e.key === "Escape") {
        setCharPanelOpen(false);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  const handleParamChange = (
    param: keyof DungeonGenerationParams,
    value: number | [number, number],
  ) => {
    setParams((prev) => ({ ...prev, [param]: value }));
  };

  const handleRegenerate = () => {
    const newDungeon = generateDungeon(params);
    setBaseDungeon(newDungeon);
    setEditedDungeon(deepCloneDungeon(newDungeon));
    setHasUnsavedChanges(false);
    setEditMode(false);
    setActiveTool("none");
    setSelectedLibraryId(null);
  };

  const handleRandomizeSeed = () => {
    const newSeed = Math.floor(Math.random() * 1000000);
    setParams((prev) => ({ ...prev, seed: newSeed }));
  };

  const handleToggleEditMode = () => {
    setEditMode((prev) => {
      if (prev) setActiveTool("none");
      return !prev;
    });
  };

  const handleSelectTool = (tool: EditTool) => {
    if (!editMode) setEditMode(true);
    setActiveTool((prev) => (prev === tool ? "none" : tool));
  };

  const handleCellToggle = useCallback((gridX: number, gridY: number) => {
    setEditedDungeon((prev) => {
      const newCells = prev.cells.map((row) => [...row]);
      const current = newCells[gridY]?.[gridX];
      if (current === undefined) return prev;
      if (current === 0) {
        newCells[gridY][gridX] = 1;
      } else if (current === 1 || current === 2 || current === 3) {
        newCells[gridY][gridX] = 0;
      }
      return { ...prev, cells: newCells };
    });
    setHasUnsavedChanges(true);
  }, []);

  const handlePlaceSpawnPoint = useCallback((roomIndex: number) => {
    setEditedDungeon((prev) => {
      const room = prev.rooms[roomIndex];
      if (!room) return prev;
      const cx = Math.floor(room.x + room.width / 2);
      const cy = Math.floor(room.y + room.height / 2);
      const filtered = prev.spawnPoints.filter(
        (sp) =>
          sp.type !== "dungeon_entrance" ||
          !(sp.position.x === cx && sp.position.y === cy),
      );
      const newSpawn = {
        name: `Spawn ${filtered.filter((s) => s.type === "dungeon_entrance").length + 1}`,
        position: { x: cx, y: cy },
        type: "dungeon_entrance" as const,
      };
      return { ...prev, spawnPoints: [...filtered, newSpawn] };
    });
    setHasUnsavedChanges(true);
  }, []);

  const handlePlaceBossRoom = useCallback((roomIndex: number) => {
    setEditedDungeon((prev) => {
      const room = prev.rooms[roomIndex];
      if (!room) return prev;
      const cx = Math.floor(room.x + room.width / 2);
      const cy = Math.floor(room.y + room.height / 2);
      const filtered = prev.spawnPoints.filter(
        (sp) => sp.type !== "boss_entrance",
      );
      const newBossSpawn = {
        name: "Boss Room Entrance",
        position: { x: cx, y: cy },
        type: "boss_entrance" as const,
      };
      return { ...prev, spawnPoints: [...filtered, newBossSpawn] };
    });
    setHasUnsavedChanges(true);
  }, []);

  // Batch Generation
  const handleBatchGenerate = async (count: number) => {
    const newEntries = await generateBatch(count, params);
    setLibraryEntries((prev) => [...prev, ...newEntries]);
    setLibraryOpen(true);
  };

  // Library Callbacks
  const handleLibrarySelect = (entry: DungeonLibraryEntry) => {
    setSelectedLibraryId(entry.id);
    const cloned = deepCloneDungeon(entry.dungeon);
    setBaseDungeon(cloned);
    setEditedDungeon(deepCloneDungeon(cloned));
    setHasUnsavedChanges(false);
    setEditMode(false);
    setActiveTool("none");
  };

  const handleLibraryRemove = (id: string) => {
    setLibraryEntries((prev) => prev.filter((e) => e.id !== id));
    if (selectedLibraryId === id) setSelectedLibraryId(null);
  };

  const handleLibraryClearAll = () => {
    setLibraryEntries([]);
    setSelectedLibraryId(null);
  };

  const progressPercent =
    batchState.total > 0
      ? Math.round((batchState.progress / batchState.total) * 100)
      : 0;

  // Mob info for toolbar
  const mobInfoList = mobIds.map((id, idx) => ({
    id,
    type: "Mob",
    index: idx,
  }));

  // Toolbar handlers
  const handleCameraModeToggle = useCallback(() => {
    setCameraMode((prev) => (prev === "orbit" ? "fps" : "orbit"));
  }, []);

  const handleToggleFocusMode = useCallback(() => {
    focusMode.toggle();
  }, [focusMode]);

  const handlePrevMob = useCallback(() => {
    focusMode.previous();
    if (!focusMode.isActive) focusMode.activate();
  }, [focusMode]);

  const handleNextMob = useCallback(() => {
    focusMode.next();
    if (!focusMode.isActive) focusMode.activate();
  }, [focusMode]);

  // Demo: show a loot pickup prompt after 3 seconds
  // biome-ignore lint/correctness/useExhaustiveDependencies: intentional — resets demo prompt when dungeon changes
  useEffect(() => {
    const timer = setTimeout(() => {
      setActivePrompt({
        id: "loot-demo-1",
        name: "Iron Sword",
        interactionType: "pickup",
        position: { x: 0, y: 0, z: 0 },
        promptText: "Pick Up",
        isNearby: true,
      });
      setTimeout(() => setActivePrompt(null), 5000);
    }, 3000);
    return () => clearTimeout(timer);
  }, [editedDungeon]);

  const handleInteract = useCallback((_obj: InteractableObject) => {
    setActivePrompt(null);
  }, []);

  return (
    <div className="flex h-full w-full flex-col overflow-hidden">
      {/* Top Toolbar */}
      <DungeonToolbar
        cameraMode={cameraMode}
        onCameraModeToggle={handleCameraModeToggle}
        mobs={mobInfoList}
        currentMobIndex={focusMode.currentIndex}
        onPrevMob={handlePrevMob}
        onNextMob={handleNextMob}
        focusModeActive={focusMode.isActive}
        onToggleFocusMode={handleToggleFocusMode}
      />

      {/* Header */}
      <div className="border-b border-border bg-card px-4 py-2 sm:px-6">
        <div className="container mx-auto flex flex-wrap items-center justify-between gap-2">
          <div>
            <h1 className="text-lg font-bold tracking-tight sm:text-xl">
              Dungeon Generator
            </h1>
            <p className="text-xs text-muted-foreground">
              Generate procedural dungeons using the Digger algorithm
            </p>
          </div>
          <div className="flex items-center gap-2">
            {hasUnsavedChanges && (
              <Badge
                variant="outline"
                className="gap-1 border-amber-500 text-amber-500"
              >
                <AlertCircle className="h-3 w-3" />
                Unsaved
              </Badge>
            )}
            {libraryEntries.length > 0 && (
              <Badge variant="secondary" className="gap-1">
                <Layers className="h-3 w-3" />
                {libraryEntries.length} in library
              </Badge>
            )}
            <Button
              variant={libraryOpen ? "default" : "outline"}
              size="sm"
              className="gap-1.5"
              onClick={() => setLibraryOpen((v) => !v)}
            >
              <Layers className="h-3.5 w-3.5" />
              Library
              {libraryOpen ? (
                <ChevronRight className="h-3.5 w-3.5" />
              ) : (
                <ChevronLeft className="h-3.5 w-3.5" />
              )}
            </Button>
          </div>
        </div>
      </div>

      {/* Collapsible Settings Panel */}
      <Collapsible open={settingsOpen} onOpenChange={setSettingsOpen}>
        <div className="border-b border-border bg-card/60">
          <div className="container mx-auto px-4 sm:px-6">
            <div className="flex items-center justify-between py-1.5">
              <div className="flex items-center gap-3 flex-wrap">
                {/* Batch Generate */}
                <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
                  Batch:
                </span>
                {batchState.isGenerating ? (
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-muted-foreground flex items-center gap-1">
                      <Zap className="h-3 w-3 animate-pulse text-primary" />
                      {batchState.progress}/{batchState.total}
                    </span>
                    <Progress value={progressPercent} className="h-1.5 w-20" />
                    <Button
                      variant="outline"
                      size="sm"
                      className="h-6 text-xs"
                      onClick={cancelBatch}
                    >
                      Cancel
                    </Button>
                  </div>
                ) : (
                  <div className="flex gap-1.5">
                    {BATCH_SIZES.map(({ label, count }) => (
                      <Button
                        key={label}
                        variant="outline"
                        size="sm"
                        className="h-6 px-2 text-xs gap-1"
                        onClick={() => handleBatchGenerate(count)}
                      >
                        <Zap className="h-3 w-3 text-primary" />
                        {label}
                      </Button>
                    ))}
                  </div>
                )}

                {/* Editor Tools */}
                <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wide ml-2">
                  Edit:
                </span>
                <div className="flex gap-1.5">
                  <Button
                    size="sm"
                    variant={
                      editMode && activeTool === "none" ? "default" : "outline"
                    }
                    onClick={handleToggleEditMode}
                    className="h-6 px-2 text-xs gap-1"
                  >
                    {editMode ? (
                      <Eye className="h-3 w-3" />
                    ) : (
                      <Pencil className="h-3 w-3" />
                    )}
                    {editMode ? "View" : "Edit"}
                  </Button>
                  <Button
                    size="sm"
                    variant={
                      activeTool === "spawnPoint" ? "default" : "outline"
                    }
                    onClick={() => handleSelectTool("spawnPoint")}
                    className="h-6 px-2 text-xs gap-1"
                  >
                    <MapPin className="h-3 w-3 text-red-500" />
                    Spawn
                  </Button>
                  <Button
                    size="sm"
                    variant={activeTool === "bossRoom" ? "default" : "outline"}
                    onClick={() => handleSelectTool("bossRoom")}
                    className="h-6 px-2 text-xs gap-1"
                  >
                    <Skull className="h-3 w-3 text-amber-500" />
                    Boss
                  </Button>
                </div>
              </div>

              <CollapsibleTrigger asChild>
                <Button variant="ghost" size="sm" className="h-6 gap-1 text-xs">
                  Settings
                  {settingsOpen ? (
                    <ChevronUp className="h-3 w-3" />
                  ) : (
                    <ChevronDown className="h-3 w-3" />
                  )}
                </Button>
              </CollapsibleTrigger>
            </div>
          </div>

          <CollapsibleContent>
            <div className="border-t border-border bg-card">
              <div className="container mx-auto px-4 py-3 sm:px-6">
                <DungeonControlPanel
                  params={params}
                  onParamChange={handleParamChange}
                  onRegenerate={handleRegenerate}
                  onRandomizeSeed={handleRandomizeSeed}
                />
              </div>
            </div>
          </CollapsibleContent>
        </div>
      </Collapsible>

      {/* Main Content */}
      <div className="flex min-h-0 flex-1 overflow-hidden">
        {/* Visualization Area */}
        <div className="flex flex-1 flex-col min-h-0 overflow-hidden relative">
          {/* 3D Visualization */}
          <div
            className="flex-1 min-h-0 relative overflow-hidden"
            style={{ minHeight: "70vh" }}
          >
            <Dungeon3DScene
              dungeonData={editedDungeon}
              editMode={editMode}
              activeTool={activeTool}
              onCellToggle={handleCellToggle}
              onPlaceSpawnPoint={handlePlaceSpawnPoint}
              onPlaceBossRoom={handlePlaceBossRoom}
              cameraMode={cameraMode}
              onCameraModeChange={setCameraMode}
              externalFocusMode={{
                isActive: focusMode.isActive,
                currentIndex: focusMode.currentIndex,
                focusedMobId: focusMode.focusedMobId,
                onNext: focusMode.next,
                onPrevious: focusMode.previous,
                onExit: focusMode.deactivate,
              }}
              onMobIdsChange={setMobIds}
              onInventoryChange={setInventoryItems}
            />

            {/* Player Status HUD — top-left overlay, outside Canvas */}
            <PlayerStatusHUD
              focusedEntityId={
                focusMode.isActive ? focusMode.focusedMobId : null
              }
            />

            {/* Interaction Prompt */}
            <InteractionPrompt
              activePrompt={activePrompt}
              onInteract={handleInteract}
            />

            {/* Skill Hotbar — bottom-center */}
            <SkillHotbar playerMana={80} playerStamina={100} />

            {/* Action Buttons HUD — bottom-right circular buttons */}
            <ActionButtonsHUD
              onInventory={() => setInventoryOpen((v) => !v)}
              onCharacter={() => setCharPanelOpen((v) => !v)}
              onSkills={() => setSkillsOpen((v) => !v)}
              inventoryOpen={inventoryOpen}
              characterOpen={charPanelOpen}
              skillsOpen={skillsOpen}
            />

            {/* Inventory HUD — shown when open or items are present */}
            {(inventoryOpen || inventoryItems.length > 0) && (
              <InventoryHUD items={inventoryItems} />
            )}

            {/* Minimap — circular compass frame, top-right corner */}
            <MinimapHUD dungeonData={editedDungeon} />

            {/* Chat Log HUD — bottom-left corner, fixed overlay */}
            <ChatLogHUD />

            {/* Character Stats Panel — slides in from right edge */}
            <CharacterStatsPanel
              isOpen={charPanelOpen}
              onClose={() => setCharPanelOpen(false)}
              focusedEntityId={
                focusMode.isActive ? focusMode.focusedMobId : null
              }
            />
          </div>

        </div>

        {/* Library Panel */}
        {libraryOpen && (
          <div className="w-72 flex-shrink-0 border-l border-border bg-card xl:w-80">
            <DungeonLibrary
              entries={libraryEntries}
              selectedId={selectedLibraryId}
              onSelect={handleLibrarySelect}
              onRemove={handleLibraryRemove}
              onClearAll={handleLibraryClearAll}
            />
          </div>
        )}
      </div>
    </div>
  );
}
