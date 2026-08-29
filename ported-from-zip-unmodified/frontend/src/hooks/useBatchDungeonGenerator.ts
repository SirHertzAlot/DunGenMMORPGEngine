import { useCallback, useRef, useState } from "react";
import {
  type DungeonGenerationParams,
  generateDungeon,
} from "../lib/rotjsDungeonGenerator";
import type { DungeonLibraryEntry } from "../types/dungeon3d";

export interface BatchGenerationState {
  isGenerating: boolean;
  progress: number;
  total: number;
}

export interface UseBatchDungeonGeneratorReturn {
  batchState: BatchGenerationState;
  generateBatch: (
    count: number,
    baseParams: DungeonGenerationParams,
  ) => Promise<DungeonLibraryEntry[]>;
  cancelBatch: () => void;
}

export function useBatchDungeonGenerator(): UseBatchDungeonGeneratorReturn {
  const [batchState, setBatchState] = useState<BatchGenerationState>({
    isGenerating: false,
    progress: 0,
    total: 0,
  });

  const cancelRef = useRef(false);

  const cancelBatch = useCallback(() => {
    cancelRef.current = true;
  }, []);

  const generateBatch = useCallback(
    async (
      count: number,
      baseParams: DungeonGenerationParams,
    ): Promise<DungeonLibraryEntry[]> => {
      cancelRef.current = false;
      setBatchState({ isGenerating: true, progress: 0, total: count });

      const results: DungeonLibraryEntry[] = [];

      for (let i = 0; i < count; i++) {
        if (cancelRef.current) break;

        // Use a unique random seed for each dungeon
        const seed = Math.floor(Math.random() * 10_000_000);
        const params: DungeonGenerationParams = { ...baseParams, seed };

        // Yield to the browser event loop every 5 dungeons to keep UI responsive
        if (i > 0 && i % 5 === 0) {
          await new Promise<void>((resolve) => setTimeout(resolve, 0));
        }

        const dungeon = generateDungeon(params);

        const entry: DungeonLibraryEntry = {
          id: `dungeon-${Date.now()}-${i}-${seed}`,
          label: `Dungeon #${i + 1}`,
          seed,
          dungeon,
          generatedAt: Date.now(),
        };

        results.push(entry);
        setBatchState({ isGenerating: true, progress: i + 1, total: count });
      }

      setBatchState({ isGenerating: false, progress: 0, total: 0 });
      return results;
    },
    [],
  );

  return { batchState, generateBatch, cancelBatch };
}
