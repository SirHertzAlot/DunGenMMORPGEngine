/**
 * InventoryHUD — displays loot items collected from the dungeon.
 * Reads loot items passed from the ECS player entity's Inventory component.
 * Shown as a compact grid of colored slots at bottom-right (above action buttons).
 */

import type React from "react";
import type { LootItemData } from "../types/loot";

const TIER_STYLES: Record<
  string,
  { border: string; bg: string; text: string; label: string }
> = {
  common: {
    border: "border-muted-foreground/40",
    bg: "bg-muted/30",
    text: "text-muted-foreground",
    label: "C",
  },
  rare: {
    border: "border-blue-500/60",
    bg: "bg-blue-950/40",
    text: "text-blue-400",
    label: "R",
  },
  epic: {
    border: "border-purple-500/60",
    bg: "bg-purple-950/40",
    text: "text-purple-400",
    label: "E",
  },
  legendary: {
    border: "border-amber-500/70",
    bg: "bg-amber-950/40",
    text: "text-amber-400",
    label: "L",
  },
};

const MAX_DISPLAY_SLOTS = 10;

interface InventoryHUDProps {
  items: LootItemData[];
  /** Override positioning. Defaults to fixed bottom-right above action buttons. */
  positionStyle?: React.CSSProperties;
}

export default function InventoryHUD({
  items,
  positionStyle,
}: InventoryHUDProps) {
  const displayItems = items.slice(-MAX_DISPLAY_SLOTS);
  const emptyCount = Math.max(0, MAX_DISPLAY_SLOTS - displayItems.length);

  const defaultStyle: React.CSSProperties = {
    position: "absolute",
    bottom: "230px",
    right: "12px",
    zIndex: 45,
  };

  return (
    <div
      style={positionStyle ?? defaultStyle}
      className="flex items-center gap-1 pointer-events-none"
      data-ocid="inventory-hud"
    >
      <div
        className="flex flex-col gap-1 px-2 py-1.5 rounded-lg shadow-lg backdrop-blur-sm"
        style={{
          background: "#1a1208cc",
          border: "1px solid #c9a22760",
        }}
      >
        <span
          className="text-xs font-mono shrink-0"
          style={{ color: "#c9a227", fontSize: "9px" }}
        >
          BAG {items.length}/20
        </span>
        <div className="flex flex-wrap gap-1" style={{ maxWidth: "180px" }}>
          {displayItems.map((item) => {
            const style = TIER_STYLES[item.tier] ?? TIER_STYLES.common;
            return (
              <div
                key={item.id}
                title={`${item.name} (${item.tier})`}
                className={`w-7 h-7 rounded border ${style.border} ${style.bg} flex items-center justify-center shrink-0`}
              >
                <span
                  className={`text-xs font-bold leading-none ${style.text}`}
                >
                  {style.label}
                </span>
              </div>
            );
          })}
          {Array.from(
            { length: emptyCount },
            (_, i) => `slot-empty-${items.length + i}`,
          ).map((slotKey) => (
            <div
              key={slotKey}
              className="w-7 h-7 rounded border border-border/20 bg-muted/10 shrink-0"
            />
          ))}
        </div>
      </div>
    </div>
  );
}
