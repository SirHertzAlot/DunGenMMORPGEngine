import React from "react";
import {
  MASTERY_TIER_COLORS,
  MASTERY_TIER_LABELS,
  type MasteryTier,
} from "../types/mastery";

interface CraftsmanBadgeProps {
  masteryTier: MasteryTier | null;
  masteryLevel: number;
}

/**
 * Compact badge showing an entity's mastery tier + level.
 * Renders nothing when masteryTier is null/undefined.
 * Tier 7 (God) gets a bright border to evoke a radiant glow effect.
 */
export default function CraftsmanBadge({
  masteryTier,
  masteryLevel,
}: CraftsmanBadgeProps) {
  if (masteryTier == null) return null;

  const label = MASTERY_TIER_LABELS[masteryTier];
  const hex = MASTERY_TIER_COLORS[masteryTier];
  const isGod = masteryTier === 7;

  return (
    <span
      data-ocid="craftsman-badge"
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 3,
        fontSize: 11,
        fontWeight: 700,
        lineHeight: 1,
        padding: "2px 6px",
        borderRadius: 999,
        backgroundColor: `${hex}22`,
        border: `1px solid ${isGod ? "#f8fafc" : hex}`,
        color: isGod ? "#f8fafc" : hex,
        boxShadow: isGod ? `0 0 6px 1px ${hex}99` : undefined,
        whiteSpace: "nowrap",
        letterSpacing: "0.01em",
        userSelect: "none",
      }}
    >
      {label}
      <span style={{ opacity: 0.75, fontWeight: 500 }}>Lv.{masteryLevel}</span>
    </span>
  );
}
