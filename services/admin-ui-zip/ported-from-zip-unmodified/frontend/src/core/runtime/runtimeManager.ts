/**
 * Core RuntimeManager — self-contained, all types inlined to avoid circular imports.
 * Entity shape matches types/runtime.ts for cross-module compatibility.
 */

export type EntityId = string;
export type ComponentType = string;
export type SystemId = string;

export interface Entity {
  id: EntityId;
  components: Map<ComponentType, any>;
  active: boolean;
  createdAt: number;
  updatedAt: number;
  tags?: string[];
}

export interface System {
  id: SystemId;
  name: string;
  priority: number;
  requiredComponents: ComponentType[];
  execute: (entities: Entity[], deltaTime: number) => void;
  enabled: boolean;
}

export type RuntimeState = "idle" | "running" | "paused" | "stopped";

export interface RuntimeConfig {
  tickRate?: number;
  maxEntities?: number;
  debug?: boolean;
  enableDeterminism?: boolean;
  seed?: number;
}

export interface PerformanceMetrics {
  fps: number;
  tickTime: number;
  entityCount: number;
  activeSystemCount: number;
  memoryUsage: number;
  systemExecutionTimes: Map<SystemId, number>;
}

export interface GameEvent {
  id: string;
  type: string;
  timestamp: number;
  data: Record<string, unknown>;
}

export class RuntimeManager {
  private entities: Map<EntityId, Entity> = new Map();
  private systems: System[] = [];
  private _state: RuntimeState = "idle";
  private config: RuntimeConfig;
  private metrics: PerformanceMetrics;
  private eventListeners: Map<string, Set<(event: GameEvent) => void>> =
    new Map();
  private entityCounter = 0;
  private eventCounter = 0;

  constructor(config: RuntimeConfig = {}) {
    this.config = config;
    this.metrics = {
      fps: 0,
      tickTime: 0,
      entityCount: 0,
      activeSystemCount: 0,
      memoryUsage: 0,
      systemExecutionTimes: new Map(),
    };
    console.log("[RuntimeManager] Constructed");
  }

  // ── Entity Management ──────────────────────────────────────────────────────

  createEntity(id?: EntityId): Entity {
    const entityId = id ?? `entity_${Date.now()}_${++this.entityCounter}`;
    if (this.entities.has(entityId)) {
      return this.entities.get(entityId)!;
    }
    const now = Date.now();
    const entity: Entity = {
      id: entityId,
      components: new Map(),
      active: true,
      createdAt: now,
      updatedAt: now,
    };
    this.entities.set(entityId, entity);
    this.metrics.entityCount = this.entities.size;
    this.emitEvent({
      id: `evt_${++this.eventCounter}`,
      type: "entity:created",
      timestamp: now,
      data: { entityId },
    });
    return entity;
  }

  addEntity(entity: Entity): void {
    this.entities.set(entity.id, entity);
    this.metrics.entityCount = this.entities.size;
    if (this.config.debug) {
      console.log(
        "[RuntimeManager] addEntity:",
        entity.id,
        "| Total:",
        this.entities.size,
      );
    }
  }

  destroyEntity(entityId: EntityId): boolean {
    const existed = this.entities.has(entityId);
    this.entities.delete(entityId);
    this.metrics.entityCount = this.entities.size;
    if (existed) {
      this.emitEvent({
        id: `evt_${++this.eventCounter}`,
        type: "entity:destroyed",
        timestamp: Date.now(),
        data: { entityId },
      });
    }
    return existed;
  }

  removeEntity(entityId: EntityId): void {
    this.destroyEntity(entityId);
  }

  getEntity(entityId: EntityId): Entity | undefined {
    return this.entities.get(entityId);
  }

  getAllEntities(): Entity[] {
    return Array.from(this.entities.values());
  }

  getEntities(): Entity[] {
    return Array.from(this.entities.values());
  }

  clearEntities(): void {
    this.entities.clear();
    this.metrics.entityCount = 0;
  }

  // ── Component Management ───────────────────────────────────────────────────

  addComponent(
    entityId: EntityId,
    componentType: ComponentType,
    data: any,
  ): boolean {
    const entity = this.entities.get(entityId);
    if (!entity) return false;
    entity.components.set(componentType, data);
    entity.updatedAt = Date.now();
    return true;
  }

  removeComponent(entityId: EntityId, componentType: ComponentType): boolean {
    const entity = this.entities.get(entityId);
    if (!entity) return false;
    return entity.components.delete(componentType);
  }

  getComponent(entityId: EntityId, componentType: ComponentType): any {
    return this.entities.get(entityId)?.components.get(componentType);
  }

  hasComponent(entityId: EntityId, componentType: ComponentType): boolean {
    return this.entities.get(entityId)?.components.has(componentType) ?? false;
  }

  // ── System Management ──────────────────────────────────────────────────────

  registerSystem(system: System): void {
    if (this.systems.find((s) => s.id === system.id)) {
      console.warn("[RuntimeManager] System already registered:", system.id);
      return;
    }
    this.systems.push(system);
    this.systems.sort((a, b) => a.priority - b.priority);
    this.metrics.activeSystemCount = this.systems.length;
    console.log(
      "[RuntimeManager] Registered system:",
      system.id,
      "| Total:",
      this.systems.length,
    );
  }

  unregisterSystem(systemId: SystemId): boolean {
    const idx = this.systems.findIndex((s) => s.id === systemId);
    if (idx === -1) return false;
    this.systems.splice(idx, 1);
    this.metrics.activeSystemCount = this.systems.length;
    return true;
  }

  getSystem(systemId: SystemId): System | undefined {
    return this.systems.find((s) => s.id === systemId);
  }

  getAllSystems(): System[] {
    return [...this.systems];
  }

  getSystems(): System[] {
    return [...this.systems];
  }

  // ── Query ──────────────────────────────────────────────────────────────────

  query(requiredComponents: ComponentType[]): Entity[] {
    return Array.from(this.entities.values()).filter(
      (e) => e.active && requiredComponents.every((c) => e.components.has(c)),
    );
  }

  queryEntities(opts: {
    requiredComponents: ComponentType[];
    excludedComponents?: ComponentType[];
  }): Entity[] {
    return Array.from(this.entities.values()).filter((e) => {
      if (!e.active) return false;
      if (!opts.requiredComponents.every((c) => e.components.has(c)))
        return false;
      if (opts.excludedComponents?.some((c) => e.components.has(c)))
        return false;
      return true;
    });
  }

  // ── Runtime Control ────────────────────────────────────────────────────────

  start(): void {
    this._state = "running";
    console.log("[RuntimeManager] State -> running");
    this.emitEvent({
      id: `evt_${++this.eventCounter}`,
      type: "runtime:started",
      timestamp: Date.now(),
      data: {},
    });
  }

  pause(): void {
    this._state = "paused";
    console.log("[RuntimeManager] State -> paused");
    this.emitEvent({
      id: `evt_${++this.eventCounter}`,
      type: "runtime:paused",
      timestamp: Date.now(),
      data: {},
    });
  }

  resume(): void {
    this._state = "running";
    console.log("[RuntimeManager] State -> running (resumed)");
    this.emitEvent({
      id: `evt_${++this.eventCounter}`,
      type: "runtime:resumed",
      timestamp: Date.now(),
      data: {},
    });
  }

  stop(): void {
    this._state = "stopped";
    console.log("[RuntimeManager] State -> stopped");
    this.emitEvent({
      id: `evt_${++this.eventCounter}`,
      type: "runtime:stopped",
      timestamp: Date.now(),
      data: {},
    });
  }

  getState(): { isRunning: boolean; isPaused: boolean; state: RuntimeState } {
    return {
      isRunning: this._state === "running",
      isPaused: this._state === "paused",
      state: this._state,
    };
  }

  // ── Metrics ────────────────────────────────────────────────────────────────

  getMetrics(): PerformanceMetrics {
    return { ...this.metrics };
  }

  // ── Events ─────────────────────────────────────────────────────────────────

  addEventListener(
    eventType: string,
    callback: (event: GameEvent) => void,
  ): void {
    if (!this.eventListeners.has(eventType)) {
      this.eventListeners.set(eventType, new Set());
    }
    this.eventListeners.get(eventType)!.add(callback);
  }

  removeEventListener(
    eventType: string,
    callback: (event: GameEvent) => void,
  ): void {
    this.eventListeners.get(eventType)?.delete(callback);
  }

  private emitEvent(event: GameEvent): void {
    // Emit to specific listeners
    // biome-ignore lint/complexity/noForEach: existing hot-path pattern — refactor out of scope
    this.eventListeners.get(event.type)?.forEach((cb) => cb(event));
    // Emit to wildcard listeners
    // biome-ignore lint/complexity/noForEach: existing hot-path pattern — refactor out of scope
    this.eventListeners.get("*")?.forEach((cb) => cb(event));
  }

  // ── Manual Tick (called from R3F useFrame) ─────────────────────────────────

  tick(deltaTime: number): void {
    if (this._state !== "running") return;
    const entityList = Array.from(this.entities.values());
    for (const system of this.systems) {
      if (!system.enabled) continue;
      const eligible = entityList.filter(
        (e) =>
          e.active &&
          system.requiredComponents.every((c) => e.components.has(c)),
      );
      system.execute(eligible, deltaTime);
    }
  }
}

// ── Singleton ──────────────────────────────────────────────────────────────────

let _instance: RuntimeManager | null = null;

export function getRuntimeManager(config: RuntimeConfig = {}): RuntimeManager {
  if (!_instance) {
    _instance = new RuntimeManager(config);
    console.log("[getRuntimeManager] Created new RuntimeManager singleton");
  }
  return _instance;
}

export function resetRuntimeManager(): void {
  _instance = null;
  console.log("[getRuntimeManager] Singleton reset");
}
