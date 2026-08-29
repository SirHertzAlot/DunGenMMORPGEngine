import { Plus, RotateCcw } from "lucide-react";
import React, { useState, useCallback, useRef } from "react";
import { useHotbarState } from "../hooks/useHotbarState";
import { usePlayerEffects } from "../hooks/usePlayerEffects";
import { useSkillCooldowns } from "../hooks/useSkillCooldowns";
import BuffDebuffContainer from "./BuffDebuffContainer";
import SkillGroup from "./SkillGroup";

interface DragRef {
  slotId: string;
  groupId: string;
}

interface SkillHotbarProps {
  playerMana?: number;
  playerStamina?: number;
}

export default function SkillHotbar({
  playerMana = 100,
  playerStamina = 100,
}: SkillHotbarProps) {
  const {
    config,
    setActiveSlot,
    moveSkill,
    renameGroup,
    createGroup,
    deleteGroup,
    resetToDefault,
  } = useHotbarState();
  const {
    startCooldown,
    getRemainingCooldown,
    cooldowns: _cooldowns,
  } = useSkillCooldowns();
  const {
    activeBuffs,
    activeDebuffs,
    getDurationProgress,
    getRemainingDuration,
    addEffect,
  } = usePlayerEffects();
  const dragRef = useRef<DragRef | null>(null);
  const [showAddGroup, setShowAddGroup] = useState(false);
  const [newGroupLabel, setNewGroupLabel] = useState("");

  // Build cooldown remaining map for all skills
  const cooldownMap: Record<string, number> = {};
  for (const group of config.groups) {
    for (const slot of group.slots) {
      if (slot.skill) {
        cooldownMap[slot.skill.id] = getRemainingCooldown(slot.skill.id);
      }
    }
  }

  const handleActivate = useCallback(
    (slotId: string, skillId: string) => {
      setActiveSlot(slotId);

      for (const group of config.groups) {
        const slot = group.slots.find((s) => s.slotId === slotId);
        if (slot?.skill) {
          const skill = slot.skill;
          if (skill.cooldownTotal > 0) {
            startCooldown(skillId, skill.cooldownTotal);
          }

          if (skill.name === "Bravery on Display") {
            addEffect({
              effectId: "bravery-on-display",
              name: "Bravery on Display",
              isBuff: true,
              durationSeconds: 15,
              magnitude: 1.3,
              description: "Attack power increased by 30%",
              icon: "🔥",
            });
          } else if (skill.name === "Visceral Intimidation") {
            addEffect({
              effectId: "visceral-intimidation",
              name: "Visceral Intimidation",
              isBuff: false,
              durationSeconds: 10,
              magnitude: 0.7,
              description: "Enemy attack reduced by 30%",
              icon: "💀",
            });
          }
          break;
        }
      }
    },
    [config.groups, setActiveSlot, startCooldown, addEffect],
  );

  const handleDragStart = useCallback((slotId: string, groupId: string) => {
    dragRef.current = { slotId, groupId };
  }, []);

  const handleDragOver = useCallback((_slotId: string, _groupId: string) => {
    // visual feedback could be added here
  }, []);

  const handleDrop = useCallback(
    (toSlotId: string, toGroupId: string) => {
      if (!dragRef.current) return;
      const { slotId: fromSlotId, groupId: fromGroupId } = dragRef.current;
      if (fromSlotId !== toSlotId || fromGroupId !== toGroupId) {
        moveSkill(fromGroupId, fromSlotId, toGroupId, toSlotId);
      }
      dragRef.current = null;
    },
    [moveSkill],
  );

  const handleAddGroup = () => {
    if (newGroupLabel.trim()) {
      createGroup(newGroupLabel.trim(), "custom");
      setNewGroupLabel("");
      setShowAddGroup(false);
    }
  };

  // Split groups into two rows: first half = items/consumables, second half = skills
  const midpoint = Math.ceil(config.groups.length / 2);
  const itemsRowGroups = config.groups.slice(0, midpoint);
  const skillsRowGroups = config.groups.slice(midpoint);
  const allGroupsInSingleRow = config.groups.length <= 2;

  return (
    <div
      className="absolute bottom-2 left-1/2 -translate-x-1/2 z-40 flex flex-col items-center gap-1 pointer-events-none"
      data-ocid="skill-hotbar"
    >
      {/* Buff/Debuff bars */}
      <div className="pointer-events-auto">
        <BuffDebuffContainer
          activeBuffs={activeBuffs}
          activeDebuffs={activeDebuffs}
          getDurationProgress={getDurationProgress}
          getRemainingDuration={getRemainingDuration}
        />
      </div>

      {/* Hotbar — 2-row layout with ornate gold scrollwork */}
      <div
        className="pointer-events-auto flex flex-col gap-0 rounded-xl overflow-hidden"
        style={{
          background: "#1a1208",
          border: "2px solid #c9a227",
          boxShadow:
            "0 0 0 1px #1a120880, 0 -2px 12px #c9a22730, 0 2px 12px #c9a22730, inset 0 1px 0 #c9a22740, inset 0 -1px 0 #c9a22740",
        }}
      >
        {/* Items row label bar */}
        <div
          className="flex items-center justify-between px-3 py-0.5"
          style={{
            background:
              "linear-gradient(90deg, #c9a22720, #c9a22740, #c9a22720)",
            borderBottom: "1px solid #c9a22760",
          }}
        >
          <span
            style={{
              fontSize: "8px",
              color: "#c9a227",
              fontWeight: 700,
              letterSpacing: "0.12em",
              textTransform: "uppercase",
            }}
          >
            Items
          </span>
          <div className="flex gap-1">
            {showAddGroup ? (
              <div className="flex items-center gap-1">
                <input
                  className="text-xs px-1.5 py-0.5 rounded border w-20"
                  style={{
                    background: "#2a1e0a",
                    borderColor: "#c9a22760",
                    color: "#f5e6c8",
                    fontSize: "10px",
                  }}
                  placeholder="Group name"
                  value={newGroupLabel}
                  onChange={(e) => setNewGroupLabel(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") handleAddGroup();
                    if (e.key === "Escape") setShowAddGroup(false);
                  }}
                />
                <button
                  type="button"
                  onClick={handleAddGroup}
                  className="text-green-400 hover:text-green-300 text-xs px-1 py-0.5 rounded"
                  style={{
                    background: "#2a1e0a",
                    border: "1px solid #c9a22760",
                  }}
                >
                  +
                </button>
                <button
                  type="button"
                  onClick={() => setShowAddGroup(false)}
                  className="text-red-400 hover:text-red-300 text-xs px-1 py-0.5 rounded"
                  style={{
                    background: "#2a1e0a",
                    border: "1px solid #c9a22760",
                  }}
                >
                  ✕
                </button>
              </div>
            ) : (
              <div className="flex gap-1">
                <button
                  type="button"
                  onClick={() => setShowAddGroup(true)}
                  className="hover:text-amber-300 p-0.5 rounded transition-colors"
                  style={{ color: "#c9a22780", background: "transparent" }}
                  title="Add skill group"
                >
                  <Plus className="w-2.5 h-2.5" />
                </button>
                <button
                  type="button"
                  onClick={resetToDefault}
                  className="hover:text-amber-300 p-0.5 rounded transition-colors"
                  style={{ color: "#c9a22780", background: "transparent" }}
                  title="Reset hotbar"
                >
                  <RotateCcw className="w-2.5 h-2.5" />
                </button>
              </div>
            )}
          </div>
        </div>

        {/* Items row groups */}
        <div
          className="flex items-center gap-2 px-3 py-1.5"
          style={{ background: "#1a120890" }}
        >
          {(allGroupsInSingleRow ? config.groups : itemsRowGroups).map(
            (group) => (
              <SkillGroup
                key={group.groupId}
                group={group}
                activeSlotId={config.activeSlotId}
                cooldowns={cooldownMap}
                onActivate={handleActivate}
                onDragStart={handleDragStart}
                onDragOver={handleDragOver}
                onDrop={handleDrop}
                onRename={renameGroup}
                onDelete={deleteGroup}
                playerMana={playerMana}
                playerStamina={playerStamina}
              />
            ),
          )}
        </div>

        {/* Divider with gold accent */}
        {!allGroupsInSingleRow && (
          <div
            style={{
              height: "1px",
              background:
                "linear-gradient(90deg, transparent, #c9a22770, transparent)",
            }}
          />
        )}

        {/* Skills row label */}
        {!allGroupsInSingleRow && (
          <>
            <div
              className="flex items-center px-3 py-0.5"
              style={{
                background:
                  "linear-gradient(90deg, #c9a22720, #c9a22740, #c9a22720)",
                borderTop: "1px solid #c9a22760",
              }}
            >
              <span
                style={{
                  fontSize: "8px",
                  color: "#c9a227",
                  fontWeight: 700,
                  letterSpacing: "0.12em",
                  textTransform: "uppercase",
                }}
              >
                Skills
              </span>
            </div>

            {/* Skills row groups */}
            <div
              className="flex items-center gap-2 px-3 py-1.5"
              style={{ background: "#1a120870" }}
            >
              {skillsRowGroups.map((group) => (
                <SkillGroup
                  key={group.groupId}
                  group={group}
                  activeSlotId={config.activeSlotId}
                  cooldowns={cooldownMap}
                  onActivate={handleActivate}
                  onDragStart={handleDragStart}
                  onDragOver={handleDragOver}
                  onDrop={handleDrop}
                  onRename={renameGroup}
                  onDelete={deleteGroup}
                  playerMana={playerMana}
                  playerStamina={playerStamina}
                />
              ))}
            </div>
          </>
        )}

        {/* Bottom gold trim */}
        <div
          style={{
            height: "2px",
            background:
              "linear-gradient(90deg, transparent, #c9a227, transparent)",
          }}
        />
      </div>
    </div>
  );
}
