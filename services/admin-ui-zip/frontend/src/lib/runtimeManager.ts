import { debugLogger } from './debugLogger';
import type {
  Entity,
  EntityId,
  ComponentType,
  ComponentData,
  System,
  SystemId,
  RuntimeState,
  CacheState,
  PerformanceMetrics,
  RuntimeConfig,
  EntityQuery,
  GameEvent,
  EventListener,
} from '../types/runtime';

/**
 * Production-grade ECS/DOTS Runtime Manager
 * Handles entity lifecycle, system execution, and cache synchronization
 */
class RuntimeManager {
  private state: RuntimeState;
  private cache: CacheState;
  private config: RuntimeConfig;
  private metrics: PerformanceMetrics;
  private eventListeners: Map<string, Set<EventListener>>;
  private animationFrameId: number | null = null;
  private lastTickTime: number = 0;
  private accumulator: number = 0;

  constructor(config: Partial<RuntimeConfig> = {}) {
    this.config = {
      tickRate: 60,
      maxEntities: 10000,
      enablePersistence: true,
      enableDeterminism: true,
      seed: Date.now(),
      ...config,
    };

    this.state = {
      entities: new Map(),
      systems: new Map(),
      tickRate: this.config.tickRate,
      currentTick: 0,
      isRunning: false,
      isPaused: false,
      seed: this.config.seed,
    };

    this.cache = {
      dirtyEntities: new Set(),
      pendingPersistence: new Map(),
      lastSyncTimestamp: Date.now(),
    };

    this.metrics = {
      fps: 0,
      tickTime: 0,
      systemExecutionTimes: new Map(),
      entityCount: 0,
      activeSystemCount: 0,
      memoryUsage: 0,
    };

    this.eventListeners = new Map();

    debugLogger.info('RuntimeManager', 'Runtime system initialized', {
      tickRate: this.config.tickRate,
      seed: this.config.seed,
      deterministicMode: this.config.enableDeterminism,
    });
  }

  // Entity Management
  createEntity(id?: EntityId): Entity {
    const entityId = id || this.generateEntityId();
    
    if (this.state.entities.has(entityId)) {
      debugLogger.warn('RuntimeManager', `Entity ${entityId} already exists`);
      return this.state.entities.get(entityId)!;
    }

    if (this.state.entities.size >= this.config.maxEntities) {
      throw new Error(`Maximum entity limit reached: ${this.config.maxEntities}`);
    }

    const entity: Entity = {
      id: entityId,
      components: new Map(),
      active: true,
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };

    this.state.entities.set(entityId, entity);
    this.cache.dirtyEntities.add(entityId);
    this.metrics.entityCount = this.state.entities.size;

    debugLogger.info('RuntimeManager', `Entity created: ${entityId}`, {
      totalEntities: this.state.entities.size,
    });

    this.emitEvent({
      id: this.generateEventId(),
      type: 'entity:created',
      timestamp: Date.now(),
      data: { entityId },
    });

    return entity;
  }

  destroyEntity(entityId: EntityId): boolean {
    const entity = this.state.entities.get(entityId);
    if (!entity) {
      debugLogger.warn('RuntimeManager', `Entity ${entityId} not found`);
      return false;
    }

    this.state.entities.delete(entityId);
    this.cache.dirtyEntities.delete(entityId);
    this.cache.pendingPersistence.delete(entityId);
    this.metrics.entityCount = this.state.entities.size;

    debugLogger.info('RuntimeManager', `Entity destroyed: ${entityId}`, {
      totalEntities: this.state.entities.size,
    });

    this.emitEvent({
      id: this.generateEventId(),
      type: 'entity:destroyed',
      timestamp: Date.now(),
      data: { entityId },
    });

    return true;
  }

  getEntity(entityId: EntityId): Entity | undefined {
    return this.state.entities.get(entityId);
  }

  getAllEntities(): Entity[] {
    return Array.from(this.state.entities.values());
  }

  // Component Management
  addComponent(entityId: EntityId, componentType: ComponentType, data: ComponentData): boolean {
    const entity = this.state.entities.get(entityId);
    if (!entity) {
      debugLogger.warn('RuntimeManager', `Cannot add component: Entity ${entityId} not found`);
      return false;
    }

    entity.components.set(componentType, data);
    entity.updatedAt = Date.now();
    this.cache.dirtyEntities.add(entityId);

    debugLogger.info('RuntimeManager', `Component added to entity ${entityId}`, {
      componentType,
      componentCount: entity.components.size,
    });

    this.emitEvent({
      id: this.generateEventId(),
      type: 'component:added',
      timestamp: Date.now(),
      data: { entityId, componentType },
    });

    return true;
  }

  removeComponent(entityId: EntityId, componentType: ComponentType): boolean {
    const entity = this.state.entities.get(entityId);
    if (!entity) {
      debugLogger.warn('RuntimeManager', `Cannot remove component: Entity ${entityId} not found`);
      return false;
    }

    const removed = entity.components.delete(componentType);
    if (removed) {
      entity.updatedAt = Date.now();
      this.cache.dirtyEntities.add(entityId);

      debugLogger.info('RuntimeManager', `Component removed from entity ${entityId}`, {
        componentType,
        componentCount: entity.components.size,
      });

      this.emitEvent({
        id: this.generateEventId(),
        type: 'component:removed',
        timestamp: Date.now(),
        data: { entityId, componentType },
      });
    }

    return removed;
  }

  getComponent<T extends ComponentData>(entityId: EntityId, componentType: ComponentType): T | undefined {
    const entity = this.state.entities.get(entityId);
    return entity?.components.get(componentType) as T | undefined;
  }

  hasComponent(entityId: EntityId, componentType: ComponentType): boolean {
    const entity = this.state.entities.get(entityId);
    return entity?.components.has(componentType) ?? false;
  }

  // System Management
  registerSystem(system: System): void {
    if (this.state.systems.has(system.id)) {
      debugLogger.warn('RuntimeManager', `System ${system.id} already registered`);
      return;
    }

    this.state.systems.set(system.id, system);
    this.metrics.activeSystemCount = Array.from(this.state.systems.values()).filter(s => s.enabled).length;

    debugLogger.info('RuntimeManager', `System registered: ${system.name}`, {
      systemId: system.id,
      priority: system.priority,
      requiredComponents: system.requiredComponents,
    });
  }

  unregisterSystem(systemId: SystemId): boolean {
    const removed = this.state.systems.delete(systemId);
    if (removed) {
      this.metrics.activeSystemCount = Array.from(this.state.systems.values()).filter(s => s.enabled).length;
      debugLogger.info('RuntimeManager', `System unregistered: ${systemId}`);
    }
    return removed;
  }

  enableSystem(systemId: SystemId): void {
    const system = this.state.systems.get(systemId);
    if (system) {
      system.enabled = true;
      this.metrics.activeSystemCount = Array.from(this.state.systems.values()).filter(s => s.enabled).length;
      debugLogger.info('RuntimeManager', `System enabled: ${systemId}`);
    }
  }

  disableSystem(systemId: SystemId): void {
    const system = this.state.systems.get(systemId);
    if (system) {
      system.enabled = false;
      this.metrics.activeSystemCount = Array.from(this.state.systems.values()).filter(s => s.enabled).length;
      debugLogger.info('RuntimeManager', `System disabled: ${systemId}`);
    }
  }

  // Entity Query System
  queryEntities(query: EntityQuery): Entity[] {
    const results: Entity[] = [];

    for (const entity of this.state.entities.values()) {
      if (!entity.active) continue;

      const hasRequired = query.requiredComponents.every(comp => 
        entity.components.has(comp)
      );

      const hasExcluded = query.excludedComponents?.some(comp => 
        entity.components.has(comp)
      ) ?? false;

      if (hasRequired && !hasExcluded) {
        results.push(entity);
      }
    }

    return results;
  }

  // Runtime Loop
  start(): void {
    if (this.state.isRunning) {
      debugLogger.warn('RuntimeManager', 'Runtime already running');
      return;
    }

    this.state.isRunning = true;
    this.state.isPaused = false;
    this.lastTickTime = performance.now();
    this.accumulator = 0;

    debugLogger.success('RuntimeManager', 'Runtime started', {
      tickRate: this.state.tickRate,
      entityCount: this.state.entities.size,
      systemCount: this.state.systems.size,
    });

    this.emitEvent({
      id: this.generateEventId(),
      type: 'runtime:started',
      timestamp: Date.now(),
      data: {},
    });

    this.tick();
  }

  pause(): void {
    this.state.isPaused = true;
    debugLogger.info('RuntimeManager', 'Runtime paused');
    
    this.emitEvent({
      id: this.generateEventId(),
      type: 'runtime:paused',
      timestamp: Date.now(),
      data: {},
    });
  }

  resume(): void {
    if (!this.state.isRunning) {
      this.start();
      return;
    }

    this.state.isPaused = false;
    this.lastTickTime = performance.now();
    debugLogger.info('RuntimeManager', 'Runtime resumed');
    
    this.emitEvent({
      id: this.generateEventId(),
      type: 'runtime:resumed',
      timestamp: Date.now(),
      data: {},
    });

    this.tick();
  }

  stop(): void {
    this.state.isRunning = false;
    this.state.isPaused = false;
    
    if (this.animationFrameId !== null) {
      cancelAnimationFrame(this.animationFrameId);
      this.animationFrameId = null;
    }

    debugLogger.info('RuntimeManager', 'Runtime stopped');
    
    this.emitEvent({
      id: this.generateEventId(),
      type: 'runtime:stopped',
      timestamp: Date.now(),
      data: {},
    });
  }

  private tick = (): void => {
    if (!this.state.isRunning || this.state.isPaused) {
      return;
    }

    const currentTime = performance.now();
    const deltaTime = (currentTime - this.lastTickTime) / 1000;
    this.lastTickTime = currentTime;

    const fixedDeltaTime = 1 / this.state.tickRate;
    this.accumulator += deltaTime;

    const tickStart = performance.now();

    while (this.accumulator >= fixedDeltaTime) {
      this.executeSystems(fixedDeltaTime);
      this.state.currentTick++;
      this.accumulator -= fixedDeltaTime;
    }

    const tickEnd = performance.now();
    this.metrics.tickTime = tickEnd - tickStart;
    this.metrics.fps = 1000 / (currentTime - this.lastTickTime + this.metrics.tickTime);

    // Sync cache periodically
    if (Date.now() - this.cache.lastSyncTimestamp > 1000) {
      this.syncCache();
    }

    this.animationFrameId = requestAnimationFrame(this.tick);
  };

  private executeSystems(deltaTime: number): void {
    const systems = Array.from(this.state.systems.values())
      .filter(s => s.enabled)
      .sort((a, b) => a.priority - b.priority);

    for (const system of systems) {
      const systemStart = performance.now();
      
      try {
        const entities = this.queryEntities({ requiredComponents: system.requiredComponents });
        system.execute(entities, deltaTime);
      } catch (error) {
        debugLogger.error('RuntimeManager', `System execution error: ${system.name}`, {
          systemId: system.id,
          error: String(error),
        });
      }

      const systemEnd = performance.now();
      this.metrics.systemExecutionTimes.set(system.id, systemEnd - systemStart);
    }
  }

  // Cache Management
  private syncCache(): void {
    if (!this.config.enablePersistence) return;

    const dirtyCount = this.cache.dirtyEntities.size;
    if (dirtyCount === 0) return;

    debugLogger.info('RuntimeManager', `Syncing cache: ${dirtyCount} dirty entities`);

    for (const entityId of this.cache.dirtyEntities) {
      const entity = this.state.entities.get(entityId);
      if (entity) {
        this.cache.pendingPersistence.set(entityId, entity);
      }
    }

    this.cache.dirtyEntities.clear();
    this.cache.lastSyncTimestamp = Date.now();

    this.emitEvent({
      id: this.generateEventId(),
      type: 'cache:synced',
      timestamp: Date.now(),
      data: { entityCount: dirtyCount },
    });
  }

  markDirty(entityId: EntityId): void {
    this.cache.dirtyEntities.add(entityId);
  }

  getPendingPersistence(): Map<EntityId, Entity> {
    return new Map(this.cache.pendingPersistence);
  }

  clearPendingPersistence(): void {
    this.cache.pendingPersistence.clear();
  }

  // Event System
  addEventListener(eventType: string, callback: (event: GameEvent) => void): () => void {
    if (!this.eventListeners.has(eventType)) {
      this.eventListeners.set(eventType, new Set());
    }

    const listener: EventListener = { eventType, callback };
    this.eventListeners.get(eventType)!.add(listener);

    return () => {
      this.eventListeners.get(eventType)?.delete(listener);
    };
  }

  private emitEvent(event: GameEvent): void {
    const listeners = this.eventListeners.get(event.type);
    if (listeners) {
      listeners.forEach(listener => {
        try {
          listener.callback(event);
        } catch (error) {
          debugLogger.error('RuntimeManager', `Event listener error: ${event.type}`, {
            error: String(error),
          });
        }
      });
    }
  }

  // Metrics
  getMetrics(): PerformanceMetrics {
    return { ...this.metrics };
  }

  getState(): RuntimeState {
    return {
      ...this.state,
      entities: new Map(this.state.entities),
      systems: new Map(this.state.systems),
    };
  }

  // Serialization
  serializeEntity(entity: Entity): string {
    const serialized = {
      id: entity.id,
      components: Array.from(entity.components.entries()),
      active: entity.active,
      createdAt: entity.createdAt,
      updatedAt: entity.updatedAt,
    };
    return JSON.stringify(serialized);
  }

  deserializeEntity(data: string): Entity {
    const parsed = JSON.parse(data);
    return {
      id: parsed.id,
      components: new Map(parsed.components),
      active: parsed.active,
      createdAt: parsed.createdAt,
      updatedAt: parsed.updatedAt,
    };
  }

  // Utilities
  private generateEntityId(): EntityId {
    return `entity_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }

  private generateEventId(): string {
    return `event_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }

  reset(): void {
    this.stop();
    this.state.entities.clear();
    this.state.systems.clear();
    this.state.currentTick = 0;
    this.cache.dirtyEntities.clear();
    this.cache.pendingPersistence.clear();
    this.metrics.entityCount = 0;
    this.metrics.activeSystemCount = 0;

    debugLogger.info('RuntimeManager', 'Runtime reset');
    
    this.emitEvent({
      id: this.generateEventId(),
      type: 'runtime:reset',
      timestamp: Date.now(),
      data: {},
    });
  }
}

export const runtimeManager = new RuntimeManager();
export default RuntimeManager;
