import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  ChevronLeft,
  ChevronRight,
  Eye,
  Navigation,
  Users,
} from "lucide-react";
import React from "react";

export type CameraMode = "orbit" | "fps";

interface MobInfo {
  id: string;
  type: string;
  index: number;
}

interface DungeonToolbarProps {
  cameraMode: CameraMode;
  onCameraModeToggle: () => void;
  mobs: MobInfo[];
  currentMobIndex: number;
  onPrevMob: () => void;
  onNextMob: () => void;
  focusModeActive: boolean;
  onToggleFocusMode: () => void;
}

export default function DungeonToolbar({
  cameraMode,
  onCameraModeToggle,
  mobs,
  currentMobIndex,
  onPrevMob,
  onNextMob,
  focusModeActive,
  onToggleFocusMode,
}: DungeonToolbarProps) {
  const currentMob = mobs[currentMobIndex];
  const hasMobs = mobs.length > 0;

  return (
    <TooltipProvider>
      <div className="w-full bg-card border-b border-border flex items-center gap-2 px-3 py-2 flex-wrap">
        {/* Camera Mode Toggle */}
        <div className="flex items-center gap-1.5">
          <Eye className="w-4 h-4 text-muted-foreground" />
          <span className="text-xs text-muted-foreground font-medium hidden sm:inline">
            Camera:
          </span>
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant={cameraMode === "orbit" ? "default" : "outline"}
                size="sm"
                onClick={onCameraModeToggle}
                className="h-7 px-2.5 text-xs"
              >
                {cameraMode === "orbit" ? (
                  <span className="flex items-center gap-1">
                    <Navigation className="w-3 h-3" />
                    <span className="hidden sm:inline">Isometric</span>
                    <span className="sm:hidden">ISO</span>
                  </span>
                ) : (
                  <span className="flex items-center gap-1">
                    <Eye className="w-3 h-3" />
                    <span>FPS</span>
                  </span>
                )}
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>
                Switch to {cameraMode === "orbit" ? "FPS" : "Isometric"} camera
                mode
              </p>
            </TooltipContent>
          </Tooltip>
        </div>

        <Separator orientation="vertical" className="h-6" />

        {/* Focus Mode Toggle */}
        <div className="flex items-center gap-1.5">
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant={focusModeActive ? "default" : "outline"}
                size="sm"
                onClick={onToggleFocusMode}
                className="h-7 px-2.5 text-xs"
              >
                <Users className="w-3 h-3 mr-1" />
                <span className="hidden sm:inline">Focus Mode</span>
                <span className="sm:hidden">Focus</span>
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>{focusModeActive ? "Exit" : "Enter"} mob focus mode</p>
            </TooltipContent>
          </Tooltip>
        </div>

        <Separator orientation="vertical" className="h-6" />

        {/* Mob Scroll Controls */}
        <div className="flex items-center gap-1.5">
          <Users className="w-4 h-4 text-muted-foreground" />
          <span className="text-xs text-muted-foreground font-medium hidden sm:inline">
            Mob:
          </span>

          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="outline"
                size="sm"
                onClick={onPrevMob}
                disabled={!hasMobs}
                className="h-7 w-7 p-0"
              >
                <ChevronLeft className="w-3.5 h-3.5" />
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>Previous mob</p>
            </TooltipContent>
          </Tooltip>

          <div className="min-w-[100px] text-center">
            {hasMobs && currentMob ? (
              <div className="flex items-center gap-1 justify-center">
                <Badge variant="secondary" className="text-xs px-1.5 py-0 h-5">
                  {currentMob.type}
                </Badge>
                <span className="text-xs text-muted-foreground">
                  {currentMobIndex + 1}/{mobs.length}
                </span>
              </div>
            ) : (
              <span className="text-xs text-muted-foreground italic">
                No mobs
              </span>
            )}
          </div>

          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="outline"
                size="sm"
                onClick={onNextMob}
                disabled={!hasMobs}
                className="h-7 w-7 p-0"
              >
                <ChevronRight className="w-3.5 h-3.5" />
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>Next mob</p>
            </TooltipContent>
          </Tooltip>
        </div>

        {/* Keyboard hints */}
        <div className="ml-auto hidden lg:flex items-center gap-2 text-xs text-muted-foreground">
          <span>← → cycle mobs</span>
          <Separator orientation="vertical" className="h-4" />
          <span>V = cam mode</span>
          <Separator orientation="vertical" className="h-4" />
          <span>F = focus</span>
          <Separator orientation="vertical" className="h-4" />
          <span>Esc = exit focus</span>
        </div>
      </div>
    </TooltipProvider>
  );
}
