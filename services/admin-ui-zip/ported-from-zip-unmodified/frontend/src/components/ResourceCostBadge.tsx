import { Droplets, Zap } from "lucide-react";
import React from "react";

interface ResourceCostBadgeProps {
  manaCost: number;
  staminaCost: number;
  hasEnoughMana?: boolean;
  hasEnoughStamina?: boolean;
}

export default function ResourceCostBadge({
  manaCost,
  staminaCost,
  hasEnoughMana = true,
  hasEnoughStamina = true,
}: ResourceCostBadgeProps) {
  if (manaCost === 0 && staminaCost === 0) return null;

  return (
    <div className="absolute bottom-0.5 right-0.5 flex flex-col gap-0.5 items-end">
      {manaCost > 0 && (
        <div
          className={`flex items-center gap-0.5 px-1 rounded text-[9px] font-bold leading-none py-0.5 ${
            hasEnoughMana
              ? "bg-blue-900/80 text-blue-200"
              : "bg-red-900/80 text-red-200"
          }`}
        >
          <Droplets className="w-2 h-2" />
          {manaCost}
        </div>
      )}
      {staminaCost > 0 && (
        <div
          className={`flex items-center gap-0.5 px-1 rounded text-[9px] font-bold leading-none py-0.5 ${
            hasEnoughStamina
              ? "bg-yellow-900/80 text-yellow-200"
              : "bg-red-900/80 text-red-200"
          }`}
        >
          <Zap className="w-2 h-2" />
          {staminaCost}
        </div>
      )}
    </div>
  );
}
