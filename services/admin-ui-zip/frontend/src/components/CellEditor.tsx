import { useState, useEffect } from 'react';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent } from '@/components/ui/sheet';
import { Layers, Settings } from 'lucide-react';
import TerrainScene from './TerrainScene';
import ControlPanel from './ControlPanel';
import type { GridConfig, CellConfig } from '../types/grid';
import type { TilesetConfig } from '../lib/wfcDungeon';
import type { HydraulicErosionParams, ThermalErosionParams, WindErosionParams, PlateauErosionParams, RiverErosionParams } from '../lib/erosion';

interface CellEditorProps {
  gridConfig: GridConfig;
  selectedCell: { row: number; col: number };
  onCellSelect: (row: number, col: number) => void;
  onCellUpdate: (row: number, col: number, config: CellConfig) => void;
  tilesetConfig: TilesetConfig | null;
}

export default function CellEditor({ gridConfig, selectedCell, onCellSelect, onCellUpdate, tilesetConfig }: CellEditorProps) {
  const config = gridConfig.cells[selectedCell.row][selectedCell.col];
  const [localConfig, setLocalConfig] = useState<CellConfig>(config);
  const [applyHydraulicTrigger, setApplyHydraulicTrigger] = useState(0);
  const [applyThermalTrigger, setApplyThermalTrigger] = useState(0);
  const [applyWindTrigger, setApplyWindTrigger] = useState(0);
  const [applyPlateauTrigger, setApplyPlateauTrigger] = useState(0);
  const [applyRiverTrigger, setApplyRiverTrigger] = useState(0);
  const [resetRiverTrigger, setResetRiverTrigger] = useState(0);
  const [isControlsOpen, setIsControlsOpen] = useState(false);
  const [isCellListOpen, setIsCellListOpen] = useState(false);

  useEffect(() => {
    setLocalConfig(config);
  }, [config, selectedCell]);

  const handleNoiseParamChange = (param: keyof CellConfig['noiseSettings'], value: number) => {
    const newConfig = {
      ...localConfig,
      noiseSettings: {
        ...localConfig.noiseSettings,
        [param]: value,
      },
    };
    setLocalConfig(newConfig);
    onCellUpdate(selectedCell.row, selectedCell.col, newConfig);
  };

  const handleHydraulicParamChange = (param: keyof HydraulicErosionParams, value: number) => {
    const newConfig = {
      ...localConfig,
      erosionSettings: {
        ...localConfig.erosionSettings,
        hydraulic: {
          ...localConfig.erosionSettings.hydraulic,
          [param]: value,
        },
      },
    };
    setLocalConfig(newConfig);
    onCellUpdate(selectedCell.row, selectedCell.col, newConfig);
  };

  const handleThermalParamChange = (param: keyof ThermalErosionParams, value: number) => {
    const newConfig = {
      ...localConfig,
      erosionSettings: {
        ...localConfig.erosionSettings,
        thermal: {
          ...localConfig.erosionSettings.thermal,
          [param]: value,
        },
      },
    };
    setLocalConfig(newConfig);
    onCellUpdate(selectedCell.row, selectedCell.col, newConfig);
  };

  const handleWindParamChange = (param: keyof WindErosionParams, value: number) => {
    const newConfig = {
      ...localConfig,
      erosionSettings: {
        ...localConfig.erosionSettings,
        wind: {
          ...localConfig.erosionSettings.wind,
          [param]: value,
        },
      },
    };
    setLocalConfig(newConfig);
    onCellUpdate(selectedCell.row, selectedCell.col, newConfig);
  };

  const handlePlateauParamChange = (param: keyof PlateauErosionParams, value: number) => {
    const newConfig = {
      ...localConfig,
      erosionSettings: {
        ...localConfig.erosionSettings,
        plateau: {
          ...localConfig.erosionSettings.plateau,
          [param]: value,
        },
      },
    };
    setLocalConfig(newConfig);
    onCellUpdate(selectedCell.row, selectedCell.col, newConfig);
  };

  const handleRiverParamChange = (param: keyof RiverErosionParams, value: number) => {
    const newConfig = {
      ...localConfig,
      erosionSettings: {
        ...localConfig.erosionSettings,
        river: {
          ...localConfig.erosionSettings.river,
          [param]: value,
        },
      },
    };
    setLocalConfig(newConfig);
    onCellUpdate(selectedCell.row, selectedCell.col, newConfig);
  };

  // Get all cells except the selected one
  const otherCells = gridConfig.cells.flatMap((row, rowIndex) =>
    row.map((cell, colIndex) => ({
      row: rowIndex,
      col: colIndex,
      cell,
    }))
  ).filter(({ row, col }) => !(row === selectedCell.row && col === selectedCell.col));

  const handleCellSelectFromList = (row: number, col: number) => {
    onCellSelect(row, col);
    setIsCellListOpen(false);
  };

  const isDungeonMode = gridConfig.mode === 'dungeon';

  return (
    <div className="flex h-full w-full flex-col overflow-hidden lg:flex-row">
      {/* Desktop: Scrollable Cell List Sidebar */}
      <aside className="hidden w-64 border-r border-border bg-card lg:block xl:w-72">
        <div className="border-b border-border p-4">
          <h3 className="text-sm font-semibold">Other Cells</h3>
          <p className="mt-1 text-xs text-muted-foreground">
            Click to switch focus
          </p>
        </div>
        <ScrollArea className="h-[calc(100%-5rem)]">
          <div className="space-y-2 p-4">
            {otherCells.map(({ row, col, cell }) => (
              <Card
                key={`${row}-${col}`}
                className="cursor-pointer transition-all hover:bg-accent hover:shadow-md active:scale-95"
                onClick={() => onCellSelect(row, col)}
              >
                <div className="p-3">
                  <div className="mb-1 text-sm font-mono font-semibold">
                    Cell [{row}, {col}]
                  </div>
                  <div className="space-y-0.5">
                    {isDungeonMode ? (
                      <>
                        <div className="text-xs text-muted-foreground">
                          Type: {cell.dungeonSettings?.tileType || 'room'}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          Model: {cell.dungeonSettings?.selectedModel || 'none'}
                        </div>
                      </>
                    ) : (
                      <>
                        <div className="text-xs text-muted-foreground">
                          Scale: {cell.noiseSettings.noiseScale.toFixed(3)}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          Elevation: {cell.noiseSettings.elevation.toFixed(1)}
                        </div>
                      </>
                    )}
                  </div>
                </div>
              </Card>
            ))}
          </div>
        </ScrollArea>
      </aside>

      {/* Mobile: Cell List Sheet */}
      <Sheet open={isCellListOpen} onOpenChange={setIsCellListOpen}>
        <SheetContent side="left" className="w-64 p-0 lg:hidden">
          <div className="border-b border-border p-4">
            <h3 className="text-sm font-semibold">Other Cells</h3>
            <p className="mt-1 text-xs text-muted-foreground">
              Click to switch focus
            </p>
          </div>
          <ScrollArea className="h-[calc(100%-5rem)]">
            <div className="space-y-2 p-4">
              {otherCells.map(({ row, col, cell }) => (
                <Card
                  key={`${row}-${col}`}
                  className="cursor-pointer transition-all hover:bg-accent hover:shadow-md active:scale-95"
                  onClick={() => handleCellSelectFromList(row, col)}
                >
                  <div className="p-3">
                    <div className="mb-1 text-sm font-mono font-semibold">
                      Cell [{row}, {col}]
                    </div>
                    <div className="space-y-0.5">
                      {isDungeonMode ? (
                        <>
                          <div className="text-xs text-muted-foreground">
                            Type: {cell.dungeonSettings?.tileType || 'room'}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            Model: {cell.dungeonSettings?.selectedModel || 'none'}
                          </div>
                        </>
                      ) : (
                        <>
                          <div className="text-xs text-muted-foreground">
                            Scale: {cell.noiseSettings.noiseScale.toFixed(3)}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            Elevation: {cell.noiseSettings.elevation.toFixed(1)}
                          </div>
                        </>
                      )}
                    </div>
                  </div>
                </Card>
              ))}
            </div>
          </ScrollArea>
        </SheetContent>
      </Sheet>

      {/* Main Editor Area */}
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
        {/* Selected Cell Header with Mobile Actions */}
        <div className="border-b border-border bg-card p-3 sm:p-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-base font-semibold sm:text-lg">
                Editing {isDungeonMode ? 'Dungeon' : 'Terrain'} Cell [{selectedCell.row}, {selectedCell.col}]
              </h2>
              <p className="hidden text-sm text-muted-foreground sm:block">
                {isDungeonMode 
                  ? 'Configure dungeon tile model and rotation'
                  : 'Adjust terrain parameters and apply erosion effects'}
              </p>
            </div>
            <div className="flex gap-2 lg:hidden">
              <Button
                variant="outline"
                size="icon"
                onClick={() => setIsCellListOpen(true)}
              >
                <Layers className="h-4 w-4" />
              </Button>
              <Button
                variant="outline"
                size="icon"
                onClick={() => setIsControlsOpen(true)}
              >
                <Settings className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </div>

        {/* Content Area */}
        <div className="flex min-h-0 flex-1 flex-col overflow-hidden lg:flex-row">
          {/* Preview - Full width on mobile, flex on desktop */}
          <div className="relative flex min-h-0 flex-1 overflow-hidden">
            {isDungeonMode ? (
              <div className="flex items-center justify-center w-full h-full bg-muted/30">
                <div className="text-center space-y-2 p-8">
                  <p className="text-sm text-muted-foreground">
                    Dungeon cell preview
                  </p>
                  <p className="text-xs text-muted-foreground">
                    Model: {localConfig.dungeonSettings?.selectedModel || 'None selected'}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    Rotation: {localConfig.dungeonSettings?.rotation || 0}°
                  </p>
                </div>
              </div>
            ) : (
              <TerrainScene
                params={localConfig.noiseSettings}
                hydraulicParams={localConfig.erosionSettings.hydraulic}
                thermalParams={localConfig.erosionSettings.thermal}
                windParams={localConfig.erosionSettings.wind}
                plateauParams={localConfig.erosionSettings.plateau}
                riverParams={localConfig.erosionSettings.river}
                applyHydraulicTrigger={applyHydraulicTrigger}
                applyThermalTrigger={applyThermalTrigger}
                applyWindTrigger={applyWindTrigger}
                applyPlateauTrigger={applyPlateauTrigger}
                applyRiverTrigger={applyRiverTrigger}
                resetRiverTrigger={resetRiverTrigger}
              />
            )}
          </div>

          {/* Desktop: Control Panel Sidebar */}
          <aside className="hidden w-80 border-l border-border lg:block xl:w-96">
            <ScrollArea className="h-full">
              {isDungeonMode ? (
                <div className="p-6 space-y-4">
                  <div>
                    <h3 className="text-lg font-semibold mb-2">Dungeon Cell Configuration</h3>
                    <p className="text-sm text-muted-foreground">
                      Configure tile model and rotation for this dungeon cell
                    </p>
                  </div>
                  <div className="rounded-lg border border-border bg-muted/30 p-4">
                    <p className="text-sm text-muted-foreground">
                      Dungeon configuration controls will be available here
                    </p>
                  </div>
                </div>
              ) : (
                <ControlPanel
                  params={localConfig.noiseSettings}
                  onParamChange={handleNoiseParamChange}
                  hydraulicParams={localConfig.erosionSettings.hydraulic}
                  onHydraulicParamChange={handleHydraulicParamChange}
                  thermalParams={localConfig.erosionSettings.thermal}
                  onThermalParamChange={handleThermalParamChange}
                  windParams={localConfig.erosionSettings.wind}
                  onWindParamChange={handleWindParamChange}
                  plateauParams={localConfig.erosionSettings.plateau}
                  onPlateauParamChange={handlePlateauParamChange}
                  riverParams={localConfig.erosionSettings.river}
                  onRiverParamChange={handleRiverParamChange}
                  onApplyHydraulicErosion={() => setApplyHydraulicTrigger((p) => p + 1)}
                  onApplyThermalErosion={() => setApplyThermalTrigger((p) => p + 1)}
                  onApplyWindErosion={() => setApplyWindTrigger((p) => p + 1)}
                  onApplyPlateauErosion={() => setApplyPlateauTrigger((p) => p + 1)}
                  onApplyRiverErosion={() => setApplyRiverTrigger((p) => p + 1)}
                  onResetRiverErosion={() => setResetRiverTrigger((p) => p + 1)}
                />
              )}
            </ScrollArea>
          </aside>

          {/* Mobile: Control Panel Sheet */}
          <Sheet open={isControlsOpen} onOpenChange={setIsControlsOpen}>
            <SheetContent side="right" className="w-full p-0 sm:w-96 lg:hidden">
              <ScrollArea className="h-full">
                {isDungeonMode ? (
                  <div className="p-6 space-y-4">
                    <div>
                      <h3 className="text-lg font-semibold mb-2">Dungeon Cell Configuration</h3>
                      <p className="text-sm text-muted-foreground">
                        Configure tile model and rotation for this dungeon cell
                      </p>
                    </div>
                    <div className="rounded-lg border border-border bg-muted/30 p-4">
                      <p className="text-sm text-muted-foreground">
                        Dungeon configuration controls will be available here
                      </p>
                    </div>
                  </div>
                ) : (
                  <ControlPanel
                    params={localConfig.noiseSettings}
                    onParamChange={handleNoiseParamChange}
                    hydraulicParams={localConfig.erosionSettings.hydraulic}
                    onHydraulicParamChange={handleHydraulicParamChange}
                    thermalParams={localConfig.erosionSettings.thermal}
                    onThermalParamChange={handleThermalParamChange}
                    windParams={localConfig.erosionSettings.wind}
                    onWindParamChange={handleWindParamChange}
                    plateauParams={localConfig.erosionSettings.plateau}
                    onPlateauParamChange={handlePlateauParamChange}
                    riverParams={localConfig.erosionSettings.river}
                    onRiverParamChange={handleRiverParamChange}
                    onApplyHydraulicErosion={() => {
                      setApplyHydraulicTrigger((p) => p + 1);
                      setIsControlsOpen(false);
                    }}
                    onApplyThermalErosion={() => {
                      setApplyThermalTrigger((p) => p + 1);
                      setIsControlsOpen(false);
                    }}
                    onApplyWindErosion={() => {
                      setApplyWindTrigger((p) => p + 1);
                      setIsControlsOpen(false);
                    }}
                    onApplyPlateauErosion={() => {
                      setApplyPlateauTrigger((p) => p + 1);
                      setIsControlsOpen(false);
                    }}
                    onApplyRiverErosion={() => {
                      setApplyRiverTrigger((p) => p + 1);
                      setIsControlsOpen(false);
                    }}
                    onResetRiverErosion={() => {
                      setResetRiverTrigger((p) => p + 1);
                      setIsControlsOpen(false);
                    }}
                  />
                )}
              </ScrollArea>
            </SheetContent>
          </Sheet>
        </div>
      </div>
    </div>
  );
}
