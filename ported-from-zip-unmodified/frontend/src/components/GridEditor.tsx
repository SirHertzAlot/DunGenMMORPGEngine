import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Grid3x3, Plus, Save, FolderOpen, Download, Upload } from 'lucide-react';
import CellEditor from './CellEditor';
import GridView from './GridView';
import { useSaveGrid, useLoadGrid } from '../hooks/useQueries';
import { toast } from 'sonner';
import type { GridConfig, CellConfig } from '../types/grid';
import { exportGridData } from '../lib/exportGrid';
import { parseTilesetYAML, type TilesetConfig } from '../lib/wfcDungeon';
import { debugLogger } from '../lib/debugLogger';

interface GridEditorProps {
  mode: 'terrain' | 'dungeon';
}

export default function GridEditor({ mode }: GridEditorProps) {
  const [gridDimension, setGridDimension] = useState(3);
  const [gridConfig, setGridConfig] = useState<GridConfig | null>(null);
  const [selectedCell, setSelectedCell] = useState<{ row: number; col: number } | null>(null);
  const [showNewGridDialog, setShowNewGridDialog] = useState(true);
  const [gridId, setGridId] = useState<string>('');
  const [loadGridId, setLoadGridId] = useState<string>('');
  const [tilesetConfig, setTilesetConfig] = useState<TilesetConfig | null>(null);

  const saveGridMutation = useSaveGrid();
  const { data: loadedGrid, refetch: loadGrid } = useLoadGrid(loadGridId);

  const handleTilesetUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    debugLogger.info('GridEditor', `Loading tileset YAML: ${file.name}`);

    try {
      const text = await file.text();
      const config = parseTilesetYAML(text);
      setTilesetConfig(config);
      
      toast.success(`Tileset loaded: ${Object.keys(config.tiles).length} tiles`);
      debugLogger.success('GridEditor', `Tileset loaded successfully`);
    } catch (err: any) {
      const errorMsg = `Failed to parse tileset YAML: ${err.message}`;
      toast.error(errorMsg);
      debugLogger.error('GridEditor', errorMsg);
    }
  };

  const handleCreateGrid = () => {
    if (gridDimension < 1 || gridDimension > 10) {
      toast.error('Grid dimension must be between 1 and 10');
      return;
    }

    if (mode === 'dungeon' && !tilesetConfig) {
      toast.error('Please upload a tileset configuration for dungeon mode');
      return;
    }

    const newGrid: GridConfig = {
      id: gridId || `${mode}_grid_${Date.now()}`,
      dimension: gridDimension,
      mode,
      cells: Array(gridDimension).fill(null).map(() =>
        Array(gridDimension).fill(null).map(() => createDefaultCellConfig(mode))
      ),
    };

    setGridConfig(newGrid);
    setShowNewGridDialog(false);
    toast.success(`Created ${gridDimension}×${gridDimension} ${mode} grid`);
  };

  const handleLoadGrid = async () => {
    if (!loadGridId.trim()) {
      toast.error('Please enter a grid ID');
      return;
    }

    const result = await loadGrid();
    if (result.data) {
      setGridConfig({
        id: loadGridId,
        dimension: result.data.length,
        mode,
        cells: result.data,
      });
      setShowNewGridDialog(false);
      toast.success('Grid loaded successfully');
    } else {
      toast.error('Grid not found');
    }
  };

  const handleSaveGrid = async () => {
    if (!gridConfig) return;

    try {
      await saveGridMutation.mutateAsync({
        id: gridConfig.id,
        cells: gridConfig.cells,
      });
      toast.success('Grid saved successfully');
    } catch (error) {
      toast.error('Failed to save grid');
      console.error(error);
    }
  };

  const handleExportGrid = () => {
    if (!gridConfig) return;
    exportGridData(gridConfig);
    toast.success('Grid exported successfully');
  };

  const handleCellClick = (row: number, col: number) => {
    setSelectedCell({ row, col });
  };

  const handleCellUpdate = (row: number, col: number, config: CellConfig) => {
    if (!gridConfig) return;

    const newCells = gridConfig.cells.map((rowCells, r) =>
      rowCells.map((cell, c) => (r === row && c === col ? config : cell))
    );

    setGridConfig({
      ...gridConfig,
      cells: newCells,
    });
  };

  return (
    <div className="flex h-full w-full flex-col overflow-hidden">
      {/* Top Control Bar - Responsive */}
      {gridConfig && (
        <div className="border-b border-border bg-card p-3 sm:p-4">
          <div className="container mx-auto flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-2">
              <Grid3x3 className="h-4 w-4 text-primary sm:h-5 sm:w-5" />
              <span className="text-sm font-semibold sm:text-base">
                {mode === 'terrain' ? 'Terrain' : 'Dungeon'} Grid: {gridConfig.dimension}×{gridConfig.dimension}
              </span>
              <span className="hidden text-sm text-muted-foreground sm:inline">({gridConfig.id})</span>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setShowNewGridDialog(true)}
                className="flex-1 sm:flex-none"
              >
                <Plus className="mr-2 h-4 w-4" />
                <span className="hidden sm:inline">New Grid</span>
                <span className="sm:hidden">New</span>
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={handleSaveGrid}
                disabled={saveGridMutation.isPending}
                className="flex-1 sm:flex-none"
              >
                <Save className="mr-2 h-4 w-4" />
                <span className="hidden sm:inline">Save Grid</span>
                <span className="sm:hidden">Save</span>
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={handleExportGrid}
                className="flex-1 sm:flex-none"
              >
                <Download className="mr-2 h-4 w-4" />
                <span className="hidden sm:inline">Export</span>
                <span className="sm:hidden">Export</span>
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Main Content */}
      <div className="flex min-h-0 flex-1 overflow-hidden">
        {gridConfig ? (
          selectedCell ? (
            <CellEditor
              gridConfig={gridConfig}
              selectedCell={selectedCell}
              onCellSelect={handleCellClick}
              onCellUpdate={handleCellUpdate}
              tilesetConfig={tilesetConfig}
            />
          ) : (
            <div className="flex flex-1 flex-col items-center justify-center overflow-hidden">
              <div className="w-full max-w-7xl px-2 py-4 sm:px-4 sm:py-6">
                <Card className="mb-4">
                  <CardHeader className="pb-3">
                    <CardTitle className="text-base sm:text-lg">Select a Cell</CardTitle>
                    <CardDescription className="text-xs sm:text-sm">
                      Click on any cell below to start editing its {mode === 'terrain' ? 'terrain' : 'dungeon configuration'}
                    </CardDescription>
                  </CardHeader>
                </Card>
                <div className="overflow-auto rounded-lg border border-border bg-card">
                  <GridView
                    config={gridConfig}
                    selectedCell={selectedCell}
                    onCellClick={handleCellClick}
                  />
                </div>
              </div>
            </div>
          )
        ) : (
          <div className="flex flex-1 items-center justify-center p-4 sm:p-6 md:p-8">
            <Card className="w-full max-w-md">
              <CardHeader>
                <CardTitle className="text-lg sm:text-xl">No Grid Loaded</CardTitle>
                <CardDescription className="text-sm">
                  Create a new {mode} grid or load an existing one to get started
                </CardDescription>
              </CardHeader>
            </Card>
          </div>
        )}
      </div>

      {/* New/Load Grid Dialog - Responsive */}
      <Dialog open={showNewGridDialog} onOpenChange={setShowNewGridDialog}>
        <DialogContent className="max-w-[calc(100vw-2rem)] sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="text-lg sm:text-xl">{mode === 'terrain' ? 'Terrain' : 'Dungeon'} Grid Configuration</DialogTitle>
            <DialogDescription className="text-sm">
              Create a new {mode} grid or load an existing one
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-6 py-4">
            {mode === 'dungeon' && (
              <div className="space-y-4">
                <h3 className="text-sm font-semibold">Tileset Configuration</h3>
                <div className="space-y-2">
                  <Label htmlFor="tileset-upload" className="text-sm">YAML Tileset File</Label>
                  <div className="flex gap-2">
                    <input
                      id="tileset-upload"
                      type="file"
                      accept=".yaml,.yml"
                      onChange={handleTilesetUpload}
                      className="hidden"
                    />
                    <Button
                      variant="outline"
                      className="w-full"
                      onClick={() => document.getElementById('tileset-upload')?.click()}
                    >
                      <Upload className="mr-2 h-4 w-4" />
                      {tilesetConfig ? 'Change Tileset' : 'Upload Tileset'}
                    </Button>
                  </div>
                  {tilesetConfig && (
                    <div className="rounded-md border border-green-500/20 bg-green-500/10 p-2">
                      <p className="text-xs text-green-600 dark:text-green-400">
                        ✓ Tileset loaded: {Object.keys(tilesetConfig.tiles).length} tiles
                      </p>
                    </div>
                  )}
                  <p className="text-xs text-muted-foreground">
                    Required for dungeon generation
                  </p>
                </div>
              </div>
            )}

            <div className="space-y-4">
              <h3 className="text-sm font-semibold">Create New Grid</h3>
              <div className="space-y-2">
                <Label htmlFor="gridId" className="text-sm">Grid ID (optional)</Label>
                <Input
                  id="gridId"
                  placeholder={`my-${mode}-grid`}
                  value={gridId}
                  onChange={(e) => setGridId(e.target.value)}
                  className="text-base"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="dimension" className="text-sm">Grid Dimension (n×n)</Label>
                <Input
                  id="dimension"
                  type="number"
                  min={1}
                  max={10}
                  value={gridDimension}
                  onChange={(e) => setGridDimension(parseInt(e.target.value) || 1)}
                  className="text-base"
                />
                <p className="text-xs text-muted-foreground">
                  Creates a {gridDimension}×{gridDimension} grid ({gridDimension * gridDimension} cells)
                </p>
              </div>
              <Button 
                onClick={handleCreateGrid} 
                className="w-full" 
                size="lg"
                disabled={mode === 'dungeon' && !tilesetConfig}
              >
                <Plus className="mr-2 h-4 w-4" />
                Create {mode === 'terrain' ? 'Terrain' : 'Dungeon'} Grid
              </Button>
            </div>

            <div className="relative">
              <div className="absolute inset-0 flex items-center">
                <span className="w-full border-t" />
              </div>
              <div className="relative flex justify-center text-xs uppercase">
                <span className="bg-background px-2 text-muted-foreground">Or</span>
              </div>
            </div>

            <div className="space-y-4">
              <h3 className="text-sm font-semibold">Load Existing Grid</h3>
              <div className="space-y-2">
                <Label htmlFor="loadGridId" className="text-sm">Grid ID</Label>
                <Input
                  id="loadGridId"
                  placeholder="Enter grid ID"
                  value={loadGridId}
                  onChange={(e) => setLoadGridId(e.target.value)}
                  className="text-base"
                />
              </div>
              <Button onClick={handleLoadGrid} variant="secondary" className="w-full" size="lg">
                <FolderOpen className="mr-2 h-4 w-4" />
                Load Grid
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function createDefaultCellConfig(mode: 'terrain' | 'dungeon'): CellConfig {
  if (mode === 'dungeon') {
    return {
      mode: 'dungeon',
      dungeonSettings: {
        selectedModel: null,
        rotation: 0,
        tileType: 'room',
      },
      noiseSettings: {
        noiseScale: 0.1,
        elevation: 15,
        octaves: 4,
        persistence: 0.5,
      },
      erosionSettings: {
        hydraulic: { iterations: 100, strength: 0.5, sedimentCapacity: 0.5 },
        thermal: { iterations: 50, strength: 0.3, talusAngle: 0.7 },
        wind: { iterations: 100, direction: 45, transportRate: 0.5 },
        plateau: { iterations: 50, threshold: 0.7, strength: 0.5 },
        river: {
          iterations: 200,
          flowDirectionBias: 180,
          rainfallSourcePoints: 5,
          erosionDepositionRate: 0.5,
          evaporationRate: 0.3,
          poolingThreshold: 0.4,
        },
      },
      lightingSettings: {
        ambientIntensity: 0.4,
        directionalIntensity: 0.8,
      },
    };
  }

  return {
    mode: 'terrain',
    noiseSettings: {
      noiseScale: 0.1,
      elevation: 15,
      octaves: 4,
      persistence: 0.5,
    },
    erosionSettings: {
      hydraulic: { iterations: 100, strength: 0.5, sedimentCapacity: 0.5 },
      thermal: { iterations: 50, strength: 0.3, talusAngle: 0.7 },
      wind: { iterations: 100, direction: 45, transportRate: 0.5 },
      plateau: { iterations: 50, threshold: 0.7, strength: 0.5 },
      river: {
        iterations: 200,
        flowDirectionBias: 180,
        rainfallSourcePoints: 5,
        erosionDepositionRate: 0.5,
        evaporationRate: 0.3,
        poolingThreshold: 0.4,
      },
    },
    lightingSettings: {
      ambientIntensity: 0.4,
      directionalIntensity: 0.8,
    },
  };
}
