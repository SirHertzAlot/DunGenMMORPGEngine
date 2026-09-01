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
  deleteFile: (id: Uint8Array) => Promise<void>;
  getCallerUserProfile: () => Promise<unknown>;
  saveCallerUserProfile: (profile: { name: string }) => Promise<void>;
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

function textToUint8Array(text: string): Uint8Array {
  return new TextEncoder().encode(text);
}

function configKey(base64Id: string): string {
  // Grid ids are opaque strings; the backend keys them exactly. Base64 from a
  // byte array is URL-safe enough for a path segment when quoted/encoded.
  return encodeURIComponent(base64Id);
}

// The backend stores the grid config as an opaque JSON object that has a `cells`
// field. All REST calls ride the authenticated /admin group, so the browser never
// needs to present the admin key; nginx injects X-Admin-Key for /admin/.
const ADMIN_BASE = '/admin';

export const backendImpl: backendInterface = {
  async saveGridConfig(uuid: Uint8Array, config: { dim: bigint; cells: BackendCellConfig[][]; owner: unknown }) {
    const id = uint8ArrayToBase64(uuid);
    const res = await fetch(`${ADMIN_BASE}/grid-configs/${configKey(id)}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gridConfig: config })
    });
    if (!res.ok) throw new Error(`saveGridConfig failed: ${res.status}`);
  },

  async getGridConfig(uuid: Uint8Array) {
    const id = uint8ArrayToBase64(uuid);
    const res = await fetch(`${ADMIN_BASE}/grid-configs/${configKey(id)}`);
    if (res.status === 404) return null;
    if (!res.ok) throw new Error(`getGridConfig failed: ${res.status}`);
    const body = await res.json();
    const config = body?.gridConfig;
    if (!config || !config.cells) return null;
    return { cells: config.cells as BackendCellConfig[][] };
  },

  async getAllGridConfigs() {
    const res = await fetch(`${ADMIN_BASE}/grid-configs`);
    if (!res.ok) throw new Error(`getAllGridConfigs failed: ${res.status}`);
    const body = await res.json();
    const ids: Array<{ gridId: string }> = body?.configs ?? [];
    const results: Array<[Uint8Array, { cells: BackendCellConfig[][] }]> = [];
    await Promise.all(ids.map(async (entry) => {
      try {
        const mres = await fetch(`${ADMIN_BASE}/grid-configs/${configKey(entry.gridId)}`);
        if (!mres.ok) return;
        const mbody = await mres.json();
        const config = mbody?.gridConfig;
        if (config?.cells) {
          results.push([base64ToUint8Array(entry.gridId), { cells: config.cells as BackendCellConfig[][] }]);
        }
      } catch { /* ignore per-grid errors */ }
    }));
    return results;
  },

  async getFiles() {
    const res = await fetch(`${ADMIN_BASE}/files`);
    if (!res.ok) throw new Error(`getFiles failed: ${res.status}`);
    const body = await res.json();
    const files: FileMetadata[] = (body?.files ?? []).map((f: any) => ({
      id: base64ToUint8Array(f.id),
      name: f.name,
      size: BigInt(f.size ?? 0),
      fileType: (f.fileType ?? 'GLB') as FileType,
      uploadedAt: BigInt(f.uploadedAtUnixMs ?? 0),
      relativePath: f.relativePath ?? '',
      isDirectory: !!f.isDirectory,
      archiveType: (f.archiveType ?? null) as ArchiveType | null,
    }));
    return files;
  },

  async saveFile(id, blob, name, size, fileType, uploadedAt, extractionSource, relativePath, isDirectory, archiveType) {
    const idB64 = uint8ArrayToBase64(id);
    const payload = {
      id: idB64,
      name,
      size: Number(size),
      fileType,
      uploadedAtUnixMs: Number(uploadedAt),
      relativePath: relativePath ?? '',
      isDirectory: !!isDirectory,
      archiveType: archiveType ?? null,
      extractionSourceId: extractionSource ? uint8ArrayToBase64(extractionSource) : null,
      dataBase64: uint8ArrayToBase64(blob.toBytes()),
    };
    const res = await fetch(`${ADMIN_BASE}/files`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    if (!res.ok) throw new Error(`saveFile failed: ${res.status}`);
  },

  async deleteFile(id: Uint8Array) {
    const idB64 = uint8ArrayToBase64(id);
    const res = await fetch(`${ADMIN_BASE}/files/${configKey(idB64)}`, { method: 'DELETE' });
    if (!res.ok) throw new Error(`deleteFile failed: ${res.status}`);
  },

  async getCallerUserProfile() {
    // No profile backend exists; return null (matches offline behavior shape).
    return null;
  },

  async saveCallerUserProfile() {
    // No profile backend exists; no-op.
    return;
  }
};

export default backendImpl;
