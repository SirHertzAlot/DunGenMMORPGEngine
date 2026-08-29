import { useState } from 'react';
import GridEditor from '../components/GridEditor';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Info, Mountain, Castle } from 'lucide-react';
import DungeonGeneratorPage from './DungeonGeneratorPage';

type GeneratorMode = 'terrain' | 'dungeon';

export default function TerrainGenerator() {
  const [mode, setMode] = useState<GeneratorMode>('dungeon');

  return (
    <div className="flex h-full w-full flex-col overflow-hidden">
      {/* Mode Toggle */}
      <div className="border-b border-border bg-card px-4 py-3 sm:px-6">
        <div className="container mx-auto flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-xl font-bold tracking-tight sm:text-2xl">3D Terrain & Dungeon Generator</h1>
            <p className="text-xs text-muted-foreground sm:text-sm">
              Create procedural terrain or design dungeons with Digger algorithm
            </p>
          </div>
          <Tabs value={mode} onValueChange={(value) => setMode(value as GeneratorMode)} className="w-full sm:w-auto">
            <TabsList className="grid w-full grid-cols-2">
              <TabsTrigger value="terrain" className="flex items-center gap-2">
                <Mountain className="h-4 w-4" />
                <span>Terrain</span>
              </TabsTrigger>
              <TabsTrigger value="dungeon" className="flex items-center gap-2">
                <Castle className="h-4 w-4" />
                <span>Dungeon</span>
              </TabsTrigger>
            </TabsList>
          </Tabs>
        </div>
      </div>

      {/* Offline mode info */}
      <Alert className="m-4 mb-0 rounded-none border-x-0 border-t-0">
        <Info className="h-4 w-4" />
        <AlertDescription>
          {mode === 'terrain' 
            ? 'Terrain editor is running in offline mode with local-only functionality.'
            : 'Dungeon generator uses custom Digger algorithm for procedural generation with 2D/3D preview.'}
        </AlertDescription>
      </Alert>
      
      {mode === 'terrain' ? (
        <GridEditor mode={mode} />
      ) : (
        <DungeonGeneratorPage />
      )}
    </div>
  );
}
