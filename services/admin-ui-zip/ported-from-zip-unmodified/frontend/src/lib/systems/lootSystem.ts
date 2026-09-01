/**
 * Loot System
 *
 * Handles loot drop spawning on mob death and pickup by player-like entities.
 * Runs after CollisionSystem in the ECS tick (priority 25).
 *
 * - spawnLoot(position, mobType, mobEntityId): called from death callback
 * - pickupLoot(playerEntityId, lootEntityId): called from UI interaction
 * - Emits PICKUP_PROMPT_AVAILABLE event when player is within pickup range
 */

import {
  DEFAULT_TIER_WEIGHTS,
  type LootItemData,
  type LootTierName,
  MOB_LOOT_TIER_WEIGHTS,
  sampleTier,
} from "../../types/loot";
import type { Entity, System } from "../../types/runtime";
import { debugLogger } from "../debugLogger";
import { generateLootAttributes } from "../lootGenerator";
import { getRuntimeManager } from "../runtimeManager";

// ── Constants ──────────────────────────────────────────────────────────────────

/** One dungeon tile in world units (10:1 ratio). */
const PICKUP_RANGE = 12; // ~1 tile + small buffer

// ── State ─────────────────────────────────────────────────────────────────────

/** All active loot entity IDs in the world. */
const activeLootIds = new Set<string>();

/** Pickup prompt callbacks subscribed from the UI. */
type PromptCallback = (
  lootEntityId: string,
  playerEntityId: string,
  itemData: LootItemData,
) => void;
let onPickupPrompt: PromptCallback | null = null;
let onPromptCleared: (() => void) | null = null;

/** Currently shown prompt (to avoid spamming). */
let currentPromptLootId: string | null = null;

// ── Public API ─────────────────────────────────────────────────────────────────

export function setLootCallbacks(
  promptCb: PromptCallback,
  clearCb: () => void,
): void {
  onPickupPrompt = promptCb;
  onPromptCleared = clearCb;
}

/**
 * Spawn 1–3 loot entities at the given position when a mob dies.
 */
export function spawnLoot(
  position: { x: number; y: number; z: number },
  mobType: string,
  mobEntityId: string,
): void {
  const runtime = getRuntimeManager();
  const weights = MOB_LOOT_TIER_WEIGHTS[mobType] ?? DEFAULT_TIER_WEIGHTS;

  const dropCount = 1 + Math.floor(Math.random() * 3); // 1–3
  const droppedItems: LootItemData[] = [];

  for (let i = 0; i < dropCount; i++) {
    const tier: LootTierName = sampleTier(weights);
    const seed = Date.now() + i * 137;
    const generated = generateLootAttributes(tier, seed);

    const itemData: LootItemData = {
      id: generated.id,
      name: generated.name,
      tier: generated.tier,
      isExcellent: generated.isExcellent,
      attributes: generated.attributes.map((a) => ({
        category: a.category,
        name: a.name,
        value: a.value,
        description: a.description,
      })),
    };

    // Scatter loot items around the death position
    const scatter = 4;
    const lootX = position.x + (Math.random() - 0.5) * scatter;
    const lootZ = position.z + (Math.random() - 0.5) * scatter;

    const lootId = `loot-${mobEntityId}-${i}-${seed}`;
    const entity: Entity = {
      id: lootId,
      components: new Map(),
      active: true,
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };

    entity.components.set("Transform", {
      position: { x: lootX, y: 0, z: lootZ },
      rotation: { x: 0, y: 0, z: 0 },
      scale: { x: 2, y: 2, z: 2 },
    });

    entity.components.set("LootItem", {
      itemData,
      pickupable: true,
    });

    entity.components.set("CollisionBody", {
      width: 2,
      height: 2,
      depth: 2,
      previousPosition: { x: lootX, y: 0, z: lootZ },
    });

    runtime.addEntity(entity);
    activeLootIds.add(lootId);
    droppedItems.push(itemData);
  }

  debugLogger.success(
    "loot",
    `Loot dropped by ${mobType} (${mobEntityId}): ${droppedItems.length} item(s)`,
    {
      mobEntityId,
      mobType,
      position,
      items: droppedItems.map((d) => ({ name: d.name, tier: d.tier })),
    },
  );
}

/**
 * Attempt to pick up a loot entity for the given player.
 * Returns true if successful.
 */
export function pickupLoot(
  playerEntityId: string,
  lootEntityId: string,
): boolean {
  const runtime = getRuntimeManager();
  const player = runtime.getEntity(playerEntityId);
  const lootEntity = runtime.getEntity(lootEntityId);

  if (!player || !lootEntity || !lootEntity.active) return false;

  const lootItem = lootEntity.components.get("LootItem") as
    | { itemData: LootItemData; pickupable: boolean }
    | undefined;
  if (!lootItem?.pickupable) return false;

  // Validate proximity
  const playerTransform = player.components.get("Transform") as
    | { position: { x: number; y: number; z: number } }
    | undefined;
  const lootTransform = lootEntity.components.get("Transform") as
    | { position: { x: number; y: number; z: number } }
    | undefined;
  if (playerTransform && lootTransform) {
    const dx = playerTransform.position.x - lootTransform.position.x;
    const dz = playerTransform.position.z - lootTransform.position.z;
    const dist = Math.sqrt(dx * dx + dz * dz);
    if (dist > PICKUP_RANGE * 2) return false; // too far
  }

  // Add to player inventory
  const inventory = player.components.get("Inventory") as
    | { lootItems?: LootItemData[]; capacity?: number; maxSlots?: number }
    | undefined;
  if (inventory) {
    if (!inventory.lootItems) inventory.lootItems = [];
    inventory.lootItems.push(lootItem.itemData);
  } else {
    player.components.set("Inventory", {
      capacity: 20,
      maxSlots: 20,
      items: [],
      lootItems: [lootItem.itemData],
    });
  }

  // Remove loot entity
  lootEntity.active = false;
  runtime.destroyEntity(lootEntityId);
  activeLootIds.delete(lootEntityId);

  if (currentPromptLootId === lootEntityId) {
    currentPromptLootId = null;
    onPromptCleared?.();
  }

  const inv = player.components.get("Inventory") as
    | { lootItems?: LootItemData[]; maxSlots?: number }
    | undefined;
  const remaining = (inv?.maxSlots ?? 20) - (inv?.lootItems?.length ?? 0);

  debugLogger.info(
    "loot",
    `Picked up: ${lootItem.itemData.name} (${lootItem.itemData.tier})`,
    {
      playerEntityId,
      itemName: lootItem.itemData.tier,
      tier: lootItem.itemData.tier,
      remainingSlots: remaining,
    },
  );

  return true;
}

/** Returns a copy of currently active loot entity IDs. */
export function getActiveLootIds(): string[] {
  return [...activeLootIds];
}

/** Remove a loot entity ID from tracking (e.g. after scene reset). */
export function clearLootTracking(): void {
  activeLootIds.clear();
  currentPromptLootId = null;
}

// ── ECS System ─────────────────────────────────────────────────────────────────

export const LootSystem: System = {
  id: "loot-system",
  name: "Loot System",
  priority: 25, // after CollisionSystem (20) and CombatSystem (15)
  requiredComponents: ["Transform"],
  enabled: true,

  execute(entities: Entity[], _deltaTime: number): void {
    const runtime = getRuntimeManager();

    // Collect loot entities and player-like entities with inventory
    const lootEntities: Entity[] = [];
    const playerEntities: Entity[] = [];

    for (const e of entities) {
      if (!e.active) continue;
      if (e.components.has("LootItem")) {
        lootEntities.push(e);
      } else if (e.components.has("Inventory")) {
        playerEntities.push(e);
      }
    }

    // Sync activeLootIds with reality
    for (const id of [...activeLootIds]) {
      const e = runtime.getEntity(id);
      if (!e || !e.active) activeLootIds.delete(id);
    }

    if (lootEntities.length === 0 || playerEntities.length === 0) {
      if (currentPromptLootId !== null) {
        currentPromptLootId = null;
        onPromptCleared?.();
      }
      return;
    }

    // Check each player against each loot item
    let foundPrompt = false;
    for (const player of playerEntities) {
      const pTransform = player.components.get("Transform") as
        | { position: { x: number; y: number; z: number } }
        | undefined;
      if (!pTransform) continue;

      for (const loot of lootEntities) {
        const lTransform = loot.components.get("Transform") as
          | { position: { x: number; y: number; z: number } }
          | undefined;
        if (!lTransform) continue;

        const dx = pTransform.position.x - lTransform.position.x;
        const dz = pTransform.position.z - lTransform.position.z;
        const dist = Math.sqrt(dx * dx + dz * dz);

        if (dist <= PICKUP_RANGE) {
          const lootItem = loot.components.get("LootItem") as
            | { itemData: LootItemData; pickupable: boolean }
            | undefined;
          if (lootItem?.pickupable && loot.id !== currentPromptLootId) {
            currentPromptLootId = loot.id;
            onPickupPrompt?.(loot.id, player.id, lootItem.itemData);
          }
          foundPrompt = true;
          break;
        }
      }
      if (foundPrompt) break;
    }

    if (!foundPrompt && currentPromptLootId !== null) {
      currentPromptLootId = null;
      onPromptCleared?.();
    }
  },
};
