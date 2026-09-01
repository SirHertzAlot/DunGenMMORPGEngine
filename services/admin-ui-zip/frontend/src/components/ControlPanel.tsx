import { Label } from '@/components/ui/label';
import { Slider } from '@/components/ui/slider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Droplets, Mountain, Wind, Layers, Waves, RotateCcw } from 'lucide-react';
import type { CellConfig } from '../types/grid';
import type { HydraulicErosionParams, ThermalErosionParams, WindErosionParams, PlateauErosionParams, RiverErosionParams } from '../lib/erosion';

interface ControlPanelProps {
  params: CellConfig['noiseSettings'];
  onParamChange: (param: keyof CellConfig['noiseSettings'], value: number) => void;
  hydraulicParams: HydraulicErosionParams;
  onHydraulicParamChange: (param: keyof HydraulicErosionParams, value: number) => void;
  thermalParams: ThermalErosionParams;
  onThermalParamChange: (param: keyof ThermalErosionParams, value: number) => void;
  windParams: WindErosionParams;
  onWindParamChange: (param: keyof WindErosionParams, value: number) => void;
  plateauParams: PlateauErosionParams;
  onPlateauParamChange: (param: keyof PlateauErosionParams, value: number) => void;
  riverParams: RiverErosionParams;
  onRiverParamChange: (param: keyof RiverErosionParams, value: number) => void;
  onApplyHydraulicErosion: () => void;
  onApplyThermalErosion: () => void;
  onApplyWindErosion: () => void;
  onApplyPlateauErosion: () => void;
  onApplyRiverErosion: () => void;
  onResetRiverErosion: () => void;
}

export default function ControlPanel({ 
  params, 
  onParamChange, 
  hydraulicParams,
  onHydraulicParamChange,
  thermalParams,
  onThermalParamChange,
  windParams,
  onWindParamChange,
  plateauParams,
  onPlateauParamChange,
  riverParams,
  onRiverParamChange,
  onApplyHydraulicErosion,
  onApplyThermalErosion,
  onApplyWindErosion,
  onApplyPlateauErosion,
  onApplyRiverErosion,
  onResetRiverErosion
}: ControlPanelProps) {
  return (
    <div className="h-full overflow-y-auto bg-card">
      <div className="space-y-4 p-4 sm:space-y-6 sm:p-6">
        <div>
          <h2 className="text-xl font-bold tracking-tight sm:text-2xl">Terrain Controls</h2>
          <p className="mt-1 text-xs text-muted-foreground sm:text-sm">
            Adjust parameters to generate unique landscapes in real-time
          </p>
        </div>

        <Separator />

        <Card>
          <CardHeader className="p-4 sm:p-6">
            <CardTitle className="text-sm sm:text-base">Noise Scale</CardTitle>
            <CardDescription className="text-xs sm:text-sm">
              Controls the frequency of terrain features. Lower values create larger, smoother hills.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
            <div className="flex items-center justify-between">
              <Label className="text-xs font-medium sm:text-sm">Scale</Label>
              <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                {params.noiseScale.toFixed(3)}
              </span>
            </div>
            <Slider
              value={[params.noiseScale]}
              onValueChange={([value]) => onParamChange('noiseScale', value)}
              min={0.01}
              max={0.3}
              step={0.001}
              className="w-full touch-none"
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="p-4 sm:p-6">
            <CardTitle className="text-sm sm:text-base">Elevation</CardTitle>
            <CardDescription className="text-xs sm:text-sm">
              Multiplier for terrain height. Higher values create more dramatic mountains.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
            <div className="flex items-center justify-between">
              <Label className="text-xs font-medium sm:text-sm">Height</Label>
              <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                {params.elevation.toFixed(1)}
              </span>
            </div>
            <Slider
              value={[params.elevation]}
              onValueChange={([value]) => onParamChange('elevation', value)}
              min={5}
              max={40}
              step={0.5}
              className="w-full touch-none"
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="p-4 sm:p-6">
            <CardTitle className="text-sm sm:text-base">Octaves</CardTitle>
            <CardDescription className="text-xs sm:text-sm">
              Number of noise layers. More octaves add finer detail to the terrain.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
            <div className="flex items-center justify-between">
              <Label className="text-xs font-medium sm:text-sm">Layers</Label>
              <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                {params.octaves}
              </span>
            </div>
            <Slider
              value={[params.octaves]}
              onValueChange={([value]) => onParamChange('octaves', Math.round(value))}
              min={1}
              max={8}
              step={1}
              className="w-full touch-none"
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="p-4 sm:p-6">
            <CardTitle className="text-sm sm:text-base">Persistence</CardTitle>
            <CardDescription className="text-xs sm:text-sm">
              Controls how much each octave contributes. Higher values create rougher terrain.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
            <div className="flex items-center justify-between">
              <Label className="text-xs font-medium sm:text-sm">Roughness</Label>
              <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                {params.persistence.toFixed(2)}
              </span>
            </div>
            <Slider
              value={[params.persistence]}
              onValueChange={([value]) => onParamChange('persistence', value)}
              min={0.1}
              max={0.9}
              step={0.05}
              className="w-full touch-none"
            />
          </CardContent>
        </Card>

        <Separator className="my-4 sm:my-6" />

        <div>
          <h3 className="mb-1 text-lg font-bold tracking-tight sm:text-xl">Erosion Simulation</h3>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Apply realistic erosion effects to the generated terrain
          </p>
        </div>

        {/* Hydraulic Erosion Section */}
        <div className="space-y-3 sm:space-y-4">
          <div className="flex items-center gap-2 text-base font-semibold sm:text-lg">
            <Droplets className="h-4 w-4 text-blue-500 sm:h-5 sm:w-5" />
            <span>Hydraulic Erosion</span>
          </div>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Simulates water flow, sediment transport, and deposition
          </p>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Iterations</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Number of water droplets to simulate. More iterations create stronger effects.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Droplets</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {hydraulicParams.iterations}
                </span>
              </div>
              <Input
                type="number"
                min={0}
                max={1000}
                step={10}
                value={hydraulicParams.iterations}
                onChange={(e) => onHydraulicParamChange('iterations', parseInt(e.target.value) || 0)}
                className="w-full text-base"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Erosion Strength</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Controls how aggressively water erodes the terrain
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Strength</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {hydraulicParams.strength.toFixed(2)}
                </span>
              </div>
              <Slider
                value={[hydraulicParams.strength]}
                onValueChange={([value]) => onHydraulicParamChange('strength', value)}
                min={0}
                max={1}
                step={0.05}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Sediment Capacity</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Amount of material water can carry before depositing
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Capacity</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {hydraulicParams.sedimentCapacity.toFixed(2)}
                </span>
              </div>
              <Slider
                value={[hydraulicParams.sedimentCapacity]}
                onValueChange={([value]) => onHydraulicParamChange('sedimentCapacity', value)}
                min={0}
                max={1}
                step={0.05}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Button 
            onClick={onApplyHydraulicErosion} 
            className="w-full"
            size="lg"
          >
            <Droplets className="mr-2 h-4 w-4" />
            Apply Hydraulic Erosion
          </Button>
        </div>

        <Separator className="my-4 sm:my-6" />

        {/* Thermal Erosion Section */}
        <div className="space-y-3 sm:space-y-4">
          <div className="flex items-center gap-2 text-base font-semibold sm:text-lg">
            <Mountain className="h-4 w-4 text-orange-500 sm:h-5 sm:w-5" />
            <span>Thermal Erosion</span>
          </div>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Simulates gravity-based material redistribution from steep slopes
          </p>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Iterations</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Number of simulation steps. More iterations create smoother slopes.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Steps</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {thermalParams.iterations}
                </span>
              </div>
              <Input
                type="number"
                min={0}
                max={500}
                step={10}
                value={thermalParams.iterations}
                onChange={(e) => onThermalParamChange('iterations', parseInt(e.target.value) || 0)}
                className="w-full text-base"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Erosion Strength</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Controls how much material moves per iteration
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Strength</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {thermalParams.strength.toFixed(2)}
                </span>
              </div>
              <Slider
                value={[thermalParams.strength]}
                onValueChange={([value]) => onThermalParamChange('strength', value)}
                min={0}
                max={1}
                step={0.05}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Talus Angle</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Maximum stable slope angle. Lower values create gentler slopes.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Angle</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {thermalParams.talusAngle.toFixed(2)}
                </span>
              </div>
              <Slider
                value={[thermalParams.talusAngle]}
                onValueChange={([value]) => onThermalParamChange('talusAngle', value)}
                min={0.1}
                max={1.5}
                step={0.05}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Button 
            onClick={onApplyThermalErosion} 
            className="w-full"
            size="lg"
          >
            <Mountain className="mr-2 h-4 w-4" />
            Apply Thermal Erosion
          </Button>
        </div>

        <Separator className="my-4 sm:my-6" />

        {/* Wind Erosion Section */}
        <div className="space-y-3 sm:space-y-4">
          <div className="flex items-center gap-2 text-base font-semibold sm:text-lg">
            <Wind className="h-4 w-4 text-cyan-500 sm:h-5 sm:w-5" />
            <span>Wind Erosion</span>
          </div>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Simulates wind-driven sediment transport with directional effects
          </p>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Iterations</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Number of wind simulation steps. More iterations create stronger effects.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Steps</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {windParams.iterations}
                </span>
              </div>
              <Input
                type="number"
                min={0}
                max={500}
                step={10}
                value={windParams.iterations}
                onChange={(e) => onWindParamChange('iterations', parseInt(e.target.value) || 0)}
                className="w-full text-base"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Wind Direction</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Direction of wind flow in degrees (0-360)
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Direction</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {windParams.direction}°
                </span>
              </div>
              <Slider
                value={[windParams.direction]}
                onValueChange={([value]) => onWindParamChange('direction', value)}
                min={0}
                max={360}
                step={15}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Sediment Transport Rate</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Controls how much sediment wind can transport
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Transport Rate</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {windParams.transportRate.toFixed(2)}
                </span>
              </div>
              <Slider
                value={[windParams.transportRate]}
                onValueChange={([value]) => onWindParamChange('transportRate', value)}
                min={0}
                max={1}
                step={0.05}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Button 
            onClick={onApplyWindErosion} 
            className="w-full"
            size="lg"
          >
            <Wind className="mr-2 h-4 w-4" />
            Apply Wind Erosion
          </Button>
        </div>

        <Separator className="my-4 sm:my-6" />

        {/* Plateau Erosion Section */}
        <div className="space-y-3 sm:space-y-4">
          <div className="flex items-center gap-2 text-base font-semibold sm:text-lg">
            <Layers className="h-4 w-4 text-purple-500 sm:h-5 sm:w-5" />
            <span>Plateau Erosion</span>
          </div>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Flattens elevated areas based on height thresholds
          </p>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Iterations</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Number of flattening steps. More iterations create smoother plateaus.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Steps</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {plateauParams.iterations}
                </span>
              </div>
              <Input
                type="number"
                min={0}
                max={500}
                step={10}
                value={plateauParams.iterations}
                onChange={(e) => onPlateauParamChange('iterations', parseInt(e.target.value) || 0)}
                className="w-full text-base"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Plateau Threshold</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Height threshold for plateau formation (0-1 normalized)
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Threshold</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {plateauParams.threshold.toFixed(2)}
                </span>
              </div>
              <Slider
                value={[plateauParams.threshold]}
                onValueChange={([value]) => onPlateauParamChange('threshold', value)}
                min={0}
                max={1}
                step={0.05}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Flattening Strength</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Controls how aggressively plateaus are flattened
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Strength</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {plateauParams.strength.toFixed(2)}
                </span>
              </div>
              <Slider
                value={[plateauParams.strength]}
                onValueChange={([value]) => onPlateauParamChange('strength', value)}
                min={0}
                max={1}
                step={0.05}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Button 
            onClick={onApplyPlateauErosion} 
            className="w-full"
            size="lg"
          >
            <Layers className="mr-2 h-4 w-4" />
            Apply Plateau Erosion
          </Button>
        </div>

        <Separator className="my-4 sm:my-6" />

        {/* River Erosion Section */}
        <div className="space-y-3 sm:space-y-4">
          <div className="flex items-center gap-2 text-base font-semibold sm:text-lg">
            <Waves className="h-4 w-4 text-teal-500 sm:h-5 sm:w-5" />
            <span>River Erosion</span>
          </div>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Simulates water flow, pooling, and sediment transport to form lakes and riverbeds
          </p>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Iterations</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Number of simulation steps. More iterations create more defined river networks.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Steps</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {riverParams.iterations}
                </span>
              </div>
              <Input
                type="number"
                min={0}
                max={500}
                step={10}
                value={riverParams.iterations}
                onChange={(e) => onRiverParamChange('iterations', parseInt(e.target.value) || 0)}
                className="w-full text-base"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Flow Direction Bias</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Preferred direction for water flow in degrees (0-360)
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Direction</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {riverParams.flowDirectionBias}°
                </span>
              </div>
              <Slider
                value={[riverParams.flowDirectionBias]}
                onValueChange={([value]) => onRiverParamChange('flowDirectionBias', value)}
                min={0}
                max={360}
                step={15}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Rainfall Source Points</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Number of starting points for water flow simulation
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Sources</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {riverParams.rainfallSourcePoints}
                </span>
              </div>
              <Input
                type="number"
                min={1}
                max={20}
                step={1}
                value={riverParams.rainfallSourcePoints}
                onChange={(e) => onRiverParamChange('rainfallSourcePoints', parseInt(e.target.value) || 1)}
                className="w-full text-base"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Erosion/Deposition Rate</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Controls how aggressively rivers carve channels and deposit sediment
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Rate</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {riverParams.erosionDepositionRate.toFixed(2)}
                </span>
              </div>
              <Slider
                value={[riverParams.erosionDepositionRate]}
                onValueChange={([value]) => onRiverParamChange('erosionDepositionRate', value)}
                min={0}
                max={1}
                step={0.05}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Evaporation Rate</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Rate at which water evaporates during flow simulation
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Evaporation</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {riverParams.evaporationRate.toFixed(2)}
                </span>
              </div>
              <Slider
                value={[riverParams.evaporationRate]}
                onValueChange={([value]) => onRiverParamChange('evaporationRate', value)}
                min={0}
                max={1}
                step={0.05}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="p-4 sm:p-6">
              <CardTitle className="text-sm sm:text-base">Pooling Threshold</CardTitle>
              <CardDescription className="text-xs sm:text-sm">
                Minimum water accumulation required to form lakes and pools
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-4 sm:space-y-4 sm:p-6 sm:pt-0">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium sm:text-sm">Threshold</Label>
                <span className="text-xs font-mono text-muted-foreground sm:text-sm">
                  {riverParams.poolingThreshold.toFixed(2)}
                </span>
              </div>
              <Slider
                value={[riverParams.poolingThreshold]}
                onValueChange={([value]) => onRiverParamChange('poolingThreshold', value)}
                min={0}
                max={1}
                step={0.05}
                className="w-full touch-none"
              />
            </CardContent>
          </Card>

          <div className="flex gap-2">
            <Button 
              onClick={onApplyRiverErosion} 
              className="flex-1"
              size="lg"
            >
              <Waves className="mr-2 h-4 w-4" />
              Apply River Erosion
            </Button>
            <Button 
              onClick={onResetRiverErosion} 
              variant="outline"
              size="lg"
            >
              <RotateCcw className="h-4 w-4" />
            </Button>
          </div>
        </div>

        <Separator className="my-4 sm:my-6" />

        <div className="rounded-lg border border-border bg-muted/50 p-3 sm:p-4">
          <h3 className="mb-2 text-xs font-semibold sm:text-sm">Color Legend</h3>
          <div className="space-y-2 text-[10px] sm:text-xs">
            <div className="flex items-center gap-2">
              <div className="h-3 w-3 flex-shrink-0 rounded sm:h-4 sm:w-4" style={{ backgroundColor: 'oklch(0.45 0.15 240)' }} />
              <span>Water (low elevation)</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="h-3 w-3 flex-shrink-0 rounded sm:h-4 sm:w-4" style={{ backgroundColor: 'oklch(0.65 0.18 145)' }} />
              <span>Grass (medium elevation)</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="h-3 w-3 flex-shrink-0 rounded sm:h-4 sm:w-4" style={{ backgroundColor: 'oklch(0.55 0.05 270)' }} />
              <span>Mountains (high elevation)</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
