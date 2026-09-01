import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { Lock } from "lucide-react";
import React, { useRef } from "react";
import type { SkillSlotData } from "../types/hotbar";
import CooldownOverlay from "./CooldownOverlay";
import ResourceCostBadge from "./ResourceCostBadge";

interface SkillSlotProps {
  slot: SkillSlotData;
  groupId: string;
  isActive: boolean;
  cooldownRemaining: number;
  onActivate: (slotId: string, skillId: string) => void;
  onDragStart: (slotId: string, groupId: string) => void;
  onDragOver: (slotId: string, groupId: string) => void;
  onDrop: (toSlotId: string, toGroupId: string) => void;
  playerMana?: number;
  playerStamina?: number;
}

export default function SkillSlot({
  slot,
  groupId,
  isActive,
  cooldownRemaining,
  onActivate,
  onDragStart,
  onDragOver,
  onDrop,
  playerMana = 100,
  playerStamina = 100,
}: SkillSlotProps) {
  const skill = slot.skill;
  const isOnCooldown = cooldownRemaining > 0;
  const hasEnoughMana = !skill || playerMana >= skill.manaCost;
  const hasEnoughStamina = !skill || playerStamina >= skill.staminaCost;
  const isDisabled =
    isOnCooldown ||
    !hasEnoughMana ||
    !hasEnoughStamina ||
    (skill && !skill.isUnlocked);

  const handleClick = () => {
    if (!skill || isDisabled) return;
    onActivate(slot.slotId, skill.id);
  };

  const slotContent = (
    <button
      type="button"
      className={`relative w-13 h-13 rounded border-2 cursor-pointer select-none transition-all duration-150 flex items-center justify-center
        ${isActive ? "border-yellow-400 shadow-lg shadow-yellow-400/30" : "border-gray-600/60"}
        ${isDisabled ? "opacity-60" : "hover:border-gray-400 hover:scale-105"}
        ${skill ? "bg-gray-800/90" : "bg-gray-900/50 border-dashed"}
      `}
      style={{ width: 52, height: 52 }}
      onClick={handleClick}
      draggable={!!skill}
      onDragStart={() => skill && onDragStart(slot.slotId, groupId)}
      onDragOver={(e) => {
        e.preventDefault();
        onDragOver(slot.slotId, groupId);
      }}
      onDrop={(e) => {
        e.preventDefault();
        onDrop(slot.slotId, groupId);
      }}
    >
      {skill ? (
        <>
          <span className="text-2xl leading-none select-none">
            {skill.icon}
          </span>
          {!skill.isUnlocked && (
            <div className="absolute inset-0 flex items-center justify-center bg-black/60 rounded">
              <Lock className="w-4 h-4 text-gray-400" />
            </div>
          )}
          <CooldownOverlay
            remainingSeconds={cooldownRemaining}
            totalSeconds={skill.cooldownTotal}
            size={52}
          />
          <ResourceCostBadge
            manaCost={skill.manaCost}
            staminaCost={skill.staminaCost}
            hasEnoughMana={hasEnoughMana}
            hasEnoughStamina={hasEnoughStamina}
          />
          {slot.hotkey && (
            <span className="absolute top-0.5 left-1 text-[9px] text-gray-400 font-mono leading-none">
              {slot.hotkey}
            </span>
          )}
        </>
      ) : (
        <span className="text-gray-600 text-xs">+</span>
      )}
    </button>
  );

  if (!skill) return slotContent;

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>{slotContent}</TooltipTrigger>
        <TooltipContent
          side="top"
          className="bg-gray-900 border-gray-700 text-white max-w-48"
        >
          <div className="space-y-1">
            <div className="font-bold text-sm">{skill.name}</div>
            <div className="text-xs text-gray-300">{skill.description}</div>
            {skill.cooldownTotal > 0 && (
              <div className="text-xs text-gray-400">
                Cooldown: {skill.cooldownTotal}s
              </div>
            )}
            {!skill.isUnlocked && (
              <div className="text-xs text-yellow-400">
                🔒 Locked — advance mastery to unlock
              </div>
            )}
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
