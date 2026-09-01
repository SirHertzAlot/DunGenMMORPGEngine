import React from "react";
import type { ActiveEffect } from "../hooks/usePlayerEffects";
import BuffDebuffBar from "./BuffDebuffBar";

interface BuffDebuffContainerProps {
  activeBuffs: ActiveEffect[];
  activeDebuffs: ActiveEffect[];
  getDurationProgress: (id: string) => number;
  getRemainingDuration: (id: string) => number;
}

export default function BuffDebuffContainer({
  activeBuffs,
  activeDebuffs,
  getDurationProgress,
  getRemainingDuration,
}: BuffDebuffContainerProps) {
  const allEffects = [...activeBuffs, ...activeDebuffs];
  if (allEffects.length === 0) return null;

  return (
    <div className="flex flex-wrap gap-1 justify-center mb-1 max-w-xl mx-auto px-2">
      {allEffects.map((effect) => (
        <BuffDebuffBar
          key={effect.effectId}
          effect={effect}
          getDurationProgress={getDurationProgress}
          getRemainingDuration={getRemainingDuration}
        />
      ))}
    </div>
  );
}
