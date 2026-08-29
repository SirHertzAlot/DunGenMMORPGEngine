import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ExternalBlob } from '../backend';
import type { backendInterface, CellConfig as BackendCellConfig, FileMetadata, FileType, ArchiveType } from '../backend';
import type { CellConfig } from '../types/grid';
import { toast } from 'sonner';

// Stub hook for offline mode - always returns not ready
function useOfflineActor(): { actor: backendInterface | null; isReady: boolean; status: 'offline' } {
  return {
    actor: null,
    isReady: false,
    status: 'offline' as const,
  };
}

// Convert frontend CellConfig to backend CellConfig
function toBackendCellConfig(config: CellConfig): BackendCellConfig {
  return {
    noiseSettings: {
      noiseSeed: BigInt(12345),
      scale: config.noiseSettings.noiseScale,
      roughness: config.noiseSettings.persistence,
      intensity: config.noiseSettings.elevation,
      radialWeight: 0,
      heightRange: config.noiseSettings.elevation,
    },
    erosionSettings: {
      hydraulic: {
        rainAmount: 1.0,
        waterVolume: 1.0,
        rainFreq: 1.0,
        evaporationRate: 0.01,
        erosionStr: config.erosionSettings.hydraulic.strength,
        sedimentCap: config.erosionSettings.hydraulic.sedimentCapacity,
        iterations: BigInt(config.erosionSettings.hydraulic.iterations),
        transferRate: 0.5,
        soilTransfer: 0.5,
        depositionRate: 0.3,
      },
      thermal: {
        threshold: config.erosionSettings.thermal.talusAngle,
        gravityStrength: config.erosionSettings.thermal.strength,
        incubation: 0,
        creepFactor: 0.5,
        transferRatio: 0.5,
      },
      wind: {
        direction: config.erosionSettings.wind.direction,
        iterations: BigInt(config.erosionSettings.wind.iterations),
        erosionRate: 0.05,
        sedimentTransfer: config.erosionSettings.wind.transportRate,
        windStrength: 1.0,
      },
      mountain: {
        threshold: config.erosionSettings.plateau.threshold,
        plateauHeight: 0.7,
        flatteningStr: config.erosionSettings.plateau.strength,
        smoothingStr: 0.3,
        snowLine: 0.8,
        snowCoverage: 0.5,
      },
      river: {
        flowThreshold: 0.1,
        lakeDepth: 0.5,
        riverWidth: 2.0,
        flowMultiplier: 1.0,
        channelStr: config.erosionSettings.river.erosionDepositionRate,
        evaporation: config.erosionSettings.river.evaporationRate,
        tributaryDensity: 0.5,
        springFreq: 0.1,
        rainfallAmt: 1.0,
        maxRiverLength: BigInt(100),
      },
      biomeFactors: {
        biomeType: 'temperate',
        forestDensity: 0.5,
        vegetation: 0.5,
        soilQuality: 0.5,
      },
    },
    lightingSettings: {
      intensity: config.lightingSettings.ambientIntensity,
      ambientLight: config.lightingSettings.ambientIntensity,
      directionalIntensity: config.lightingSettings.directionalIntensity,
      sunlight: 1.0,
      shadowDepth: 0.5,
      elevationTransform: 1.0,
    },
  };
}

// Convert backend CellConfig to frontend CellConfig
function fromBackendCellConfig(config: BackendCellConfig): CellConfig {
  return {
    mode: 'terrain',
    noiseSettings: {
      noiseScale: config.noiseSettings.scale,
      elevation: config.noiseSettings.intensity,
      octaves: 4,
      persistence: config.noiseSettings.roughness,
    },
    erosionSettings: {
      hydraulic: {
        iterations: Number(config.erosionSettings.hydraulic.iterations),
        strength: config.erosionSettings.hydraulic.erosionStr,
        sedimentCapacity: config.erosionSettings.hydraulic.sedimentCap,
      },
      thermal: {
        iterations: 50,
        strength: config.erosionSettings.thermal.gravityStrength,
        talusAngle: config.erosionSettings.thermal.threshold,
      },
      wind: {
        iterations: Number(config.erosionSettings.wind.iterations),
        direction: config.erosionSettings.wind.direction,
        transportRate: config.erosionSettings.wind.sedimentTransfer,
      },
      plateau: {
        iterations: 50,
        threshold: config.erosionSettings.mountain.threshold,
        strength: config.erosionSettings.mountain.flatteningStr,
      },
      river: {
        iterations: 200,
        flowDirectionBias: 180,
        rainfallSourcePoints: 5,
        erosionDepositionRate: config.erosionSettings.river.channelStr,
        evaporationRate: config.erosionSettings.river.evaporation,
        poolingThreshold: 0.4,
      },
    },
    lightingSettings: {
      ambientIntensity: config.lightingSettings.ambientLight,
      directionalIntensity: config.lightingSettings.directionalIntensity,
    },
  };
}

export function useSaveGrid() {
  const { actor, isReady } = useOfflineActor();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, cells }: { id: string; cells: CellConfig[][] }) => {
      if (!actor || !isReady) throw new Error('Backend not available in offline mode');

      const backendCells = cells.map(row =>
        row.map(cell => toBackendCellConfig(cell))
      );

      const uuid = new TextEncoder().encode(id);
      await actor.saveGridConfig(uuid, {
        dim: BigInt(cells.length),
        cells: backendCells,
        owner: undefined as any,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['grids'] });
    },
    onError: (error) => {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to save grid: ${errorMessage}`);
    },
  });
}

export function useLoadGrid(id: string) {
  const { actor, isReady } = useOfflineActor();

  return useQuery<CellConfig[][] | null>({
    queryKey: ['grid', id],
    queryFn: async () => {
      if (!actor || !id) return null;

      const uuid = new TextEncoder().encode(id);
      const result = await actor.getGridConfig(uuid);

      if (!result) return null;

      return result.cells.map(row =>
        row.map(cell => fromBackendCellConfig(cell))
      );
    },
    enabled: !!actor && isReady && !!id,
    retry: 1,
  });
}

export function useAllGrids() {
  const { actor, isReady } = useOfflineActor();

  return useQuery<Array<{ id: string; cells: CellConfig[][] }>>({
    queryKey: ['grids'],
    queryFn: async () => {
      if (!actor) return [];

      const result = await actor.getAllGridConfigs();

      return result.map(([uuid, config]) => ({
        id: new TextDecoder().decode(uuid),
        cells: config.cells.map(row =>
          row.map(cell => fromBackendCellConfig(cell))
        ),
      }));
    },
    enabled: !!actor && isReady,
    retry: 1,
  });
}

// File management hooks
export function useFiles() {
  const { actor, isReady } = useOfflineActor();

  return useQuery<FileMetadata[]>({
    queryKey: ['files'],
    queryFn: async () => {
      if (!actor) return [];
      return actor.getFiles();
    },
    enabled: !!actor && isReady,
    retry: 1,
  });
}

export function useUploadFile() {
  const { actor, isReady } = useOfflineActor();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ 
      file, 
      onProgress,
      extractionSource,
      relativePath = '',
      isDirectory = false,
      archiveType = null,
    }: { 
      file: File; 
      onProgress?: (progress: number) => void;
      extractionSource?: Uint8Array | null;
      relativePath?: string;
      isDirectory?: boolean;
      archiveType?: 'zip' | null;
    }) => {
      if (!actor || !isReady) throw new Error('Backend not available in offline mode');

      // Read file as array buffer
      const arrayBuffer = await file.arrayBuffer();
      const bytes = new Uint8Array(arrayBuffer);

      // Create ExternalBlob with progress tracking
      let externalBlob = ExternalBlob.fromBytes(bytes);
      if (onProgress) {
        externalBlob = externalBlob.withUploadProgress(onProgress);
      }

      // Generate file ID
      const id = crypto.getRandomValues(new Uint8Array(16));

      // Determine file type
      const extension = file.name.split('.').pop()?.toUpperCase() || 'GLB';
      let fileType: FileType;
      switch (extension) {
        case 'GLB':
          fileType = 'GLB' as FileType;
          break;
        case 'GLTF':
          fileType = 'GLTF' as FileType;
          break;
        case 'OBJ':
          fileType = 'OBJ' as FileType;
          break;
        case 'FBX':
          fileType = 'FBX' as FileType;
          break;
        case 'ZIP':
          fileType = 'GLB' as FileType;
          break;
        default:
          fileType = 'GLB' as FileType;
      }

      // Convert archiveType to backend format
      let backendArchiveType: ArchiveType | null = null;
      if (archiveType === 'zip') {
        backendArchiveType = 'zip' as ArchiveType;
      }

      // Upload to backend
      await actor.saveFile(
        id,
        externalBlob,
        file.name,
        BigInt(file.size),
        fileType,
        BigInt(Date.now() * 1000000),
        extractionSource || null,
        relativePath,
        isDirectory,
        backendArchiveType
      );

      return id;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['files'] });
    },
    onError: (error) => {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to upload file: ${errorMessage}`);
    },
  });
}

export function useDeleteFile() {
  const { actor, isReady } = useOfflineActor();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: Uint8Array) => {
      if (!actor || !isReady) throw new Error('Backend not available in offline mode');
      await actor.deleteFile(id);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['files'] });
    },
    onError: (error) => {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to delete file: ${errorMessage}`);
    },
  });
}

// User profile hooks
export function useGetCallerUserProfile() {
  const { actor, isReady } = useOfflineActor();

  const query = useQuery({
    queryKey: ['currentUserProfile'],
    queryFn: async () => {
      if (!actor) throw new Error('Actor not available');
      return actor.getCallerUserProfile();
    },
    enabled: !!actor && isReady,
    retry: false,
  });

  return {
    ...query,
    isLoading: !isReady || query.isLoading,
    isFetched: isReady && query.isFetched,
  };
}

export function useSaveCallerUserProfile() {
  const { actor, isReady } = useOfflineActor();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (profile: { name: string }) => {
      if (!actor || !isReady) throw new Error('Backend not available in offline mode');
      await actor.saveCallerUserProfile(profile);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['currentUserProfile'] });
    },
  });
}
