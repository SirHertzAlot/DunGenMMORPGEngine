import YAML from "yaml";
import { GraphTraversalEngine, type GraphDefinition } from "./graphTraversalEngine.js";
import { generateDeterministicId, mulberry32 } from "./seededRng.js";
import type {
  EcsGenerationConfig,
  GeneratedTerrainMesh,
  GeneratedWorldArtifact,
  HeightmapBiomes,
  PipelineDefinition,
  PipelineExecutionRequest,
  TerrainMeshVertex,
  WorldEnemy,
  WorldGenerationRequest,
  WorldLoot,
  WorldRoom,
} from "../types.js";

const enemyGraph: GraphDefinition = {
  entry: "encounter-root",
  nodes: {
    "encounter-root": { id: "encounter-root", type: "choice", next: ["goblin-pack", "undead-patrol", "cultist-cell"] },
    "goblin-pack": { id: "goblin-pack", type: "emit", weight: 4, emit: { encounter: { archetype: "goblin" } } },
    "undead-patrol": { id: "undead-patrol", type: "emit", weight: 3, emit: { encounter: { archetype: "skeleton" } } },
    "cultist-cell": { id: "cultist-cell", type: "emit", weight: 2, emit: { encounter: { archetype: "cultist" } } },
  },
};

const lootTypes = ["sword", "shield", "potion", "staff", "armor", "bow"];
const lootTiers = ["common", "rare", "epic", "legendary"];

export function generateWorldArtifact(request: WorldGenerationRequest): GeneratedWorldArtifact {
  const definition = (request.definition ?? request.Definition ?? {}) as PipelineDefinition;
  const execution = (request.execution ?? request.Execution ?? {}) as PipelineExecutionRequest;
  const ecs = normalizeEcs(definition, execution);
  const rng = mulberry32(ecs.seed);
  const terrainMesh = generateTerrainMesh(ecs.width, ecs.height, ecs.seed);

  const roomCount = clamp(Math.max(4, Math.floor((ecs.width * ecs.height) / 180)), 4, 64);
  const rooms: WorldRoom[] = [];
  for (let i = 0; i < roomCount; i++) {
    const roomWidth = randomInt(rng, 4, Math.max(5, Math.min(12, ecs.width)));
    const roomHeight = randomInt(rng, 4, Math.max(5, Math.min(10, ecs.height)));
    rooms.push({
      id: i + 1,
      x: randomInt(rng, 0, Math.max(1, ecs.width - roomWidth)),
      y: randomInt(rng, 0, Math.max(1, ecs.height - roomHeight)),
      width: roomWidth,
      height: roomHeight,
    });
  }

  const enemies: WorldEnemy[] = [];
  for (let i = 0; i < ecs.enemyCount; i++) {
    const traversal = new GraphTraversalEngine(enemyGraph, ecs.seed + i + 17).traverse();
    const firstEventData = traversal.events[0]?.data as Record<string, unknown> | undefined;
    const encounter = firstEventData?.encounter as Record<string, unknown> | undefined;
    const archetype = String(firstEventData?.archetype ?? encounter?.archetype ?? "goblin");
    enemies.push({
      id: i + 1,
      archetype,
      x: randomInt(rng, 0, Math.max(1, ecs.width)),
      y: randomInt(rng, 0, Math.max(1, ecs.height)),
      level: Math.max(1, ecs.dungeonLevel + randomInt(rng, -1, 2)),
    });
  }

  const loot: WorldLoot[] = [];
  for (let i = 0; i < ecs.lootCount; i++) {
    loot.push({
      itemId: generateDeterministicId("loot", ecs.seed, i + 1),
      itemType: lootTypes[randomInt(rng, 0, lootTypes.length)],
      tier: lootTiers[randomInt(rng, 0, lootTiers.length)],
      x: randomInt(rng, 0, Math.max(1, ecs.width)),
      y: randomInt(rng, 0, Math.max(1, ecs.height)),
    });
  }

  return {
    seed: ecs.seed,
    width: ecs.width,
    height: ecs.height,
    dungeonLevel: ecs.dungeonLevel,
    rooms,
    enemies,
    loot,
    terrainMesh,
  };
}

function generateTerrainMesh(width: number, height: number, seed: number): GeneratedTerrainMesh {
  const waterLevel = 0.32;
  const heightScale = 24;
  const normalized = buildNormalizedHeightfield(width, height, seed);
  const vertices: TerrainMeshVertex[] = new Array(width * height);
  let waterTiles = 0;
  let landTiles = 0;
  let mountainTiles = 0;

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const value = normalized[y * width + x];
      if (value < waterLevel) {
        waterTiles++;
      } else if (value > 0.75) {
        mountainTiles++;
      } else {
        landTiles++;
      }

      vertices[y * width + x] = {
        x,
        y: round4(value * heightScale),
        z: y,
        u: width <= 1 ? 0 : round4(x / (width - 1)),
        v: height <= 1 ? 0 : round4(y / (height - 1)),
        normalX: 0,
        normalY: 1,
        normalZ: 0,
      };
    }
  }

  const triangles = buildTriangles(width, height);
  applyNormals(vertices, triangles);
  const biomes: HeightmapBiomes = {
    waterTiles,
    landTiles,
    mountainTiles,
    waterPercent: round1((waterTiles / (width * height)) * 100),
    landPercent: round1((landTiles / (width * height)) * 100),
    mountainPercent: round1((mountainTiles / (width * height)) * 100),
  };

  return {
    meshId: `terrain_${seed}`,
    width,
    height,
    seed,
    algorithm: "value-noise",
    waterLevel,
    heightScale,
    minHeight: 0,
    maxHeight: heightScale,
    vertices,
    triangles,
    biomes,
  };
}

function buildNormalizedHeightfield(width: number, height: number, seed: number): number[] {
  const values = new Array<number>(width * height).fill(0);
  let amplitude = 1;
  let frequency = 4 / Math.max(width, height);
  let maxAmplitude = 0;

  for (let octave = 0; octave < 4; octave++) {
    const rng = mulberry32(seed + octave * 7919);
    const gridWidth = Math.max(2, Math.floor(width * frequency) + 2);
    const gridHeight = Math.max(2, Math.floor(height * frequency) + 2);
    const noise = new Array<number>(gridWidth * gridHeight);
    for (let i = 0; i < noise.length; i++) {
      noise[i] = rng();
    }

    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const fx = clamp(x * frequency * (gridWidth - 1), 0, gridWidth - 2);
        const fy = clamp(y * frequency * (gridHeight - 1), 0, gridHeight - 2);
        const ix = Math.floor(fx);
        const iy = Math.floor(fy);
        const tx = fx - ix;
        const ty = fy - iy;
        const top = lerp(noise[iy * gridWidth + ix], noise[iy * gridWidth + ix + 1], tx);
        const bottom = lerp(noise[(iy + 1) * gridWidth + ix], noise[(iy + 1) * gridWidth + ix + 1], tx);
        values[y * width + x] += lerp(top, bottom, ty) * amplitude;
      }
    }

    maxAmplitude += amplitude;
    amplitude *= 0.55;
    frequency *= 2;
  }

  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;
  for (let i = 0; i < values.length; i++) {
    values[i] /= maxAmplitude;
    if (values[i] < min) {
      min = values[i];
    }
    if (values[i] > max) {
      max = values[i];
    }
  }

  const range = Math.max(max - min, Number.EPSILON);
  for (let i = 0; i < values.length; i++) {
    values[i] = (values[i] - min) / range;
  }

  return values;
}

function buildTriangles(width: number, height: number): number[] {
  const triangles = new Array<number>((width - 1) * (height - 1) * 6);
  let offset = 0;
  for (let y = 0; y < height - 1; y++) {
    for (let x = 0; x < width - 1; x++) {
      const topLeft = y * width + x;
      const topRight = topLeft + 1;
      const bottomLeft = topLeft + width;
      const bottomRight = bottomLeft + 1;
      triangles[offset++] = topLeft;
      triangles[offset++] = bottomLeft;
      triangles[offset++] = topRight;
      triangles[offset++] = topRight;
      triangles[offset++] = bottomLeft;
      triangles[offset++] = bottomRight;
    }
  }

  return triangles;
}

function applyNormals(vertices: TerrainMeshVertex[], triangles: number[]): void {
  for (let i = 0; i < triangles.length; i += 3) {
    const a = vertices[triangles[i]];
    const b = vertices[triangles[i + 1]];
    const c = vertices[triangles[i + 2]];

    const abX = b.x - a.x;
    const abY = b.y - a.y;
    const abZ = b.z - a.z;
    const acX = c.x - a.x;
    const acY = c.y - a.y;
    const acZ = c.z - a.z;

    const normalX = abY * acZ - abZ * acY;
    const normalY = abZ * acX - abX * acZ;
    const normalZ = abX * acY - abY * acX;

    a.normalX += normalX;
    a.normalY += normalY;
    a.normalZ += normalZ;
    b.normalX += normalX;
    b.normalY += normalY;
    b.normalZ += normalZ;
    c.normalX += normalX;
    c.normalY += normalY;
    c.normalZ += normalZ;
  }

  for (const vertex of vertices) {
    const magnitude = Math.sqrt(
      vertex.normalX * vertex.normalX +
      vertex.normalY * vertex.normalY +
      vertex.normalZ * vertex.normalZ,
    );
    if (magnitude <= Number.EPSILON) {
      vertex.normalX = 0;
      vertex.normalY = 1;
      vertex.normalZ = 0;
      continue;
    }

    vertex.normalX = round4(vertex.normalX / magnitude);
    vertex.normalY = round4(vertex.normalY / magnitude);
    vertex.normalZ = round4(vertex.normalZ / magnitude);
  }
}

function lerp(start: number, end: number, amount: number): number {
  return start + (end - start) * amount;
}

function round4(value: number): number {
  return Math.round(value * 10000) / 10000;
}

function round1(value: number): number {
  return Math.round(value * 10) / 10;
}

function normalizeEcs(definition: PipelineDefinition, execution: PipelineExecutionRequest): Required<{ dungeonLevel: number; width: number; height: number; enemyCount: number; lootCount: number; seed: number }> {
  const rawEcs = (definition.ecs ?? definition.Ecs ?? {}) as EcsGenerationConfig;
  const parsedYaml = parseConstraints(execution.constraintsYaml ?? execution.ConstraintsYaml ?? "");
  const yamlEcs = typeof parsedYaml.ecs === "object" && parsedYaml.ecs ? parsedYaml.ecs as Record<string, unknown> : parsedYaml;

  return {
    dungeonLevel: readNumber(yamlEcs, ["dungeonLevel", "DungeonLevel"], readNumber(rawEcs, ["dungeonLevel", "DungeonLevel"], 1)),
    width: readNumber(yamlEcs, ["width", "Width"], readNumber(rawEcs, ["width", "Width"], 80)),
    height: readNumber(yamlEcs, ["height", "Height"], readNumber(rawEcs, ["height", "Height"], 24)),
    enemyCount: readNumber(yamlEcs, ["enemyCount", "EnemyCount"], readNumber(rawEcs, ["enemyCount", "EnemyCount"], 5)),
    lootCount: readNumber(yamlEcs, ["lootCount", "LootCount"], readNumber(rawEcs, ["lootCount", "LootCount"], 3)),
    seed: readNumber(yamlEcs, ["seed", "Seed"], readNumber(rawEcs, ["seed", "Seed"], 12345)),
  };
}

function parseConstraints(raw: string): Record<string, unknown> {
  if (!raw.trim()) {
    return {};
  }

  try {
    const parsed = YAML.parse(raw);
    return typeof parsed === "object" && parsed ? parsed as Record<string, unknown> : {};
  } catch {
    return {};
  }
}

function readNumber(source: unknown, keys: string[], fallback: number): number {
  if (!source || typeof source !== "object") {
    return fallback;
  }

  for (const key of keys) {
    const value = (source as Record<string, unknown>)[key];
    if (typeof value === "number" && Number.isFinite(value)) {
      return value;
    }
  }

  return fallback;
}

function randomInt(rng: () => number, minInclusive: number, maxExclusive: number): number {
  if (maxExclusive <= minInclusive) {
    return minInclusive;
  }

  return Math.floor(rng() * (maxExclusive - minInclusive)) + minInclusive;
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}
