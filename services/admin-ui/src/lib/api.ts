/// <reference types="vite/client" />

// All paths are proxied: /api/* → backend root (Vite dev proxy / nginx in prod)
const env = import.meta.env as {
  VITE_API_BASE_PATH?: string
  VITE_ADMIN_API_KEY?: string
}

const BASE = (env.VITE_API_BASE_PATH ?? '/api').replace(/\/$/, '')
const ADMIN_KEY = env.VITE_ADMIN_API_KEY ?? 'dev-admin-key'

const adminHeaders: HeadersInit = {
  'Content-Type': 'application/json',
  'X-Admin-Key': ADMIN_KEY,
}

async function request<T>(input: RequestInfo, init?: RequestInit): Promise<T> {
  const res = await fetch(input, init)
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(`${res.status}: ${text}`)
  }
  return res.json() as Promise<T>
}

function asRecord(value: unknown): Record<string, unknown> {
  if (value && typeof value === 'object' && !Array.isArray(value))
    return value as Record<string, unknown>

  throw new Error('Unexpected response shape from backend.')
}

// ── Pipeline ─────────────────────────────────────────────────────────────────
export const getPipelineRuntime = () =>
  request<unknown>(`${BASE}/admin/pipeline/runtime/current`, { headers: adminHeaders })

export const createPipelineRequest = (body: object) =>
  request<{ requestId: string }>(`${BASE}/admin/pipeline/requests`, {
    method: 'POST', headers: adminHeaders, body: JSON.stringify(body),
  })

export const approvePipelineRequest = (id: string, body: object) =>
  request<unknown>(`${BASE}/admin/pipeline/requests/${id}/approve`, {
    method: 'POST', headers: adminHeaders, body: JSON.stringify(body),
  })

export const getLatestWorld = () =>
  request<unknown>(`${BASE}/admin/pipeline/runtime/world/current`, { headers: adminHeaders })

// ── Generators ───────────────────────────────────────────────────────────────
export const createGeneratorJob = (body: object) =>
  request<GeneratorJobRecord>(`${BASE}/admin/generators/jobs`, {
    method: 'POST', headers: adminHeaders, body: JSON.stringify(body),
  })

export async function executeWorldViaJob(body: WorldGeneratorJobRequest): Promise<PipelineExecutionRecord> {
  const job = await createGeneratorJob({
    generatorId: 'world-pipeline',
    requestedBy: 'admin-ui',
    sessionId: body.sessionId,
    notes: body.notes,
    constraintsYaml: body.constraintsYaml ?? '',
    seedOverride: body.seed,
    parameters: body.parameters ?? {},
  })

  if (!job.execution)
    throw new Error('World job completed without an execution payload.')

  return asRecord(job.execution) as unknown as PipelineExecutionRecord
}

export async function generateCharactersViaJob(body: CharacterGeneratorJobRequest): Promise<GeneratedCharacter[]> {
  const job = await createGeneratorJob({
    generatorId: 'characters',
    requestedBy: 'admin-ui',
    seedOverride: body.seed,
    parameters: {
      level: String(body.level),
      count: String(body.count),
      ...(body.class ? { class: body.class } : {}),
      ...(body.race ? { race: body.race } : {}),
    },
  })

  if (!Array.isArray(job.result))
    throw new Error('Character job completed without a result payload.')

  return job.result as GeneratedCharacter[]
}

export async function generateTerrainMeshViaJob(body: HeightmapGeneratorJobRequest): Promise<GeneratedTerrainMesh> {
  const job = await createGeneratorJob({
    generatorId: 'heightmap',
    requestedBy: 'admin-ui',
    seedOverride: body.seed,
    parameters: {
      width: String(body.width),
      height: String(body.height),
      waterLevel: String(body.waterLevel),
      algorithm: body.algorithm,
      roughness: String(body.roughness),
      octaves: String(body.octaves),
    },
  })

  return asRecord(job.result) as unknown as GeneratedTerrainMesh
}

// ── Observability ─────────────────────────────────────────────────────────────
export const getSnapshot = () =>
  request<unknown>(`${BASE}/admin/observability/snapshot`, { headers: adminHeaders })

export const getEvents = (take = 50) =>
  request<{ events: ObservabilityEvent[] }>(`${BASE}/admin/observability/events?take=${take}`, {
    headers: adminHeaders,
  })

export const getContainerHealth = () =>
  request<ContainerHealthStatus[]>(`${BASE}/admin/observability/containers/health`, { headers: adminHeaders })

export const getContainerLogInsights = (tail = 250) =>
  request<ContainerLogInsight[]>(`${BASE}/admin/observability/containers/logs?tail=${tail}`, {
    headers: adminHeaders,
  })

export const getDatabaseObservabilitySnapshot = () =>
  request<DatabaseObservabilitySnapshot>(`${BASE}/admin/observability/databases/snapshot`, {
    headers: adminHeaders,
  })

export const queryDatabasePrometheus = (database: string, query: string) =>
  request<PrometheusQueryResult>(
    `${BASE}/admin/observability/databases/${encodeURIComponent(database)}/query?query=${encodeURIComponent(query)}`,
    { headers: adminHeaders },
  )

export const runDatabaseMaintenance = (database: string, action: string, confirmed = false) =>
  request<DatabaseMaintenanceResult>(`${BASE}/admin/observability/databases/${encodeURIComponent(database)}/maintenance`, {
    method: 'POST',
    headers: adminHeaders,
    body: JSON.stringify({ action, confirmed }),
  })

// ── Types ─────────────────────────────────────────────────────────────────────
export interface CharacterStats {
  strength: number; dexterity: number; intelligence: number
  constitution: number; wisdom: number; charisma: number
}

export interface EquipmentSlot { itemId: string; type: string; tier: string; name: string }

export interface CharacterEquipment {
  mainHand?: EquipmentSlot; offHand?: EquipmentSlot
  armor?: EquipmentSlot; accessory?: EquipmentSlot
}

export interface GeneratedCharacter {
  characterId: string; name: string; class: string; race: string; level: number
  stats: CharacterStats; hitPoints: number; maxHitPoints: number
  armorClass: number; speed: number; skills: string[]; abilities: string[]
  equipment: CharacterEquipment; gold: number; background: string; alignment: string; seed: number
}

export interface HeightmapBiomes {
  waterTiles: number; landTiles: number; mountainTiles: number
  waterPercent: number; landPercent: number; mountainPercent: number
}

export interface WorldRoom {
  id: number; x: number; y: number; width: number; height: number
}

export interface WorldEnemy {
  id: number; archetype: string; x: number; y: number; level: number
}

export interface WorldLoot {
  itemId: string; itemType: string; tier: string; x: number; y: number
}

export interface GeneratedWorldArtifact {
  seed: number
  width: number
  height: number
  dungeonLevel: number
  rooms: WorldRoom[]
  enemies: WorldEnemy[]
  loot: WorldLoot[]
  terrainMesh?: GeneratedTerrainMesh
}

export interface PipelineExecutionRecord {
  executionId: string
  pipelineId: string
  requestId: string
  sessionId?: string | null
  requestedBy: string
  notes: string
  startedAtUtc: string
  completedAtUtc: string
  artifactPath: string
  status: string
  stepResults: Array<Record<string, unknown>>
  world: GeneratedWorldArtifact
}

export interface TerrainMeshVertex {
  x: number; y: number; z: number
  u: number; v: number
  normalX: number; normalY: number; normalZ: number
}

export interface GeneratedTerrainMesh {
  meshId: string; width: number; height: number; seed: number
  algorithm: string; waterLevel: number; heightScale: number; minHeight: number; maxHeight: number
  vertices: TerrainMeshVertex[]; triangles: number[]; biomes: HeightmapBiomes
}

export interface ObservabilityEvent {
  eventId: string; type: string; timestamp: string; payload: unknown
}

export interface ContainerHealthStatus {
  name: string
  kind: string
  target: string
  isOnline: boolean
  statusCode?: number
  responseTimeMs: number
  checkedAtUtc: string
  message: string
}

export interface ContainerLogInsight {
  containerName: string
  capturedAtUtc: string
  sourceAvailable: boolean
  lineCount: number
  errorCount: number
  warningCount: number
  healthHint: string
  message: string
  lastLines: string[]
}

export interface DatabasePanelSnapshot {
  name: string
  displayName: string
  isUp: boolean
  capturedAtUtc: string
  metrics: Record<string, number | null>
  maintenanceActions: string[]
  notes: string
}

export interface DatabaseObservabilitySnapshot {
  capturedAtUtc: string
  databases: DatabasePanelSnapshot[]
}

export interface PrometheusQueryResult {
  database: string
  query: string
  success: boolean
  value: number | null
  message: string
  capturedAtUtc: string
}

export interface DatabaseMaintenanceResult {
  database: string
  action: string
  success: boolean
  message: string
}

export interface GeneratorJobRecord {
  jobId: string
  generatorId: string
  outputMode: string
  requestedBy: string
  sessionId?: string | null
  constraintsYaml: string
  notes: string
  seedOverride?: number | null
  submittedAtUtc: string
  completedAtUtc?: string | null
  status: string
  error?: string | null
  execution?: unknown
  result?: unknown
  parameters: Record<string, string>
}

export interface CharacterGeneratorJobRequest {
  level: number
  class?: string
  race?: string
  count: number
  seed?: number
}

export interface HeightmapGeneratorJobRequest {
  width: number
  height: number
  seed?: number
  waterLevel: number
  algorithm: string
  roughness: number
  octaves: number
}

export interface WorldGeneratorJobRequest {
  sessionId: string
  notes?: string
  constraintsYaml?: string
  seed?: number
  parameters?: Record<string, string>
}
