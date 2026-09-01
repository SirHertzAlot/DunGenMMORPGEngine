export interface GridConfig {
  id: string;
  dimension: number;
  mode: 'terrain' | 'dungeon';
  cells: CellConfig[][];
}

export interface CellConfig {
  mode: 'terrain' | 'dungeon';
  dungeonSettings?: DungeonCellSettings;
  noiseSettings: NoiseSettings;
  erosionSettings: ErosionSettings;
  lightingSettings: LightingSettings;
}

export interface DungeonCellSettings {
  selectedModel: string | null;
  rotation: number;
  tileType: 'room' | 'corridor' | 'door' | 'wall' | 'entrance' | 'exit';
}

export interface NoiseSettings {
  noiseScale: number;
  elevation: number;
  octaves: number;
  persistence: number;
}

export interface ErosionSettings {
  hydraulic: HydraulicErosionSettings;
  thermal: ThermalErosionSettings;
  wind: WindErosionSettings;
  plateau: PlateauErosionSettings;
  river: RiverErosionSettings;
}

export interface HydraulicErosionSettings {
  iterations: number;
  strength: number;
  sedimentCapacity: number;
}

export interface ThermalErosionSettings {
  iterations: number;
  strength: number;
  talusAngle: number;
}

export interface WindErosionSettings {
  iterations: number;
  direction: number;
  transportRate: number;
}

export interface PlateauErosionSettings {
  iterations: number;
  threshold: number;
  strength: number;
}

export interface RiverErosionSettings {
  iterations: number;
  flowDirectionBias: number;
  rainfallSourcePoints: number;
  erosionDepositionRate: number;
  evaporationRate: number;
  poolingThreshold: number;
}

export interface LightingSettings {
  ambientIntensity: number;
  directionalIntensity: number;
}
