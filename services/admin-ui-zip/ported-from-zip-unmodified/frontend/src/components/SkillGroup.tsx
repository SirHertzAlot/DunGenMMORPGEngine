import { Check, Pencil, Trash2, X } from "lucide-react";
import React, { useState } from "react";
import type { SkillGroupData } from "../types/hotbar";
import SkillSlot from "./SkillSlot";

interface SkillGroupProps {
  group: SkillGroupData;
  activeSlotId: string | null;
  cooldowns: Record<string, number>;
  onActivate: (slotId: string, skillId: string) => void;
  onDragStart: (slotId: string, groupId: string) => void;
  onDragOver: (slotId: string, groupId: string) => void;
  onDrop: (toSlotId: string, toGroupId: string) => void;
  onRename: (groupId: string, label: string) => void;
  onDelete: (groupId: string) => void;
  playerMana?: number;
  playerStamina?: number;
}

const GROUP_COLORS: Record<string, string> = {
  attack: "border-red-500/50 bg-red-950/30",
  defense: "border-blue-500/50 bg-blue-950/30",
  utility: "border-green-500/50 bg-green-950/30",
  custom: "border-purple-500/50 bg-purple-950/30",
};

const LABEL_COLORS: Record<string, string> = {
  attack: "text-red-400",
  defense: "text-blue-400",
  utility: "text-green-400",
  custom: "text-purple-400",
};

export default function SkillGroup({
  group,
  activeSlotId,
  cooldowns,
  onActivate,
  onDragStart,
  onDragOver,
  onDrop,
  onRename,
  onDelete,
  playerMana,
  playerStamina,
}: SkillGroupProps) {
  const [editing, setEditing] = useState(false);
  const [editLabel, setEditLabel] = useState(group.label);

  const colorClass = GROUP_COLORS[group.groupType] || GROUP_COLORS.custom;
  const labelColor = LABEL_COLORS[group.groupType] || LABEL_COLORS.custom;

  const handleSaveLabel = () => {
    if (editLabel.trim()) onRename(group.groupId, editLabel.trim());
    setEditing(false);
  };

  return (
    <div
      className={`flex flex-col items-center gap-1 px-2 py-1.5 rounded-lg border ${colorClass}`}
    >
      {/* Group label */}
      <div className="flex items-center gap-1 w-full justify-between">
        {editing ? (
          <div className="flex items-center gap-1">
            <input
              className="bg-gray-800 text-white text-xs px-1 py-0.5 rounded border border-gray-600 w-20"
              value={editLabel}
              onChange={(e) => setEditLabel(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") handleSaveLabel();
                if (e.key === "Escape") setEditing(false);
              }}
            />
            <button
              type="button"
              onClick={handleSaveLabel}
              className="text-green-400 hover:text-green-300"
            >
              <Check className="w-3 h-3" />
            </button>
            <button
              type="button"
              onClick={() => setEditing(false)}
              className="text-red-400 hover:text-red-300"
            >
              <X className="w-3 h-3" />
            </button>
          </div>
        ) : (
          <span
            className={`text-[10px] font-bold uppercase tracking-wider ${labelColor}`}
          >
            {group.label}
          </span>
        )}
        <div className="flex items-center gap-0.5 ml-auto">
          {!editing && (
            <button
              type="button"
              onClick={() => {
                setEditLabel(group.label);
                setEditing(true);
              }}
              className="text-gray-500 hover:text-gray-300 p-0.5 rounded"
              title="Rename group"
            >
              <Pencil className="w-2.5 h-2.5" />
            </button>
          )}
          <button
            type="button"
            onClick={() => onDelete(group.groupId)}
            className="text-gray-500 hover:text-red-400 p-0.5 rounded"
            title="Delete group"
          >
            <Trash2 className="w-2.5 h-2.5" />
          </button>
        </div>
      </div>

      {/* Skill slots */}
      <div className="flex gap-1">
        {group.slots.map((slot) => (
          <SkillSlot
            key={slot.slotId}
            slot={slot}
            groupId={group.groupId}
            isActive={activeSlotId === slot.slotId}
            cooldownRemaining={slot.skill ? (cooldowns[slot.skill.id] ?? 0) : 0}
            onActivate={onActivate}
            onDragStart={onDragStart}
            onDragOver={onDragOver}
            onDrop={onDrop}
            playerMana={playerMana}
            playerStamina={playerStamina}
          />
        ))}
      </div>
    </div>
  );
}
