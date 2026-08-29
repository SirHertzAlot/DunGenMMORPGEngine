import React from "react";
import type { MasteryTier } from "../types/mastery";
import CraftsmanBadge from "./CraftsmanBadge";
import DangerRatingDisplay from "./DangerRatingDisplay";
import EnemyEffectBadges, { type EnemyEffect } from "./EnemyEffectBadges";

interface EnemyHealthBarOverlayProps {
  name: string;
  level: number;
  dangerRating: number;
  healthCurrent: number;
  healthMax: number;
  activeBuffs?: EnemyEffect[];
  activeDebuffs?: EnemyEffect[];
  masteryTier?: MasteryTier | null;
  masteryLevel?: number;
}

export default function EnemyHealthBarOverlay({
  name,
  level,
  dangerRating,
  healthCurrent,
  healthMax,
  activeBuffs = [],
  activeDebuffs = [],
  masteryTier = null,
  masteryLevel = 0,
}: EnemyHealthBarOverlayProps) {
  const healthPercent =
    healthMax > 0 ? Math.max(0, Math.min(1, healthCurrent / healthMax)) : 0;
  const allEffects: EnemyEffect[] = [...activeBuffs, ...activeDebuffs];

  // Health bar color based on percentage
  const barColor =
    healthPercent > 0.6
      ? "bg-red-500"
      : healthPercent > 0.3
        ? "bg-orange-500"
        : "bg-red-700";

  return (
    <div
      className="flex flex-col items-center gap-0.5 pointer-events-none select-none"
      style={{ minWidth: 120, maxWidth: 160 }}
    >
      {/* Name + Level */}
      <div className="flex items-center gap-1.5 px-2 py-0.5 rounded-t bg-gray-900/90 border border-gray-700/60 w-full justify-center">
        <span className="text-white font-bold text-[11px] truncate">
          {name}
        </span>
        <span className="text-gray-400 text-[9px] font-mono shrink-0">
          Lv.{level}
        </span>
      </div>

      {/* Health bar */}
      <div className="w-full px-1 bg-gray-900/90 border-x border-gray-700/60">
        <div className="h-2 bg-gray-800 rounded-full overflow-hidden">
          <div
            className={`h-full rounded-full transition-all duration-300 ${barColor}`}
            style={{ width: `${healthPercent * 100}%` }}
          />
        </div>
        <div className="text-center text-[8px] text-gray-400 font-mono leading-tight">
          {healthCurrent}/{healthMax}
        </div>
      </div>

      {/* Danger rating + Craftsman badge */}
      <div className="flex flex-col items-center gap-0.5 px-1 pb-0.5 bg-gray-900/90 border-x border-b border-gray-700/60 rounded-b w-full">
        <DangerRatingDisplay dangerRating={dangerRating} size={12} />
        <CraftsmanBadge masteryTier={masteryTier} masteryLevel={masteryLevel} />
      </div>

      {/* Effect badges */}
      {allEffects.length > 0 && (
        <div className="bg-gray-900/80 border border-gray-700/40 rounded px-1 py-0.5 w-full">
          <EnemyEffectBadges effects={allEffects} maxVisible={4} />
        </div>
      )}
    </div>
  );
}
