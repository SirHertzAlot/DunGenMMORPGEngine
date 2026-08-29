// ECS/DOTS Runtime System Type Definitions

export type EntityId = string;
export type ComponentType = string;
export type SystemId = string;

// Core ECS Component Types
export interface Transform {
  position: { x: number; y: number; z: number };
  rotation: { x: number; y: number; z: number };
  scale: { x: number; y: number; z: number };
}

export interface Health {
  max: number;
  current: number;
  regenerationRate: number;
}

export interface Inventory {
  capacity: number;
  items: InventoryItem[];
}

export interface InventoryItem {
  id: string;
  name: string;
  quantity: number;
  weight: number;
}

export interface AI {
  behaviorType: string;
  aggroRadius: number;
  patrolPath: { x: number; y: number; z: number }[];
  currentState: string;
}

export interface Quest {
  id: string;
  title: string;
  objectives: QuestObjective[];
  rewards: QuestReward[];
  status: 'active' | 'completed' | 'failed';
}

export interface QuestObjective {
  id: string;
  description: string;
  completed: boolean;
}

export interface QuestReward {
  type: string;
  value: number;
}

export interface Combat {
  damage: number;
  armor: number;
  attackSpeed: number;
  weaponType: string;
}

export interface Movement {
  speed: number;
  canJump: boolean;
  canFly: boolean;
  velocity: { x: number; y: number; z: number };
}

export interface Rendering {
  modelId?: string;
  color: { r: number; g: number; b: number };
  visible: boolean;
  castShadow: boolean;
}

// Component Registry
export type ComponentData = 
  | Transform 
  | Health 
  | Inventory 
  | AI 
  | Quest 
  | Combat 
  | Movement 
  | Rendering;

export interface ComponentSchema {
  type: ComponentType;
  data: ComponentData;
}

// Entity Definition
export interface Entity {
  id: EntityId;
  components: Map<ComponentType, ComponentData>;
  active: boolean;
  createdAt: number;
  updatedAt: number;
}

// System Definition
export interface System {
  id: SystemId;
  name: string;
  priority: number;
  requiredComponents: ComponentType[];
  execute: (entities: Entity[], deltaTime: number) => void;
  enabled: boolean;
}

// Runtime State
export interface RuntimeState {
  entities: Map<EntityId, Entity>;
  systems: Map<SystemId, System>;
  tickRate: number;
  currentTick: number;
  isRunning: boolean;
  isPaused: boolean;
  seed: number;
}

// Cache State
export interface CacheState {
  dirtyEntities: Set<EntityId>;
  pendingPersistence: Map<EntityId, Entity>;
  lastSyncTimestamp: number;
}

// Performance Metrics
export interface PerformanceMetrics {
  fps: number;
  tickTime: number;
  systemExecutionTimes: Map<SystemId, number>;
  entityCount: number;
  activeSystemCount: number;
  memoryUsage: number;
}

// Runtime Configuration
export interface RuntimeConfig {
  tickRate: number;
  maxEntities: number;
  enablePersistence: boolean;
  enableDeterminism: boolean;
  seed: number;
}

// Entity Query
export interface EntityQuery {
  requiredComponents: ComponentType[];
  excludedComponents?: ComponentType[];
}

// Event System
export interface GameEvent {
  id: string;
  type: string;
  timestamp: number;
  data: Record<string, unknown>;
}

export interface EventListener {
  eventType: string;
  callback: (event: GameEvent) => void;
}
