import React, { useEffect, useState } from "react";
import type { ActiveEffect } from "../hooks/usePlayerEffects";

interface BuffDebuffBarProps {
  effect: ActiveEffect;
  getDurationProgress: (id: string) => number;
  getRemainingDuration: (id: string) => number;
}

export default function BuffDebuffBar({
  effect,
  getDurationProgress,
  getRemainingDuration,
}: BuffDebuffBarProps) {
  const [progress, setProgress] = useState(
    getDurationProgress(effect.effectId),
  );

  useEffect(() => {
    const interval = setInterval(() => {
      setProgress(getDurationProgress(effect.effectId));
    }, 100);
    return () => clearInterval(interval);
  }, [effect.effectId, getDurationProgress]);

  const remaining = getRemainingDuration(effect.effectId);
  const isBuff = effect.isBuff;

  return (
    <div
      className={`flex items-center gap-1.5 px-2 py-1 rounded text-xs ${
        isBuff
          ? "bg-emerald-900/80 border border-emerald-500/40"
          : "bg-red-900/80 border border-red-500/40"
      }`}
      title={effect.description}
    >
      <span className="text-sm leading-none">{effect.icon}</span>
      <div className="flex flex-col gap-0.5 min-w-0">
        <span
          className={`font-semibold truncate text-[10px] leading-none ${isBuff ? "text-emerald-200" : "text-red-200"}`}
        >
          {effect.name}
        </span>
        <div
          className={`h-1 rounded-full overflow-hidden w-16 ${isBuff ? "bg-emerald-900" : "bg-red-900"}`}
        >
          <div
            className={`h-full rounded-full transition-all duration-100 ${isBuff ? "bg-emerald-400" : "bg-red-400"}`}
            style={{ width: `${progress * 100}%` }}
          />
        </div>
      </div>
      <span
        className={`text-[9px] font-mono ml-auto ${isBuff ? "text-emerald-300" : "text-red-300"}`}
      >
        {remaining.toFixed(1)}s
      </span>
    </div>
  );
}
