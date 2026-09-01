import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Slider } from "@/components/ui/slider";
import { Dices, RefreshCw } from "lucide-react";
import type { DungeonGenerationParams } from "../lib/rotjsDungeonGenerator";

interface DungeonControlPanelProps {
  params: DungeonGenerationParams;
  onParamChange: (
    param: keyof DungeonGenerationParams,
    value: number | [number, number],
  ) => void;
  onRegenerate: () => void;
  onRandomizeSeed: () => void;
}

export default function DungeonControlPanel({
  params,
  onParamChange,
  onRegenerate,
  onRandomizeSeed,
}: DungeonControlPanelProps) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Card>
          <CardHeader className="p-3">
            <CardTitle className="text-sm">Dungeon Dimensions</CardTitle>
            <CardDescription className="text-xs">
              Overall size of the dungeon grid
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 p-3 pt-0">
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium">Width</Label>
                <span className="text-xs font-mono text-muted-foreground">
                  {params.width}
                </span>
              </div>
              <Slider
                value={[params.width]}
                onValueChange={([value]) => onParamChange("width", value)}
                min={40}
                max={120}
                step={5}
                className="w-full touch-none"
              />
            </div>
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium">Height</Label>
                <span className="text-xs font-mono text-muted-foreground">
                  {params.height}
                </span>
              </div>
              <Slider
                value={[params.height]}
                onValueChange={([value]) => onParamChange("height", value)}
                min={30}
                max={80}
                step={5}
                className="w-full touch-none"
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="p-3">
            <CardTitle className="text-sm">Room Size</CardTitle>
            <CardDescription className="text-xs">
              Min/max room dimensions
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 p-3 pt-0">
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium">Min Width</Label>
                <span className="text-xs font-mono text-muted-foreground">
                  {params.roomWidthMin}
                </span>
              </div>
              <Slider
                value={[params.roomWidthMin]}
                onValueChange={([value]) =>
                  onParamChange("roomWidthMin", value)
                }
                min={3}
                max={15}
                step={1}
                className="w-full touch-none"
              />
            </div>
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium">Max Width</Label>
                <span className="text-xs font-mono text-muted-foreground">
                  {params.roomWidthMax}
                </span>
              </div>
              <Slider
                value={[params.roomWidthMax]}
                onValueChange={([value]) =>
                  onParamChange("roomWidthMax", value)
                }
                min={5}
                max={20}
                step={1}
                className="w-full touch-none"
              />
            </div>
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium">Min Height</Label>
                <span className="text-xs font-mono text-muted-foreground">
                  {params.roomHeightMin}
                </span>
              </div>
              <Slider
                value={[params.roomHeightMin]}
                onValueChange={([value]) =>
                  onParamChange("roomHeightMin", value)
                }
                min={3}
                max={15}
                step={1}
                className="w-full touch-none"
              />
            </div>
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium">Max Height</Label>
                <span className="text-xs font-mono text-muted-foreground">
                  {params.roomHeightMax}
                </span>
              </div>
              <Slider
                value={[params.roomHeightMax]}
                onValueChange={([value]) =>
                  onParamChange("roomHeightMax", value)
                }
                min={5}
                max={20}
                step={1}
                className="w-full touch-none"
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="p-3">
            <CardTitle className="text-sm">Generation Settings</CardTitle>
            <CardDescription className="text-xs">
              Density and seed control
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 p-3 pt-0">
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-medium">Dug %</Label>
                <span className="text-xs font-mono text-muted-foreground">
                  {(params.dugPercentage * 100).toFixed(0)}%
                </span>
              </div>
              <Slider
                value={[params.dugPercentage]}
                onValueChange={([value]) =>
                  onParamChange("dugPercentage", value)
                }
                min={0.1}
                max={0.6}
                step={0.05}
                className="w-full touch-none"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="seed" className="text-xs font-medium">
                Seed
              </Label>
              <div className="flex gap-2">
                <Input
                  id="seed"
                  type="number"
                  value={params.seed || 0}
                  onChange={(e) =>
                    onParamChange("seed", Number.parseInt(e.target.value) || 0)
                  }
                  className="flex-1 text-xs h-7"
                />
                <Button
                  variant="outline"
                  size="icon"
                  className="h-7 w-7"
                  onClick={onRandomizeSeed}
                  title="Randomize seed"
                >
                  <Dices className="h-3.5 w-3.5" />
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <Button onClick={onRegenerate} className="w-full" size="sm">
        <RefreshCw className="mr-2 h-3.5 w-3.5" />
        Regenerate Dungeon
      </Button>
    </div>
  );
}
