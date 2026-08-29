import React from "react";

export interface EnemyEffect {
  effectId: string;
  name: string;
  isBuff: boolean;
  icon?: string;
  description?: string;
}

interface EnemyEffectBadgesProps {
  effects: EnemyEffect[];
  maxVisible?: number;
}

export default function EnemyEffectBadges({
  effects,
  maxVisible = 4,
}: EnemyEffectBadgesProps) {
  if (effects.length === 0) return null;

  const visible = effects.slice(0, maxVisible);
  const overflow = effects.length - maxVisible;

  return (
    <div className="flex flex-wrap gap-0.5 justify-center mt-0.5">
      {visible.map((effect) => (
        <div
          key={effect.effectId}
          title={effect.description || effect.name}
          className={`px-1 py-0.5 rounded text-[8px] font-semibold leading-none ${
            effect.isBuff
              ? "bg-emerald-700/80 text-emerald-100 border border-emerald-500/40"
              : "bg-red-800/80 text-red-100 border border-red-500/40"
          }`}
        >
          {effect.icon && <span className="mr-0.5">{effect.icon}</span>}
          {effect.name}
        </div>
      ))}
      {overflow > 0 && (
        <div className="px-1 py-0.5 rounded text-[8px] font-semibold leading-none bg-gray-700/80 text-gray-300 border border-gray-600/40">
          +{overflow}
        </div>
      )}
    </div>
  );
}
