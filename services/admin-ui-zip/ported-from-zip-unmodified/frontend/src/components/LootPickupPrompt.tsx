/**
 * LootPickupPrompt — shows a floating HUD prompt when a loot item is nearby.
 * Positioned as an absolute overlay over the dungeon canvas.
 */

import React, { useEffect, useState } from "react";
import type { LootItemData } from "../types/loot";

interface LootPickupPromptProps {
  lootEntityId: string | null;
  itemData: LootItemData | null;
  onPickup: () => void;
}

const TIER_LABELS: Record<string, { label: string; color: string }> = {
  common: { label: "Common", color: "text-muted-foreground" },
  rare: { label: "Rare", color: "text-blue-400" },
  epic: { label: "Epic", color: "text-purple-400" },
  legendary: { label: "Legendary", color: "text-amber-400" },
};

export default function LootPickupPrompt({
  lootEntityId,
  itemData,
  onPickup,
}: LootPickupPromptProps) {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (lootEntityId && itemData) {
      setVisible(true);
    } else {
      setVisible(false);
    }
  }, [lootEntityId, itemData]);

  // Mobile tap + keyboard E
  useEffect(() => {
    if (!visible) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === "e" || e.key === "E") {
        e.preventDefault();
        onPickup();
      }
    };
    window.addEventListener("keydown", handler);
    return () => {
      window.removeEventListener("keydown", handler);
    };
  }, [visible, onPickup]);

  if (!visible || !itemData) return null;

  const tier = TIER_LABELS[itemData.tier] ?? {
    label: itemData.tier,
    color: "text-foreground",
  };

  return (
    <div
      className="absolute bottom-36 left-1/2 -translate-x-1/2 z-30 pointer-events-auto"
      data-ocid="loot-pickup-prompt"
    >
      <button
        type="button"
        onClick={onPickup}
        className="flex flex-col items-center gap-1 bg-card/90 border border-border rounded-xl px-5 py-3 shadow-lg backdrop-blur-sm hover:bg-card active:scale-95 transition-all"
        aria-label={`Pick up ${itemData.name}`}
      >
        <span className="text-xs text-muted-foreground font-mono uppercase tracking-widest">
          Press E to pick up
        </span>
        <span className="font-bold text-sm text-foreground">
          {itemData.name}
        </span>
        <span className={`text-xs font-semibold ${tier.color}`}>
          {tier.label}
        </span>
      </button>
    </div>
  );
}
