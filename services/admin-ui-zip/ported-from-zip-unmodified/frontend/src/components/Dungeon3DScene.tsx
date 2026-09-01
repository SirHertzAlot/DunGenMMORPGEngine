/**
 * Dungeon3DScene
 *
 * Full 3D dungeon scene with:
 * - OrbitControls (orbit, pan, zoom)
 * - Focus Mode: cycles through active mobs with smooth camera transitions
 * - FPS / Orbit camera toggle
 * - Click/tap-to-select mob targeting with MobInspectionPanel
 * - MobSpawningPanel debug UI (spawn, pause, clear)
 * - FocusModeHUD overlay
 * - RuntimeLoop: drives ECS systems from inside R3F useFrame (no setInterval)
 * - Auto-spawns mobs into the starting room when dungeonData changes
 * - LootSystem: mobs drop loot on death, pickup prompt on proximity
 */

import { OrbitControls } from "@react-three/drei";
import { Canvas, useFrame, useThree } from "@react-three/fiber";
import { useCallback, useEffect, useRef, useState } from "react";
import * as THREE from "three";

import type { RuntimeManager } from "../core/runtime/runtimeManager";
import { useDungeon3DRenderer } from "../hooks/useDungeon3DRenderer";
import { useFocusMode } from "../hooks/useFocusMode";
import { debugLogger } from "../lib/debugLogger";
import { mapSpawnPointTo3D } from "../lib/dungeonGridMapper";
import { createMobEntity, destroyAllMobs } from "../lib/mobFactory";
import type { MobTypeName } from "../lib/mobFactory";
import type { DungeonData } from "../lib/rotjsDungeonGenerator";
import { getRuntimeManager } from "../lib/runtimeManager";
import {
  CollisionSystem,
  setCollisionDungeonGrid,
} from "../lib/systems/collisionSystem";
import { CombatSystem, setCombatCallbacks } from "../lib/systems/combatSystem";
import {
  LootSystem,
  clearLootTracking,
  pickupLoot,
  setLootCallbacks,
  spawnLoot,
} from "../lib/systems/lootSystem";
import { setMasteryCallbacks } from "../lib/systems/masterySystem";
import {
  WanderSystem,
  setWanderDungeonGrid,
} from "../lib/systems/wanderSystem";
import type { EditTool } from "../pages/DungeonGeneratorPage";
import { CELL_SIZE } from "../types/dungeon3d";
import type { LootItemData } from "../types/loot";
import type {
  Entity,
  Health,
  Transform,
  WanderBehavior,
} from "../types/runtime";
import type { CameraMode } from "./DungeonToolbar";
import FocusModeHUD from "./FocusModeHUD";
import LootEntity from "./LootEntity";
import LootPickupPrompt from "./LootPickupPrompt";
import MobEntity from "./MobEntity";
import MobInspectionPanel from "./MobInspectionPanel";
import MobSpawningPanel from "./MobSpawningPanel";
import BossMarker from "./primitives/BossMarker";
import FloorCube from "./primitives/FloorCube";
import SpawnMarker from "./primitives/SpawnMarker";
import WallCube from "./primitives/WallCube";

// ─── Types ────────────────────────────────────────────────────────────────────

interface Dungeon3DSceneProps {
  dungeonData: DungeonData;
  className?: string;
  editMode?: boolean;
  activeTool?: EditTool;
  onCellToggle?: (gridX: number, gridY: number) => void;
  onPlaceSpawnPoint?: (roomIndex: number) => void;
  onPlaceBossRoom?: (roomIndex: number) => void;
  cameraMode?: CameraMode;
  onCameraModeChange?: (mode: CameraMode) => void;
  externalFocusMode?: {
    isActive: boolean;
    currentIndex: number;
    focusedMobId: string | null;
    onNext: () => void;
    onPrevious: () => void;
    onExit: () => void;
  };
  onMobIdsChange?: (ids: string[]) => void;
  onInventoryChange?: (items: LootItemData[]) => void;
}

type InternalCameraMode = "fps" | "third-person";

interface HoverState {
  gridX: number;
  gridY: number;
  roomIndex: number;
}

interface MobSnapshot {
  id: string;
  transform: Transform;
  health: Health;
}

interface LootSnapshot {
  id: string;
  position: { x: number; y: number; z: number };
  itemData: LootItemData;
}

interface OrbitControlsLike {
  enabled: boolean;
  target: THREE.Vector3;
  update: () => void;
}

// ─── Constants ────────────────────────────────────────────────────────────────

/** Stable ID for the persistent player inventory entity. */
const PLAYER_ENTITY_ID = "player-entity-0";

// ─── Helpers ──────────────────────────────────────────────────────────────────

function findRoomIndex(
  gridX: number,
  gridY: number,
  rooms: DungeonData["rooms"],
): number {
  for (let i = 0; i < rooms.length; i++) {
    const r = rooms[i];
    if (
      gridX >= r.x &&
      gridX < r.x + r.width &&
      gridY >= r.y &&
      gridY < r.y + r.height
    ) {
      return i;
    }
  }
  return -1;
}

/** Ensure the player inventory entity exists in the runtime. */
function ensurePlayerEntity(runtime: RuntimeManager): void {
  if (runtime.getEntity(PLAYER_ENTITY_ID)) return;
  const player: Entity = {
    id: PLAYER_ENTITY_ID,
    components: new Map(),
    active: true,
    createdAt: Date.now(),
    updatedAt: Date.now(),
  };
  player.components.set("Transform", {
    position: { x: 0, y: 0, z: 0 },
    rotation: { x: 0, y: 0, z: 0 },
    scale: { x: 1, y: 1, z: 1 },
  });
  player.components.set("Inventory", {
    capacity: 20,
    maxSlots: 20,
    items: [],
    lootItems: [],
  });
  runtime.addEntity(player);
}

// ─── RuntimeLoop: drives ECS from R3F useFrame ────────────────────────────────

function RuntimeLoop({ runtime }: { runtime: RuntimeManager }) {
  const lastTimeRef = useRef<number>(performance.now());

  useFrame(() => {
    const now = performance.now();
    const delta = now - lastTimeRef.current;
    lastTimeRef.current = now;

    const entities = runtime.getEntities();
    const systems = runtime.getSystems();

    for (const system of systems) {
      if (!system.enabled) continue;
      const eligible = entities.filter(
        (e) =>
          e.active &&
          system.requiredComponents.every((c) => e.components.has(c)),
      );
      if (eligible.length > 0) {
        system.execute(eligible, delta);
      }
    }
  });

  return null;
}

// ─── Camera Controller ────────────────────────────────────────────────────────

const CHASE_DISTANCE = 30;
const CHASE_HEIGHT = 18;
const CHASE_LERP = 0.1;
const LOOKAT_LERP = 0.12;

interface CameraControllerProps {
  focusedMobId: string | null;
  runtime: RuntimeManager;
  cameraMode: InternalCameraMode;
  autoFollow: boolean;
  orbitControlsRef: React.RefObject<OrbitControlsLike | null>;
}

function CameraController({
  focusedMobId,
  runtime,
  cameraMode,
  autoFollow,
  orbitControlsRef,
}: CameraControllerProps) {
  const { camera } = useThree();
  const prevMobPosRef = useRef<THREE.Vector3 | null>(null);
  const smoothAzimuthRef = useRef<number>(0);
  const initializedRef = useRef<string | null>(null);
  const smoothLookAtRef = useRef<THREE.Vector3>(new THREE.Vector3());

  useFrame((_, delta) => {
    if (!focusedMobId) return;

    const entity = runtime.getEntity(focusedMobId);
    if (!entity) return;

    const transform = entity.components.get("Transform") as
      | Transform
      | undefined;
    if (!transform) return;

    const mobPos = new THREE.Vector3(
      transform.position.x,
      transform.position.y,
      transform.position.z,
    );

    let moveDirAngle: number | null = null;
    if (prevMobPosRef.current) {
      const dx = mobPos.x - prevMobPosRef.current.x;
      const dz = mobPos.z - prevMobPosRef.current.z;
      if (Math.abs(dx) > 0.05 || Math.abs(dz) > 0.05) {
        moveDirAngle = Math.atan2(dx, dz);
      }
    }
    prevMobPosRef.current = mobPos.clone();

    if (initializedRef.current !== focusedMobId) {
      initializedRef.current = focusedMobId;
      const dir = new THREE.Vector3()
        .subVectors(camera.position, mobPos)
        .normalize();
      smoothAzimuthRef.current = Math.atan2(dir.x, dir.z);
      smoothLookAtRef.current.copy(mobPos);
    }

    const orbit = orbitControlsRef.current;

    if (cameraMode === "fps") {
      if (orbit) orbit.enabled = false;

      const wander = entity.components.get("WanderBehavior") as
        | WanderBehavior
        | undefined;
      let facingAngle = smoothAzimuthRef.current;

      if (autoFollow) {
        if (moveDirAngle !== null) {
          let diff = moveDirAngle - smoothAzimuthRef.current;
          while (diff > Math.PI) diff -= 2 * Math.PI;
          while (diff < -Math.PI) diff += 2 * Math.PI;
          smoothAzimuthRef.current += diff * Math.min(delta * 4, 1);
          facingAngle = smoothAzimuthRef.current;
        }
      } else if (wander?.targetPosition) {
        const dx = wander.targetPosition.x - transform.position.x;
        const dz = wander.targetPosition.z - transform.position.z;
        if (Math.abs(dx) > 0.1 || Math.abs(dz) > 0.1) {
          const target = Math.atan2(dx, dz);
          let diff = target - smoothAzimuthRef.current;
          while (diff > Math.PI) diff -= 2 * Math.PI;
          while (diff < -Math.PI) diff += 2 * Math.PI;
          smoothAzimuthRef.current += diff * Math.min(delta * 3, 1);
          facingAngle = smoothAzimuthRef.current;
        }
      }

      const eyeHeight = 2.5;
      const targetCamPos = new THREE.Vector3(
        mobPos.x,
        mobPos.y + eyeHeight,
        mobPos.z,
      );
      const lookDist = 15;
      const lookAt = new THREE.Vector3(
        mobPos.x + Math.sin(facingAngle) * lookDist,
        mobPos.y + eyeHeight,
        mobPos.z + Math.cos(facingAngle) * lookDist,
      );
      camera.position.lerp(targetCamPos, Math.min(delta * 8, 1));
      camera.lookAt(lookAt);
    } else {
      if (orbit) orbit.enabled = false;

      if (moveDirAngle !== null) {
        const behindAngle = moveDirAngle + Math.PI;
        let diff = behindAngle - smoothAzimuthRef.current;
        while (diff > Math.PI) diff -= 2 * Math.PI;
        while (diff < -Math.PI) diff += 2 * Math.PI;
        smoothAzimuthRef.current += diff * Math.min(delta * 3, 1);
      }

      const desiredCamPos = new THREE.Vector3(
        mobPos.x + Math.sin(smoothAzimuthRef.current) * CHASE_DISTANCE,
        mobPos.y + CHASE_HEIGHT,
        mobPos.z + Math.cos(smoothAzimuthRef.current) * CHASE_DISTANCE,
      );
      camera.position.lerp(desiredCamPos, CHASE_LERP);

      const lookAtTarget = new THREE.Vector3(mobPos.x, mobPos.y + 2, mobPos.z);
      smoothLookAtRef.current.lerp(lookAtTarget, LOOKAT_LERP);
      camera.lookAt(smoothLookAtRef.current);
    }
  });

  return null;
}

// ─── Edit Plane ───────────────────────────────────────────────────────────────

function EditPlane({
  dungeonData,
  editMode,
  activeTool,
  onCellToggle,
  onPlaceSpawnPoint,
  onPlaceBossRoom,
  onHover,
}: {
  dungeonData: DungeonData;
  editMode: boolean;
  activeTool: EditTool;
  onCellToggle?: (gx: number, gy: number) => void;
  onPlaceSpawnPoint?: (roomIndex: number) => void;
  onPlaceBossRoom?: (roomIndex: number) => void;
  onHover: (state: HoverState | null) => void;
}) {
  const planeSize =
    Math.max(dungeonData.width, dungeonData.height) * CELL_SIZE * 2;
  const centerX = (dungeonData.width * CELL_SIZE) / 2;
  const centerZ = (dungeonData.height * CELL_SIZE) / 2;

  const handlePointerMove = useCallback(
    (e: { stopPropagation: () => void; point: THREE.Vector3 }) => {
      if (!editMode) {
        onHover(null);
        return;
      }
      e.stopPropagation();
      const gridX = Math.floor(e.point.x / CELL_SIZE);
      const gridY = Math.floor(e.point.z / CELL_SIZE);
      if (
        gridX < 0 ||
        gridX >= dungeonData.width ||
        gridY < 0 ||
        gridY >= dungeonData.height
      ) {
        onHover(null);
        return;
      }
      const cellVal = dungeonData.cells[gridY]?.[gridX];
      if (cellVal === undefined) {
        onHover(null);
        return;
      }
      const roomIndex = findRoomIndex(gridX, gridY, dungeonData.rooms);
      onHover({ gridX, gridY, roomIndex });
    },
    [editMode, dungeonData, onHover],
  );

  const handlePointerLeave = useCallback(() => {
    onHover(null);
  }, [onHover]);

  const handleClick = useCallback(
    (e: { stopPropagation: () => void; point: THREE.Vector3 }) => {
      if (!editMode) return;
      e.stopPropagation();
      const gridX = Math.floor(e.point.x / CELL_SIZE);
      const gridY = Math.floor(e.point.z / CELL_SIZE);
      if (
        gridX < 0 ||
        gridX >= dungeonData.width ||
        gridY < 0 ||
        gridY >= dungeonData.height
      )
        return;
      const cellVal = dungeonData.cells[gridY]?.[gridX];
      if (cellVal === undefined) return;
      if (activeTool === "none") {
        if (cellVal !== 4) onCellToggle?.(gridX, gridY);
      } else if (activeTool === "spawnPoint" || activeTool === "bossRoom") {
        const roomIndex = findRoomIndex(gridX, gridY, dungeonData.rooms);
        if (roomIndex === -1) return;
        if (activeTool === "spawnPoint") onPlaceSpawnPoint?.(roomIndex);
        else onPlaceBossRoom?.(roomIndex);
      }
    },
    [
      editMode,
      activeTool,
      dungeonData,
      onCellToggle,
      onPlaceSpawnPoint,
      onPlaceBossRoom,
    ],
  );

  if (!editMode) return null;

  return (
    // biome-ignore lint/a11y/useKeyWithClickEvents: R3F mesh — not a DOM element, keyboard events are inapplicable
    <mesh
      position={[centerX, 0.5, centerZ]}
      rotation={[-Math.PI / 2, 0, 0]}
      onPointerMove={handlePointerMove as never}
      onPointerLeave={handlePointerLeave}
      onClick={handleClick as never}
    >
      <planeGeometry args={[planeSize, planeSize]} />
      <meshBasicMaterial transparent opacity={0} side={THREE.DoubleSide} />
    </mesh>
  );
}

// ─── Hover Highlight ──────────────────────────────────────────────────────────

function HoverHighlight({
  hoverState,
  activeTool,
  dungeonData,
}: {
  hoverState: HoverState | null;
  activeTool: EditTool;
  dungeonData: DungeonData;
}) {
  if (!hoverState) return null;
  const { gridX, gridY, roomIndex } = hoverState;
  const cellVal = dungeonData.cells[gridY]?.[gridX];
  if (cellVal === undefined) return null;

  if (
    (activeTool === "spawnPoint" || activeTool === "bossRoom") &&
    roomIndex !== -1
  ) {
    const room = dungeonData.rooms[roomIndex];
    const highlightColor = activeTool === "spawnPoint" ? "#ef4444" : "#f59e0b";
    const cells: React.ReactElement[] = [];
    for (let ry = room.y; ry < room.y + room.height; ry++) {
      for (let rx = room.x; rx < room.x + room.width; rx++) {
        cells.push(
          <mesh
            key={`h-${rx}-${ry}`}
            position={[rx * CELL_SIZE, 1, ry * CELL_SIZE]}
          >
            <boxGeometry args={[CELL_SIZE, 1, CELL_SIZE]} />
            <meshBasicMaterial
              color={highlightColor}
              transparent
              opacity={0.35}
            />
          </mesh>,
        );
      }
    }
    return <group>{cells}</group>;
  }

  const highlightColor = cellVal === 0 ? "#22c55e" : "#ef4444";
  return (
    <mesh position={[gridX * CELL_SIZE, 1, gridY * CELL_SIZE]}>
      <boxGeometry args={[CELL_SIZE, 1, CELL_SIZE]} />
      <meshBasicMaterial color={highlightColor} transparent opacity={0.5} />
    </mesh>
  );
}

// ─── Mob Layer: syncs ECS snapshots every frame ───────────────────────────────

function MobLayer({
  mobIds,
  runtime,
  onSnapshotsUpdate,
}: {
  mobIds: string[];
  runtime: RuntimeManager;
  onSnapshotsUpdate: (snapshots: MobSnapshot[]) => void;
}) {
  const snapshotsRef = useRef<MobSnapshot[]>([]);

  useFrame(() => {
    const next: MobSnapshot[] = [];
    for (const id of mobIds) {
      const entity = runtime.getEntity(id);
      if (!entity || !entity.active) continue;
      const transform = entity.components.get("Transform") as
        | Transform
        | undefined;
      const health = entity.components.get("Health") as Health | undefined;
      if (transform && health) {
        next.push({ id, transform, health });
      }
    }
    if (next.length !== snapshotsRef.current.length) {
      snapshotsRef.current = next;
      onSnapshotsUpdate([...next]);
    } else {
      snapshotsRef.current = next;
    }
  });

  return null;
}

// ─── OrbitControls Re-enabler ─────────────────────────────────────────────────

function OrbitReEnabler({
  orbitControlsRef,
}: {
  orbitControlsRef: React.RefObject<OrbitControlsLike | null>;
}) {
  useEffect(() => {
    const orbit = orbitControlsRef.current;
    if (orbit) orbit.enabled = true;
  });
  return null;
}

// ─── Loot Layer: syncs loot entity snapshots every frame ─────────────────────

function LootLayer({
  runtime,
  onLootSnapshotsUpdate,
}: {
  runtime: RuntimeManager;
  onLootSnapshotsUpdate: (snapshots: LootSnapshot[]) => void;
}) {
  const prevCountRef = useRef<number>(0);

  useFrame(() => {
    const lootEntities = runtime
      .getEntities()
      .filter(
        (e) =>
          e.active &&
          e.components.has("LootItem") &&
          e.components.has("Transform"),
      );
    const snapshots: LootSnapshot[] = lootEntities.map((e) => {
      const transform = e.components.get("Transform") as {
        position: { x: number; y: number; z: number };
      };
      const lootItem = e.components.get("LootItem") as {
        itemData: LootItemData;
      };
      return {
        id: e.id,
        position: transform.position,
        itemData: lootItem.itemData,
      };
    });
    if (snapshots.length !== prevCountRef.current) {
      prevCountRef.current = snapshots.length;
      onLootSnapshotsUpdate([...snapshots]);
    }
  });

  return null;
}

// ─── Scene Mesh (inside Canvas) ──────────────────────────────────────────────

function DungeonMesh({
  dungeonData,
  editMode,
  activeTool,
  onCellToggle,
  onPlaceSpawnPoint,
  onPlaceBossRoom,
  mobIds,
  mobSnapshots,
  onSnapshotsUpdate,
  lootSnapshots,
  onLootSnapshotsUpdate,
  nearbyLootId,
  runtime,
  selectedMobId,
  onMobClick,
  focusedMobId,
  isFocusActive,
  internalCameraMode,
  autoFollow,
  orbitControlsRef,
}: {
  dungeonData: DungeonData;
  editMode: boolean;
  activeTool: EditTool;
  onCellToggle?: (gx: number, gy: number) => void;
  onPlaceSpawnPoint?: (roomIndex: number) => void;
  onPlaceBossRoom?: (roomIndex: number) => void;
  mobIds: string[];
  mobSnapshots: MobSnapshot[];
  onSnapshotsUpdate: (s: MobSnapshot[]) => void;
  lootSnapshots: LootSnapshot[];
  onLootSnapshotsUpdate: (s: LootSnapshot[]) => void;
  nearbyLootId: string | null;
  runtime: RuntimeManager;
  selectedMobId: string | null;
  onMobClick: (id: string) => void;
  focusedMobId: string | null;
  isFocusActive: boolean;
  internalCameraMode: InternalCameraMode;
  autoFollow: boolean;
  orbitControlsRef: React.RefObject<OrbitControlsLike | null>;
}) {
  const { camera } = useThree();
  const renderData = useDungeon3DRenderer(dungeonData);
  const [hoverState, setHoverState] = useState<HoverState | null>(null);

  // biome-ignore lint/correctness/useExhaustiveDependencies: camera init — intentionally runs only when bounds change; camera ref is stable
  useEffect(() => {
    const { centerX, centerZ, maxX, maxZ } = renderData.bounds;
    const maxDim = Math.max(maxX, maxZ);
    const distance = maxDim * 0.8;
    camera.position.set(
      centerX + distance * 0.5,
      distance * 0.6,
      centerZ + distance * 0.5,
    );
    camera.lookAt(centerX, 0, centerZ);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [renderData.bounds]);

  const bossSpawn = dungeonData.spawnPoints.find(
    (s) => s.type === "boss_entrance",
  );
  const entranceSpawns = dungeonData.spawnPoints.filter(
    (s) => s.type === "dungeon_entrance",
  );

  return (
    <group>
      {renderData.floorCubes.map((cube) => (
        <FloorCube
          key={`floor-${cube.x}-${cube.z}`}
          position={[cube.x, cube.y, cube.z]}
          color={cube.color}
        />
      ))}

      {renderData.wallCubes.map((cube, index) => (
        <WallCube
          key={`wall-${cube.x}-${cube.z}-${index}`}
          position={[cube.x, cube.y, cube.z]}
          color={cube.color}
        />
      ))}

      {entranceSpawns.map((spawn) => {
        const pos3D = mapSpawnPointTo3D(spawn);
        return (
          <SpawnMarker
            key={spawn.name}
            position={[pos3D.x, pos3D.y, pos3D.z]}
            color="#ef4444"
            label={spawn.name}
          />
        );
      })}

      {bossSpawn &&
        (() => {
          const pos3D = mapSpawnPointTo3D(bossSpawn);
          return (
            <BossMarker
              key="boss-marker"
              position={[pos3D.x, pos3D.y, pos3D.z]}
            />
          );
        })()}

      {mobSnapshots.map((snap) => (
        <MobEntity
          key={snap.id}
          entityId={snap.id}
          runtime={runtime}
          onClick={onMobClick}
          isSelected={snap.id === selectedMobId}
        />
      ))}

      {lootSnapshots.map((snap) => (
        <LootEntity
          key={snap.id}
          entityId={snap.id}
          position={snap.position}
          itemData={snap.itemData}
          isNearby={snap.id === nearbyLootId}
        />
      ))}

      <MobLayer
        mobIds={mobIds}
        runtime={runtime}
        onSnapshotsUpdate={onSnapshotsUpdate}
      />
      <LootLayer
        runtime={runtime}
        onLootSnapshotsUpdate={onLootSnapshotsUpdate}
      />

      {/* Drive ECS systems from R3F loop — no separate setInterval needed */}
      <RuntimeLoop runtime={runtime} />

      {isFocusActive && focusedMobId ? (
        <CameraController
          focusedMobId={focusedMobId}
          runtime={runtime}
          cameraMode={internalCameraMode}
          autoFollow={autoFollow}
          orbitControlsRef={orbitControlsRef}
        />
      ) : (
        <OrbitReEnabler orbitControlsRef={orbitControlsRef} />
      )}

      <EditPlane
        dungeonData={dungeonData}
        editMode={editMode}
        activeTool={activeTool}
        onCellToggle={onCellToggle}
        onPlaceSpawnPoint={onPlaceSpawnPoint}
        onPlaceBossRoom={onPlaceBossRoom}
        onHover={setHoverState}
      />

      <HoverHighlight
        hoverState={hoverState}
        activeTool={activeTool}
        dungeonData={dungeonData}
      />
    </group>
  );
}

// ─── Mob counter for unique IDs ───────────────────────────────────────────────
let _autoSpawnCounter = 0;

// ─── Main Component ───────────────────────────────────────────────────────────

export default function Dungeon3DScene({
  dungeonData,
  className,
  editMode = false,
  activeTool = "none",
  onCellToggle,
  onPlaceSpawnPoint,
  onPlaceBossRoom,
  cameraMode: _externalCameraMode,
  onCameraModeChange: _onCameraModeChange,
  externalFocusMode,
  onMobIdsChange,
  onInventoryChange,
}: Dungeon3DSceneProps) {
  const runtime = useRef<RuntimeManager>(getRuntimeManager()).current;
  const orbitControlsRef = useRef<OrbitControlsLike | null>(null);

  const [mobIds, setMobIds] = useState<string[]>([]);
  const [mobSnapshots, setMobSnapshots] = useState<MobSnapshot[]>([]);
  const [lootSnapshots, setLootSnapshots] = useState<LootSnapshot[]>([]);
  const [selectedMobId, setSelectedMobId] = useState<string | null>(null);
  const [internalCameraMode, setInternalCameraMode] =
    useState<InternalCameraMode>("third-person");
  const [autoFollow, setAutoFollow] = useState(true);
  const [isPaused, setIsPaused] = useState(false);

  // Pickup prompt state
  const [promptLootId, setPromptLootId] = useState<string | null>(null);
  const [promptItemData, setPromptItemData] = useState<LootItemData | null>(
    null,
  );

  // Track which dungeon we've already spawned mobs for
  const spawnedKeyRef = useRef<string | null>(null);

  // Focus mode — driven by external or internal state
  const internalFocusMode = useFocusMode({ mobIds });
  const focusMode = externalFocusMode ?? {
    isActive: internalFocusMode.isActive,
    currentIndex: internalFocusMode.currentIndex,
    focusedMobId: internalFocusMode.focusedMobId,
    onNext: internalFocusMode.next,
    onPrevious: internalFocusMode.previous,
    onExit: internalFocusMode.deactivate,
  };

  // Register ECS systems once on mount
  // biome-ignore lint/correctness/useExhaustiveDependencies: intentional mount-only effect — runtime is a stable ref
  useEffect(() => {
    // Ensure the persistent player inventory entity exists
    ensurePlayerEntity(runtime);

    const systems = runtime.getSystems();
    if (systems.length === 0) {
      runtime.registerSystem(WanderSystem);
      runtime.registerSystem(CollisionSystem);
      runtime.registerSystem(CombatSystem);
      runtime.registerSystem(LootSystem);
    }

    // Wire loot prompt callbacks
    setLootCallbacks(
      (
        lootEntityId: string,
        _playerEntityId: string,
        itemData: LootItemData,
      ) => {
        setPromptLootId(lootEntityId);
        setPromptItemData(itemData);
      },
      () => {
        setPromptLootId(null);
        setPromptItemData(null);
      },
    );

    // Wire combat callbacks — death triggers loot spawn
    setCombatCallbacks(
      (_attackerId: string, _targetId: string, _damage: number) => {
        // Combat attack event — no UI action needed here
      },
      (mobId: string) => {
        // Spawn loot at the dead mob's position
        const deadEntity = runtime.getEntity(mobId);
        if (deadEntity) {
          const transform = deadEntity.components.get("Transform") as
            | { position: { x: number; y: number; z: number } }
            | undefined;
          const ai = deadEntity.components.get("AI") as
            | { mobType?: string; behaviorType?: string }
            | undefined;
          const mobMeta = deadEntity.components.get("MobMeta") as
            | { type?: string }
            | undefined;
          const mobType =
            mobMeta?.type ?? ai?.mobType ?? ai?.behaviorType ?? "goblin";
          const position = transform?.position ?? { x: 0, y: 0, z: 0 };
          spawnLoot(position, mobType, mobId);
        }
        setMobIds((prev) => prev.filter((id) => id !== mobId));
      },
    );

    // Wire mastery callbacks — log tier advancements and skill unlocks
    setMasteryCallbacks({
      onMasteryAdvanced: (entityId, oldTier, newTier, rollResult) => {
        debugLogger.info(
          "mastery",
          `Entity ${entityId} advanced from tier ${oldTier} to ${newTier} (roll: ${rollResult})`,
        );
      },
      onSkillsUnlocked: (entityId, skillIds) => {
        debugLogger.info(
          "mastery",
          `Entity ${entityId} unlocked skills: ${skillIds.join(", ")}`,
        );
      },
    });

    runtime.resume();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Update dungeon grid references whenever dungeonData changes
  useEffect(() => {
    setWanderDungeonGrid(dungeonData.cells);
    setCollisionDungeonGrid(dungeonData.cells);
  }, [dungeonData]);

  // ─── Auto-spawn mobs into the starting room when dungeonData changes ────────
  // biome-ignore lint/correctness/useExhaustiveDependencies: intentional — only re-run when dungeonData identity changes; other refs are stable
  useEffect(() => {
    // Build a stable key from dungeon identity
    const sp0 = dungeonData.spawnPoints?.find(
      (s) => s.type === "dungeon_entrance",
    );
    const dungeonKey = [
      dungeonData.width,
      dungeonData.height,
      sp0?.position?.x ?? "x",
      sp0?.position?.y ?? "y",
    ].join("-");

    // Skip if we already spawned for this exact dungeon
    if (spawnedKeyRef.current === dungeonKey) return;
    spawnedKeyRef.current = dungeonKey;

    // Remove all previously spawned mobs and clear loot tracking
    destroyAllMobs(runtime, mobIds);
    clearLootTracking();

    // Need a valid dungeon entrance spawn point
    const entranceSpawn = dungeonData.spawnPoints?.find(
      (s) => s.type === "dungeon_entrance",
    );
    if (!entranceSpawn) return;

    const spawnWorldX = entranceSpawn.position.x * CELL_SIZE;
    const spawnWorldZ = entranceSpawn.position.y * CELL_SIZE;

    // Spawn 3 mobs (2 goblins + 1 skeleton) near the starting room
    const mobDefs: Array<{ type: MobTypeName; dx: number; dz: number }> = [
      { type: "goblin", dx: 0, dz: 0 },
      { type: "goblin", dx: 15, dz: 10 },
      { type: "skeleton", dx: -15, dz: 10 },
    ];

    const newMobIds: string[] = [];

    for (const def of mobDefs) {
      const id = `mob-${def.type}-auto-${Date.now()}-${++_autoSpawnCounter}`;
      const entity = createMobEntity({
        id,
        type: def.type,
        x: spawnWorldX + def.dx,
        z: spawnWorldZ + def.dz,
      });
      runtime.addEntity(entity);
      newMobIds.push(id);
    }

    setMobIds(newMobIds);
    onMobIdsChange?.(newMobIds);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dungeonData]);

  // Notify parent of mob ID changes
  useEffect(() => {
    onMobIdsChange?.(mobIds);
  }, [mobIds, onMobIdsChange]);

  // Notify parent of inventory changes when loot snapshots change
  // biome-ignore lint/correctness/useExhaustiveDependencies: lootSnapshots is intentional trigger for inventory re-read
  useEffect(() => {
    if (!onInventoryChange) return;
    const playerEntity = runtime.getEntity(PLAYER_ENTITY_ID);
    if (!playerEntity) return;
    const inv = playerEntity.components.get("Inventory") as
      | { lootItems?: LootItemData[] }
      | undefined;
    onInventoryChange(inv?.lootItems ?? []);
  }, [lootSnapshots, onInventoryChange, runtime]);

  const handleMobClick = useCallback((id: string) => {
    setSelectedMobId((prev) => (prev === id ? null : id));
  }, []);

  const handleSpawned = useCallback(
    (entities: Entity[]) => {
      const newIds = entities.map((e) => e.id);
      setMobIds((prev) => {
        const combined = [...prev, ...newIds];
        onMobIdsChange?.(combined);
        return combined;
      });
    },
    [onMobIdsChange],
  );

  const handleClearMobs = useCallback(() => {
    destroyAllMobs(runtime, mobIds);
    setMobIds([]);
    setSelectedMobId(null);
    spawnedKeyRef.current = null; // allow re-spawn on next dungeonData change
    onMobIdsChange?.([]);
  }, [runtime, mobIds, onMobIdsChange]);

  const handleTogglePause = useCallback(() => {
    setIsPaused((prev) => {
      if (prev) runtime.resume();
      else runtime.pause();
      return !prev;
    });
  }, [runtime]);

  /** Handle picking up the currently prompted loot item. */
  const handlePickup = useCallback(() => {
    if (!promptLootId) return;
    const success = pickupLoot(PLAYER_ENTITY_ID, promptLootId);
    if (success) {
      setPromptLootId(null);
      setPromptItemData(null);
      // Notify parent of inventory update
      if (onInventoryChange) {
        const playerEntity = runtime.getEntity(PLAYER_ENTITY_ID);
        const inv = playerEntity?.components.get("Inventory") as
          | { lootItems?: LootItemData[] }
          | undefined;
        onInventoryChange(inv?.lootItems ?? []);
      }
    }
  }, [promptLootId, onInventoryChange, runtime]);

  const focusedMobId = focusMode.focusedMobId;
  const isFocusActive = focusMode.isActive;

  return (
    <div className={`relative w-full h-full ${className ?? ""}`}>
      {/* Focus Mode HUD */}
      <FocusModeHUD
        isActive={isFocusActive}
        currentIndex={focusMode.currentIndex}
        totalMobs={mobIds.length}
        focusedMobId={focusedMobId}
        onNext={focusMode.onNext}
        onPrevious={focusMode.onPrevious}
        onExit={focusMode.onExit}
        cameraMode={internalCameraMode}
        onToggleCameraMode={() =>
          setInternalCameraMode((prev) =>
            prev === "fps" ? "third-person" : "fps",
          )
        }
        autoFollow={autoFollow}
        onToggleAutoFollow={() => setAutoFollow((prev) => !prev)}
      />

      {/* Mob Inspection Panel */}
      {selectedMobId && (
        <MobInspectionPanel
          mobId={selectedMobId}
          runtime={runtime}
          onClose={() => setSelectedMobId(null)}
        />
      )}

      {/* Mob Spawning Panel — uses its own absolute positioning */}
      <MobSpawningPanel
        runtime={runtime}
        dungeonGrid={dungeonData.cells}
        activeMobCount={mobIds.length}
        isPaused={isPaused}
        onPauseToggle={handleTogglePause}
        onClearAll={handleClearMobs}
        onSpawned={handleSpawned}
      />

      {/* Loot Pickup Prompt */}
      <LootPickupPrompt
        lootEntityId={promptLootId}
        itemData={promptItemData}
        onPickup={handlePickup}
      />

      {/* 3D Canvas */}
      <Canvas
        shadows
        camera={{ position: [0, 80, 120], fov: 60, near: 0.1, far: 5000 }}
        className="w-full h-full"
      >
        <ambientLight intensity={0.4} />
        <directionalLight
          position={[100, 200, 100]}
          intensity={0.8}
          castShadow
          shadow-mapSize-width={2048}
          shadow-mapSize-height={2048}
        />
        <pointLight position={[0, 50, 0]} intensity={0.3} color="#ffffff" />

        <OrbitControls
          ref={orbitControlsRef as never}
          enablePan
          enableZoom
          enableRotate
          maxPolarAngle={Math.PI / 2.1}
        />

        <DungeonMesh
          dungeonData={dungeonData}
          editMode={editMode}
          activeTool={activeTool}
          onCellToggle={onCellToggle}
          onPlaceSpawnPoint={onPlaceSpawnPoint}
          onPlaceBossRoom={onPlaceBossRoom}
          mobIds={mobIds}
          mobSnapshots={mobSnapshots}
          onSnapshotsUpdate={setMobSnapshots}
          lootSnapshots={lootSnapshots}
          onLootSnapshotsUpdate={setLootSnapshots}
          nearbyLootId={promptLootId}
          runtime={runtime}
          selectedMobId={selectedMobId}
          onMobClick={handleMobClick}
          focusedMobId={focusedMobId}
          isFocusActive={isFocusActive}
          internalCameraMode={internalCameraMode}
          autoFollow={autoFollow}
          orbitControlsRef={orbitControlsRef}
        />
      </Canvas>
    </div>
  );
}
