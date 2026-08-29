import { useCallback, useEffect, useState } from "react";
import {
  type HotbarConfig,
  type SkillCategory,
  type SkillData,
  type SkillGroupData,
  SkillSlotData,
} from "../types/hotbar";

const STORAGE_KEY = "rpg-hotbar-config";

const DEFAULT_ATTACK_SKILLS: SkillData[] = [
  {
    id: "atk-1",
    name: "Strike",
    description: "A basic melee strike",
    icon: "⚔️",
    category: "attack",
    manaCost: 0,
    staminaCost: 10,
    cooldownTotal: 1.5,
    cooldownRemaining: 0,
    isUnlocked: true,
  },
  {
    id: "atk-2",
    name: "Power Slash",
    description: "A powerful overhead slash",
    icon: "🗡️",
    category: "attack",
    manaCost: 5,
    staminaCost: 20,
    cooldownTotal: 4,
    cooldownRemaining: 0,
    isUnlocked: true,
  },
  {
    id: "atk-3",
    name: "Bravery on Display",
    description: "Buff that increases attack power",
    icon: "🔥",
    category: "attack",
    manaCost: 15,
    staminaCost: 0,
    cooldownTotal: 8,
    cooldownRemaining: 0,
    isUnlocked: true,
  },
  {
    id: "atk-4",
    name: "Whirlwind",
    description: "Spin attack hitting all nearby enemies",
    icon: "🌀",
    category: "attack",
    manaCost: 20,
    staminaCost: 30,
    cooldownTotal: 12,
    cooldownRemaining: 0,
    isUnlocked: false,
  },
];

const DEFAULT_DEFENSE_SKILLS: SkillData[] = [
  {
    id: "def-1",
    name: "Block",
    description: "Raise shield to block incoming damage",
    icon: "🛡️",
    category: "defense",
    manaCost: 0,
    staminaCost: 5,
    cooldownTotal: 0.5,
    cooldownRemaining: 0,
    isUnlocked: true,
  },
  {
    id: "def-2",
    name: "Parry",
    description: "Deflect an attack and counter",
    icon: "🔰",
    category: "defense",
    manaCost: 0,
    staminaCost: 15,
    cooldownTotal: 3,
    cooldownRemaining: 0,
    isUnlocked: true,
  },
  {
    id: "def-3",
    name: "Visceral Intimidation",
    description: "Debuff that reduces enemy attack",
    icon: "💀",
    category: "defense",
    manaCost: 10,
    staminaCost: 0,
    cooldownTotal: 10,
    cooldownRemaining: 0,
    isUnlocked: true,
  },
  {
    id: "def-4",
    name: "Iron Fortress",
    description: "Greatly increase defense temporarily",
    icon: "🏰",
    category: "defense",
    manaCost: 25,
    staminaCost: 0,
    cooldownTotal: 15,
    cooldownRemaining: 0,
    isUnlocked: false,
  },
];

function createDefaultHotbar(): HotbarConfig {
  return {
    groups: [
      {
        groupId: "attack-group",
        label: "Attack",
        groupType: "attack",
        color: "red",
        slots: DEFAULT_ATTACK_SKILLS.map((skill, i) => ({
          slotId: `attack-slot-${i}`,
          skill,
          hotkey: `${i + 1}`,
        })),
      },
      {
        groupId: "defense-group",
        label: "Defense",
        groupType: "defense",
        color: "blue",
        slots: DEFAULT_DEFENSE_SKILLS.map((skill, i) => ({
          slotId: `defense-slot-${i}`,
          skill,
          hotkey: `${i + 5}`,
        })),
      },
    ],
    activeSlotId: null,
  };
}

export function useHotbarState() {
  const [config, setConfig] = useState<HotbarConfig>(() => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) return JSON.parse(stored);
    } catch {}
    return createDefaultHotbar();
  });

  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(config));
    } catch {}
  }, [config]);

  const setActiveSlot = useCallback((slotId: string | null) => {
    setConfig((prev) => ({ ...prev, activeSlotId: slotId }));
  }, []);

  const moveSkill = useCallback(
    (
      fromGroupId: string,
      fromSlotId: string,
      toGroupId: string,
      toSlotId: string,
    ) => {
      setConfig((prev) => {
        const newGroups = prev.groups.map((g) => ({
          ...g,
          slots: [...g.slots],
        }));
        const fromGroup = newGroups.find((g) => g.groupId === fromGroupId);
        const toGroup = newGroups.find((g) => g.groupId === toGroupId);
        if (!fromGroup || !toGroup) return prev;

        const fromSlot = fromGroup.slots.find((s) => s.slotId === fromSlotId);
        const toSlot = toGroup.slots.find((s) => s.slotId === toSlotId);
        if (!fromSlot || !toSlot) return prev;

        const fromSkill = fromSlot.skill;
        const toSkill = toSlot.skill;

        fromGroup.slots = fromGroup.slots.map((s) =>
          s.slotId === fromSlotId ? { ...s, skill: toSkill } : s,
        );
        toGroup.slots = toGroup.slots.map((s) =>
          s.slotId === toSlotId ? { ...s, skill: fromSkill } : s,
        );

        return { ...prev, groups: newGroups };
      });
    },
    [],
  );

  const renameGroup = useCallback((groupId: string, label: string) => {
    setConfig((prev) => ({
      ...prev,
      groups: prev.groups.map((g) =>
        g.groupId === groupId ? { ...g, label } : g,
      ),
    }));
  }, []);

  const createGroup = useCallback((label: string, groupType: SkillCategory) => {
    const newGroup: SkillGroupData = {
      groupId: `group-${Date.now()}`,
      label,
      groupType,
      color:
        groupType === "attack"
          ? "red"
          : groupType === "defense"
            ? "blue"
            : "green",
      slots: Array.from({ length: 4 }, (_, i) => ({
        slotId: `slot-${Date.now()}-${i}`,
        skill: null,
      })),
    };
    setConfig((prev) => ({ ...prev, groups: [...prev.groups, newGroup] }));
  }, []);

  const deleteGroup = useCallback((groupId: string) => {
    setConfig((prev) => ({
      ...prev,
      groups: prev.groups.filter((g) => g.groupId !== groupId),
    }));
  }, []);

  const reorderGroups = useCallback((fromIndex: number, toIndex: number) => {
    setConfig((prev) => {
      const groups = [...prev.groups];
      const [moved] = groups.splice(fromIndex, 1);
      groups.splice(toIndex, 0, moved);
      return { ...prev, groups };
    });
  }, []);

  const resetToDefault = useCallback(() => {
    setConfig(createDefaultHotbar());
  }, []);

  return {
    config,
    setActiveSlot,
    moveSkill,
    renameGroup,
    createGroup,
    deleteGroup,
    reorderGroups,
    resetToDefault,
  };
}
