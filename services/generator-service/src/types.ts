export interface PipelineDefinition {
  pipelineId?: string;
  requestId?: string;
  ecs?: EcsGenerationConfig;
  Ecs?: EcsGenerationConfig;
  steps?: PipelineStepDefinition[];
  Steps?: PipelineStepDefinition[];
}

export interface EcsGenerationConfig {
  dungeonLevel?: number;
  DungeonLevel?: number;
  width?: number;
  Width?: number;
  height?: number;
  Height?: number;
  enemyCount?: number;
  EnemyCount?: number;
  lootCount?: number;
  LootCount?: number;
  seed?: number;
  Seed?: number;
}

export interface PipelineStepDefinition {
  stage?: string;
  Stage?: string;
  ecsSystem?: string;
  EcsSystem?: string;
}

export interface PipelineExecutionRequest {
  requestedBy?: string;
  RequestedBy?: string;
  sessionId?: string;
  SessionId?: string;
  notes?: string;
  Notes?: string;
  constraintsYaml?: string;
  ConstraintsYaml?: string;
}

export interface WorldGenerationRequest {
  definition?: PipelineDefinition;
  Definition?: PipelineDefinition;
  execution?: PipelineExecutionRequest;
  Execution?: PipelineExecutionRequest;
}

export interface WorldRoom {
  id: number;
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface WorldEnemy {
  id: number;
  archetype: string;
  x: number;
  y: number;
  level: number;
}

export interface WorldLoot {
  itemId: string;
  itemType: string;
  tier: string;
  x: number;
  y: number;
}

export interface HeightmapBiomes {
  waterTiles: number;
  landTiles: number;
  mountainTiles: number;
  waterPercent: number;
  landPercent: number;
  mountainPercent: number;
}

export interface TerrainMeshVertex {
  x: number;
  y: number;
  z: number;
  u: number;
  v: number;
  normalX: number;
  normalY: number;
  normalZ: number;
}

export interface GeneratedTerrainMesh {
  meshId: string;
  width: number;
  height: number;
  seed: number;
  algorithm: string;
  waterLevel: number;
  heightScale: number;
  minHeight: number;
  maxHeight: number;
  vertices: TerrainMeshVertex[];
  triangles: number[];
  biomes: HeightmapBiomes;
}

export interface GeneratedWorldArtifact {
  seed: number;
  width: number;
  height: number;
  dungeonLevel: number;
  rooms: WorldRoom[];
  enemies: WorldEnemy[];
  loot: WorldLoot[];
  terrainMesh: GeneratedTerrainMesh;
}
