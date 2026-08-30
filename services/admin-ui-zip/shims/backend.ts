export type FileType = 'GLB' | 'GLTF' | 'OBJ' | 'FBX';
export type ArchiveType = 'zip';

export type BackendCellConfig = {
  noiseSettings: {
    noiseSeed: bigint;
    scale: number;
    roughness: number;
    intensity: number;
    radialWeight: number;
    heightRange: number;
  };
  erosionSettings: {
    hydraulic: {
      rainAmount: number;
      waterVolume: number;
      rainFreq: number;
      evaporationRate: number;
      erosionStr: number;
      sedimentCap: number;
      iterations: bigint;
      transferRate: number;
      soilTransfer: number;
      depositionRate: number;
    };
    thermal: {
      threshold: number;
      gravityStrength: number;
      incubation: number;
      creepFactor: number;
      transferRatio: number;
    };
    wind: {
      direction: number;
      iterations: bigint;
      erosionRate: number;
      sedimentTransfer: number;
      windStrength: number;
    };
    mountain: {
      threshold: number;
      plateauHeight: number;
      flatteningStr: number;
      smoothingStr: number;
      snowLine: number;
      snowCoverage: number;
    };
    river: {
      flowThreshold: number;
      lakeDepth: number;
      riverWidth: number;
      flowMultiplier: number;
      channelStr: number;
      evaporation: number;
      tributaryDensity: number;
      springFreq: number;
      rainfallAmt: number;
      maxRiverLength: bigint;
    };
    biomeFactors: {
      biomeType: string;
      forestDensity: number;
      vegetation: number;
      soilQuality: number;
    };
  };
  lightingSettings: {
    intensity: number;
    ambientLight: number;
    directionalIntensity: number;
    sunlight: number;
    shadowDepth: number;
    elevationTransform: number;
  };
};

export type CellConfig = BackendCellConfig;

export type FileMetadata = {
  id: Uint8Array;
  name: string;
  size: bigint;
  fileType: FileType;
  uploadedAt: bigint;
  relativePath: string;
  isDirectory: boolean;
  archiveType: ArchiveType | null;
};

export type backendInterface = {
  saveGridConfig: (uuid: Uint8Array, config: { dim: bigint; cells: BackendCellConfig[][]; owner: unknown }) => Promise<void>;
  getGridConfig: (uuid: Uint8Array) => Promise<{ cells: BackendCellConfig[][] } | null>;
  getAllGridConfigs: () => Promise<Array<[Uint8Array, { cells: BackendCellConfig[][] }]>>;
  getFiles: () => Promise<FileMetadata[]>;
  saveFile: (
    id: Uint8Array,
    blob: ExternalBlob,
    name: string,
    size: bigint,
    fileType: FileType,
    uploadedAt: bigint,
    extractionSource: Uint8Array | null,
    relativePath: string,
    isDirectory: boolean,
    archiveType: ArchiveType | null
  ) => Promise<void>;
};

export class ExternalBlob {
  private constructor(private readonly bytes: Uint8Array) {}

  static fromBytes(bytes: Uint8Array): ExternalBlob {
    return new ExternalBlob(bytes);
  }

  withUploadProgress(onProgress?: (progress: number) => void): ExternalBlob {
    if (onProgress) onProgress(100);
    return this;
  }

  toBytes(): Uint8Array {
    return this.bytes;
  }
}

function uint8ArrayToBase64(bytes: Uint8Array): string {
  let binary = '';
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
  return btoa(binary);
}

function base64ToUint8Array(base64: string): Uint8Array {
  const binary = atob(base64);
  const len = binary.length;
  const bytes = new Uint8Array(len);
  for (let i = 0; i < len; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

const API_BASE = '/admin';
const WORLD_API_BASE = '/v1';

export const backendImpl: backendInterface = {
  async saveGridConfig(uuid: Uint8Array, config: { dim: bigint; cells: BackendCellConfig[][]; owner: unknown }) {
    const id = uint8ArrayToBase64(uuid);
    const payload: Record<string, string> = { gridConfig: JSON.stringify(config) };
    const res = await fetch(`${WORLD_API_BASE}/world/sessions/${encodeURIComponent(id)}/metadata`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    if (!res.ok) throw new Error(`saveGridConfig failed: ${res.status}`);
  },

  async getGridConfig(uuid: Uint8Array) {
    const id = uint8ArrayToBase64(uuid);
    const res = await fetch(`${WORLD_API_BASE}/world/sessions/${encodeURIComponent(id)}/metadata`);
    if (!res.ok) return null;
    const body = await res.json();
    const props = body?.properties;
    if (!props || !props.gridConfig) return null;
    return { cells: JSON.parse(props.gridConfig) };
  },

  async getAllGridConfigs() {
    const res = await fetch(`${WORLD_API_BASE}/world/sessions`);
    if (!res.ok) return [];
    const ids: string[] = await res.json();
    const results: Array<[Uint8Array, { cells: BackendCellConfig[][] }]> = [];
    await Promise.all(ids.map(async (id) => {
      try {
        const mres = await fetch(`${WORLD_API_BASE}/world/sessions/${encodeURIComponent(id)}/metadata`);
        if (!mres.ok) return;
        const body = await mres.json();
        const props = body?.properties;
        if (props?.gridConfig) {
          const config = JSON.parse(props.gridConfig) as { cells: BackendCellConfig[][] };
          results.push([base64ToUint8Array(id), { cells: config.cells }]);
        }
      } catch { /* ignore per-session errors */ }
    }));
    return results;
  },

  async getFiles() {
    // No file backend implemented — return empty list for now.
    return [];
  },

  async saveFile(id, blob, name, size, fileType, uploadedAt, extractionSource, relativePath, isDirectory, archiveType) {
    // No server-side file store implemented in this shim. No-op.
    return;
  }
};

export default backendImpl;
