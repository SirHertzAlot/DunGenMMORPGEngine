// Hotbar and skill slot types

export type SkillCategory = "attack" | "defense" | "utility" | "custom";

export interface SkillData {
  id: string;
  name: string;
  description: string;
  icon: string; // emoji or icon identifier
  category: SkillCategory;
  manaCost: number;
  staminaCost: number;
  cooldownTotal: number; // seconds
  cooldownRemaining: number; // seconds
  isUnlocked: boolean;
  objectType?: string;
}

export interface SkillSlotData {
  slotId: string;
  skill: SkillData | null;
  hotkey?: string;
}

export interface SkillGroupData {
  groupId: string;
  label: string;
  groupType: SkillCategory;
  slots: SkillSlotData[];
  color: string; // tailwind color class
}

export interface HotbarConfig {
  groups: SkillGroupData[];
  activeSlotId: string | null;
}

export interface DragState {
  draggingSlotId: string | null;
  draggingGroupId: string | null;
  overSlotId: string | null;
  overGroupId: string | null;
}
