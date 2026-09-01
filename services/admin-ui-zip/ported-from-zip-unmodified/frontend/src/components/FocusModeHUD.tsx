import {
  Camera,
  ChevronLeft,
  ChevronRight,
  Eye,
  Navigation,
  X,
} from "lucide-react";
import React, { useEffect } from "react";

interface FocusModeHUDProps {
  isActive: boolean;
  currentIndex: number;
  totalMobs: number;
  focusedMobId: string | null;
  onNext: () => void;
  onPrevious: () => void;
  onExit: () => void;
  cameraMode: "fps" | "third-person";
  onToggleCameraMode: () => void;
  autoFollow: boolean;
  onToggleAutoFollow: () => void;
}

export default function FocusModeHUD({
  isActive,
  currentIndex,
  totalMobs,
  focusedMobId,
  onNext,
  onPrevious,
  onExit,
  cameraMode,
  onToggleCameraMode,
  autoFollow,
  onToggleAutoFollow,
}: FocusModeHUDProps) {
  useEffect(() => {
    if (!isActive) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "ArrowRight" || e.key === "ArrowDown") {
        e.preventDefault();
        onNext();
      } else if (e.key === "ArrowLeft" || e.key === "ArrowUp") {
        e.preventDefault();
        onPrevious();
      } else if (e.key === "Escape") {
        e.preventDefault();
        onExit();
      } else if (e.key === "v" || e.key === "V") {
        onToggleCameraMode();
      } else if (e.key === "f" || e.key === "F") {
        onToggleAutoFollow();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [
    isActive,
    onNext,
    onPrevious,
    onExit,
    onToggleCameraMode,
    onToggleAutoFollow,
  ]);

  if (!isActive) return null;

  if (totalMobs === 0) {
    return (
      <div className="absolute top-4 left-1/2 -translate-x-1/2 z-30 flex items-center gap-2 bg-black/70 backdrop-blur-sm border border-white/20 rounded-full px-4 py-2 text-white text-sm">
        <Eye className="w-4 h-4 text-yellow-400" />
        <span className="text-white/70">No active mobs to focus</span>
        <button
          type="button"
          onClick={onExit}
          className="ml-1 text-white/50 hover:text-white transition-colors"
        >
          <X className="w-4 h-4" />
        </button>
      </div>
    );
  }

  const shortId = focusedMobId ? focusedMobId.slice(0, 8) : "—";

  return (
    <div className="absolute top-3 left-1/2 -translate-x-1/2 z-30 flex flex-col items-center gap-1.5 pointer-events-none">
      {/* Main focus bar */}
      <div className="flex items-center gap-1.5 bg-black/75 backdrop-blur-sm border border-yellow-400/40 rounded-full px-2.5 py-1.5 shadow-lg pointer-events-auto">
        <button
          type="button"
          onClick={onPrevious}
          className="p-1 rounded-full hover:bg-white/20 transition-colors text-white/80 hover:text-white"
          title="Previous mob (←)"
        >
          <ChevronLeft className="w-4 h-4" />
        </button>

        <div className="flex flex-col items-center min-w-[80px]">
          <span className="text-yellow-400 font-bold text-sm leading-tight">
            Mob {currentIndex + 1} / {totalMobs}
          </span>
          <span className="text-white/40 text-xs leading-tight font-mono">
            {shortId}…
          </span>
        </div>

        <button
          type="button"
          onClick={onNext}
          className="p-1 rounded-full hover:bg-white/20 transition-colors text-white/80 hover:text-white"
          title="Next mob (→)"
        >
          <ChevronRight className="w-4 h-4" />
        </button>

        <div className="w-px h-5 bg-white/20 mx-0.5" />

        {/* Camera mode toggle */}
        <button
          type="button"
          onClick={onToggleCameraMode}
          className={`flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold transition-colors ${
            cameraMode === "fps"
              ? "bg-amber-500/80 text-black hover:bg-amber-400/80"
              : "bg-blue-500/70 text-white hover:bg-blue-400/70"
          }`}
          title={
            cameraMode === "fps"
              ? "Switch to 3rd Person (V)"
              : "Switch to FPS (V)"
          }
        >
          {cameraMode === "fps" ? (
            <>
              <Eye className="w-3 h-3" />
              <span>FPS</span>
            </>
          ) : (
            <>
              <Camera className="w-3 h-3" />
              <span>3rd</span>
            </>
          )}
        </button>

        {/* Auto-follow toggle */}
        <button
          type="button"
          onClick={onToggleAutoFollow}
          className={`flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold transition-colors ${
            autoFollow
              ? "bg-green-500/80 text-white hover:bg-green-400/80"
              : "bg-white/15 text-white/60 hover:bg-white/25"
          }`}
          title={
            autoFollow ? "Disable Auto-Follow (F)" : "Enable Auto-Follow (F)"
          }
        >
          <Navigation className="w-3 h-3" />
          <span>{autoFollow ? "Follow" : "Free"}</span>
        </button>

        <div className="w-px h-5 bg-white/20 mx-0.5" />

        <button
          type="button"
          onClick={onExit}
          className="p-1 rounded-full hover:bg-red-500/30 transition-colors text-white/50 hover:text-white"
          title="Exit focus mode (Esc)"
        >
          <X className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* Mode hint */}
      <div className="text-white/40 text-xs pointer-events-none select-none">
        {cameraMode === "fps" ? "👁 First Person" : "📷 Third Person"}
        {autoFollow ? " · Auto-Follow ON" : " · Free Orbit"}
        <span className="ml-2 opacity-70">
          V=cam · F=follow · ←→=cycle · Esc=exit
        </span>
      </div>
    </div>
  );
}
