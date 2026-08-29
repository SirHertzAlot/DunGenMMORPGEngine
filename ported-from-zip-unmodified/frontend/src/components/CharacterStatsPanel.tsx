/**
 * CharacterStatsPanel — collapsible character stats panel.
 * Slides in from the right edge.
 * Toggle via isOpen prop. Keyboard: C / Escape handled by parent.
 */

import { type ReactNode, useEffect, useState } from "react";
import { getRuntimeManager } from "../lib/runtimeManager";
import type { LootItemData } from "../types/loot";
import {
  MASTERY_TIER_COLORS,
  MASTERY_TIER_LABELS,
  MASTERY_TIER_XP_THRESHOLDS,
} from "../types/mastery";
import type { Masterable } from "../types/runtime";

interface CharacterStatsPanelProps {
  isOpen: boolean;
  onClose: () => void;
  focusedEntityId?: string | null;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

interface PlayerSnapshot {
  name: string;
  level: number;
  str: number;
  agi: number;
  int: number;
  vit: number;
  luk: number;
  physRes: number;
  fireRes: number;
  coldRes: number;
  equippedItems: Array<{ slot: string; name: string | null }>;
  masteryTier: number;
  masteryLevel: number;
  masteryPoints: number;
}

const GEAR_SLOTS = ["Head", "Chest", "Hands", "Legs", "Feet", "Weapon"];

function readPlayerSnapshot(entityId?: string | null): PlayerSnapshot {
  const defaults: PlayerSnapshot = {
    name: "Hero",
    level: 1,
    str: 10,
    agi: 8,
    int: 12,
    vit: 10,
    luk: 6,
    physRes: 0,
    fireRes: 0,
    coldRes: 0,
    equippedItems: GEAR_SLOTS.map((s) => ({ slot: s, name: null })),
    masteryTier: 1,
    masteryLevel: 1,
    masteryPoints: 0,
  };

  try {
    const rm = getRuntimeManager();
    const player = rm.getEntity(entityId ?? "player-entity-0");
    if (!player) return defaults;

    // Stats component — try both casings
    const stats =
      player.components.get("Stats") ??
      player.components.get("stats") ??
      player.components.get("Attributes") ??
      player.components.get("attributes");
    const s = stats as Record<string, unknown> | undefined;

    // Name / level — also check MobMeta for mob entities
    const meta =
      player.components.get("Meta") ??
      player.components.get("meta") ??
      player.components.get("Character") ??
      player.components.get("character") ??
      player.components.get("MobMeta") ??
      player.components.get("mobMeta");
    const m = meta as Record<string, unknown> | undefined;

    // Mastery
    const mastery =
      player.components.get("Masterable") ??
      player.components.get("masterable");
    const mas = mastery as Masterable | undefined;

    // Inventory for equipped items
    const inv =
      player.components.get("Inventory") ?? player.components.get("inventory");
    const invData = inv as { lootItems?: LootItemData[] } | undefined;
    const equippedItems = GEAR_SLOTS.map((slot, idx) => ({
      slot,
      name: invData?.lootItems?.[idx]?.name ?? null,
    }));

    return {
      name: (m?.name as string) ?? (m?.displayName as string) ?? defaults.name,
      level: (m?.level as number) ?? (s?.level as number) ?? defaults.level,
      str: (s?.strength as number) ?? (s?.str as number) ?? defaults.str,
      agi: (s?.agility as number) ?? (s?.agi as number) ?? defaults.agi,
      int: (s?.intelligence as number) ?? (s?.int as number) ?? defaults.int,
      vit: (s?.vitality as number) ?? (s?.vit as number) ?? defaults.vit,
      luk: (s?.luck as number) ?? (s?.luk as number) ?? defaults.luk,
      physRes: (s?.physicalResistance as number) ?? defaults.physRes,
      fireRes: (s?.fireResistance as number) ?? defaults.fireRes,
      coldRes: (s?.coldResistance as number) ?? defaults.coldRes,
      equippedItems,
      masteryTier: mas?.masteryTier ?? defaults.masteryTier,
      masteryLevel: mas?.masteryLevel ?? defaults.masteryLevel,
      masteryPoints: mas?.masteryPoints ?? defaults.masteryPoints,
    };
  } catch {
    return defaults;
  }
}

// ── Sub-components ────────────────────────────────────────────────────────────

function SectionHeader({ children }: { children: ReactNode }) {
  return (
    <div
      style={{
        fontSize: 10,
        fontWeight: 700,
        color: "#c9a227",
        letterSpacing: "0.08em",
        textTransform: "uppercase",
        borderBottom: "1px solid rgba(201,162,39,0.25)",
        paddingBottom: 4,
        marginBottom: 6,
      }}
    >
      {children}
    </div>
  );
}

function StatRow({ label, value }: { label: string; value: number | string }) {
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        fontSize: 11,
        padding: "2px 0",
      }}
    >
      <span style={{ color: "#9ca3af" }}>{label}</span>
      <span style={{ color: "#f5deb3", fontWeight: 600 }}>{value}</span>
    </div>
  );
}

function GearSlot({ slot, name }: { slot: string; name: string | null }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 8,
        padding: "3px 6px",
        borderRadius: 4,
        background: name ? "rgba(26,111,196,0.12)" : "rgba(255,255,255,0.03)",
        border: `1px solid ${name ? "rgba(26,111,196,0.4)" : "rgba(201,162,39,0.15)"}`,
        fontSize: 11,
      }}
    >
      <span style={{ color: "#c9a22799", minWidth: 44 }}>{slot}</span>
      <span
        style={{
          color: name ? "#f5deb3" : "#4b5563",
          fontStyle: name ? "normal" : "italic",
          overflow: "hidden",
          textOverflow: "ellipsis",
          whiteSpace: "nowrap",
          flex: 1,
        }}
      >
        {name ?? "Empty"}
      </span>
    </div>
  );
}

// ── Main Component ─────────────────────────────────────────────────────────────

export default function CharacterStatsPanel({
  isOpen,
  onClose,
  focusedEntityId,
}: CharacterStatsPanelProps) {
  const [snapshot, setSnapshot] = useState<PlayerSnapshot>(() =>
    readPlayerSnapshot(focusedEntityId),
  );

  // Refresh snapshot when panel opens or focused entity changes
  useEffect(() => {
    if (!isOpen) return;
    setSnapshot(readPlayerSnapshot(focusedEntityId));
    const id = setInterval(
      () => setSnapshot(readPlayerSnapshot(focusedEntityId)),
      1000,
    );
    return () => clearInterval(id);
  }, [isOpen, focusedEntityId]);

  const tierLabel =
    MASTERY_TIER_LABELS[
      snapshot.masteryTier as keyof typeof MASTERY_TIER_LABELS
    ] ?? "Apprentice";
  const tierColor =
    MASTERY_TIER_COLORS[
      snapshot.masteryTier as keyof typeof MASTERY_TIER_COLORS
    ] ?? "#9ca3af";
  const xpThreshold =
    MASTERY_TIER_XP_THRESHOLDS[
      snapshot.masteryTier as keyof typeof MASTERY_TIER_XP_THRESHOLDS
    ] ?? 100;
  const masteryPct =
    xpThreshold === Number.POSITIVE_INFINITY
      ? 100
      : Math.min(100, Math.round((snapshot.masteryPoints / xpThreshold) * 100));

  return (
    <dialog
      data-ocid="char-stats-panel"
      aria-label="Character Stats"
      open={isOpen}
      style={{
        position: "absolute",
        top: "50%",
        right: isOpen ? 0 : -320,
        left: "auto",
        transform: "translateY(-50%)",
        zIndex: 55,
        width: 300,
        maxHeight: "90%",
        overflowY: "auto",
        opacity: isOpen ? 1 : 0,
        transition: "right 200ms ease, opacity 200ms ease",
        pointerEvents: isOpen ? "auto" : "none",
        userSelect: "none",
        fontFamily: "monospace, sans-serif",
        background: "transparent",
        border: "none",
        padding: 0,
        margin: 0,
      }}
    >
      <div
        style={{
          background: "linear-gradient(160deg, #2a1e0e 0%, #1a1208 100%)",
          border: "2px solid #c9a227",
          borderRadius: "10px 0 0 10px",
          boxShadow:
            "-4px 0 32px rgba(0,0,0,0.8), 0 0 0 1px rgba(201,162,39,0.1)",
          padding: "12px 14px 14px",
          display: "flex",
          flexDirection: "column",
          gap: 12,
        }}
      >
        {/* Header */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <span style={{ fontSize: 14 }}>👤</span>
            <span
              style={{
                color: "#c9a227",
                fontSize: 13,
                fontWeight: 700,
                letterSpacing: "0.04em",
              }}
            >
              {focusedEntityId ? "Spectating" : "Character"}
            </span>
            <span
              data-ocid="char-stats.level_badge"
              style={{
                backgroundColor: "#c9a22722",
                border: "1px solid #c9a22766",
                borderRadius: 4,
                color: "#c9a227",
                fontSize: 10,
                fontWeight: 700,
                padding: "1px 5px",
              }}
            >
              Lv.{snapshot.level}
            </span>
          </div>
          <button
            type="button"
            data-ocid="char-stats.close_button"
            onClick={onClose}
            aria-label="Close character stats"
            style={{
              background: "none",
              border: "1px solid #c9a22766",
              borderRadius: 4,
              color: "#c9a227",
              cursor: "pointer",
              fontSize: 14,
              width: 44,
              height: 44,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              WebkitTapHighlightColor: "transparent",
              minWidth: 44,
              minHeight: 44,
            }}
          >
            ✕
          </button>
        </div>

        {/* Name */}
        <div
          style={{
            color: "#f5deb3",
            fontSize: 13,
            fontWeight: 700,
            textAlign: "center",
            borderBottom: "1px solid rgba(201,162,39,0.2)",
            paddingBottom: 8,
          }}
          data-ocid="char-stats.name"
        >
          {snapshot.name}
        </div>

        {/* Attributes */}
        <div data-ocid="char-stats.attributes_section">
          <SectionHeader>Attributes</SectionHeader>
          <div style={{ display: "flex", flexDirection: "column", gap: 1 }}>
            <StatRow label="Strength" value={snapshot.str} />
            <StatRow label="Agility" value={snapshot.agi} />
            <StatRow label="Intelligence" value={snapshot.int} />
            <StatRow label="Vitality" value={snapshot.vit} />
            <StatRow label="Luck" value={snapshot.luk} />
          </div>
        </div>

        {/* Resistances */}
        <div data-ocid="char-stats.resistances_section">
          <SectionHeader>Resistances</SectionHeader>
          <div style={{ display: "flex", flexDirection: "column", gap: 1 }}>
            <StatRow label="Physical" value={`${snapshot.physRes}%`} />
            <StatRow label="Fire" value={`${snapshot.fireRes}%`} />
            <StatRow label="Cold" value={`${snapshot.coldRes}%`} />
          </div>
        </div>

        {/* Equipped Gear */}
        <div data-ocid="char-stats.gear_section">
          <SectionHeader>Equipped Gear</SectionHeader>
          <div style={{ display: "flex", flexDirection: "column", gap: 3 }}>
            {snapshot.equippedItems.map((g) => (
              <GearSlot key={g.slot} slot={g.slot} name={g.name} />
            ))}
          </div>
        </div>

        {/* Mastery */}
        <div data-ocid="char-stats.mastery_section">
          <SectionHeader>Mastery</SectionHeader>
          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
              }}
            >
              <span
                style={{
                  fontSize: 11,
                  fontWeight: 700,
                  color: tierColor,
                  textShadow: `0 0 8px ${tierColor}55`,
                }}
                data-ocid="char-stats.mastery_tier"
              >
                {tierLabel}
              </span>
              <span style={{ fontSize: 10, color: "#9ca3af" }}>
                {snapshot.masteryPoints} /{" "}
                {xpThreshold === Number.POSITIVE_INFINITY ? "∞" : xpThreshold}{" "}
                XP
              </span>
            </div>
            {/* XP progress bar */}
            <div
              style={{
                height: 7,
                borderRadius: 4,
                overflow: "hidden",
                background: "rgba(255,255,255,0.06)",
                border: `1px solid ${tierColor}44`,
              }}
              data-ocid="char-stats.mastery_xp_bar"
            >
              <div
                style={{
                  width: `${masteryPct}%`,
                  height: "100%",
                  background: tierColor,
                  boxShadow: `0 0 6px ${tierColor}88`,
                  borderRadius: 4,
                  transition: "width 0.4s ease",
                }}
              />
            </div>
          </div>
        </div>
      </div>
    </dialog>
  );
}
