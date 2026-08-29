import React from "react";
import { usePlayerStats } from "../hooks/usePlayerStats";
import CraftsmanBadge from "./CraftsmanBadge";

// ── Tiny sub-components ────────────────────────────────────────────────────────

interface StatBarProps {
  label: string;
  current: number;
  max: number;
  color: string; // hex
  "data-ocid"?: string;
}

function StatBar({
  label,
  current,
  max,
  color,
  "data-ocid": ocid,
}: StatBarProps) {
  const pct = max > 0 ? Math.min(100, (current / max) * 100) : 0;
  return (
    <div data-ocid={ocid} className="flex flex-col gap-0.5">
      <div className="flex items-center justify-between">
        <span
          className="text-xs font-semibold leading-none"
          style={{ color, fontSize: 10 }}
        >
          {label}
        </span>
        <span
          className="text-xs leading-none tabular-nums"
          style={{ color: "#d4b896", fontSize: 10 }}
        >
          {current}/{max}
        </span>
      </div>
      {/* Track */}
      <div
        className="rounded-full overflow-hidden"
        style={{
          height: 7,
          backgroundColor: "rgba(255,255,255,0.08)",
          border: "1px solid rgba(201,162,39,0.2)",
        }}
      >
        <div
          className="h-full rounded-full transition-all duration-300"
          style={{
            width: `${pct}%`,
            backgroundColor: color,
            boxShadow: `0 0 6px ${color}88`,
          }}
        />
      </div>
    </div>
  );
}

// ── Main HUD component ─────────────────────────────────────────────────────────

interface PlayerStatusHUDProps {
  focusedEntityId?: string | null;
}

export default function PlayerStatusHUD({
  focusedEntityId,
}: PlayerStatusHUDProps) {
  const stats = usePlayerStats(500, focusedEntityId);
  const isSpectating = focusedEntityId != null;
  const xpPct =
    stats.xpToNext > 0 ? Math.min(100, (stats.xp / stats.xpToNext) * 100) : 0;

  // Initials for portrait
  const initials = stats.name
    .split(" ")
    .map((w) => w[0])
    .join("")
    .toUpperCase()
    .slice(0, 2);

  return (
    <div
      data-ocid="player-status-hud"
      style={{
        position: "absolute",
        top: 12,
        left: 12,
        zIndex: 40,
        pointerEvents: "none",
        userSelect: "none",
        width: 200,
      }}
    >
      <div
        style={{
          backgroundColor: "#1a1208",
          border: "2px solid #c9a227",
          borderRadius: 10,
          padding: "10px 12px 8px",
          boxShadow:
            "0 4px 24px rgba(0,0,0,0.7), 0 0 0 1px rgba(201,162,39,0.1)",
        }}
      >
        {/* Portrait + Name row */}
        <div className="flex items-center gap-2.5 mb-2">
          {/* Portrait circle */}
          <div
            data-ocid="player.portrait"
            style={{
              width: 46,
              height: 46,
              minWidth: 46,
              borderRadius: "50%",
              border: "2px solid #c9a227",
              backgroundColor: "#0f1f3d",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 15,
              fontWeight: 800,
              color: "#1a6fc4",
              boxShadow: "inset 0 0 10px rgba(26,111,196,0.3)",
              letterSpacing: "0.03em",
            }}
          >
            {initials || "?"}
          </div>

          {/* Name + Level */}
          <div className="flex flex-col min-w-0">
            <span
              data-ocid="player.name"
              style={{
                color: "#f5deb3",
                fontSize: 12,
                fontWeight: 700,
                lineHeight: 1.2,
                whiteSpace: "nowrap",
                overflow: "hidden",
                textOverflow: "ellipsis",
                maxWidth: 110,
              }}
            >
              {stats.name}
            </span>
            {isSpectating && (
              <span
                style={{
                  color: "#1a6fc4",
                  fontSize: 9,
                  fontWeight: 600,
                  lineHeight: 1.2,
                  letterSpacing: "0.04em",
                  textTransform: "uppercase",
                }}
              >
                Spectating
              </span>
            )}
            <div className="flex items-center gap-1.5 mt-0.5">
              <span
                data-ocid="player.level"
                style={{
                  backgroundColor: "#c9a22722",
                  border: "1px solid #c9a22766",
                  borderRadius: 4,
                  color: "#c9a227",
                  fontSize: 10,
                  fontWeight: 700,
                  padding: "1px 5px",
                  lineHeight: 1.4,
                }}
              >
                Lv.{stats.level}
              </span>
              <span
                style={{
                  color: "#9ca3af",
                  fontSize: 10,
                  lineHeight: 1.4,
                }}
              >
                STR {stats.strength}
              </span>
            </div>
          </div>
        </div>

        {/* Status bars */}
        <div className="flex flex-col gap-1.5 mb-2">
          <StatBar
            label="HP"
            current={stats.health.current}
            max={stats.health.max}
            color="#dc2626"
            data-ocid="player.health_bar"
          />
          <StatBar
            label="MP"
            current={stats.mana.current}
            max={stats.mana.max}
            color="#1a6fc4"
            data-ocid="player.mana_bar"
          />
          <StatBar
            label="SP"
            current={stats.stamina.current}
            max={stats.stamina.max}
            color="#16a34a"
            data-ocid="player.stamina_bar"
          />
        </div>

        {/* XP bar */}
        <div data-ocid="player.xp_bar" className="flex flex-col gap-0.5 mb-2">
          <div className="flex justify-between items-center">
            <span style={{ color: "#c9a227", fontSize: 10, fontWeight: 600 }}>
              XP
            </span>
            <span style={{ color: "#d4b896", fontSize: 10 }}>
              {stats.xp} / {stats.xpToNext}
            </span>
          </div>
          <div
            className="rounded-full overflow-hidden"
            style={{
              height: 5,
              backgroundColor: "rgba(255,255,255,0.08)",
              border: "1px solid rgba(201,162,39,0.2)",
            }}
          >
            <div
              className="h-full rounded-full transition-all duration-500"
              style={{
                width: `${xpPct}%`,
                backgroundColor: "#c9a227",
                boxShadow: "0 0 6px #c9a22788",
              }}
            />
          </div>
        </div>

        {/* Mastery badge */}
        {stats.masteryTier != null && (
          <div
            data-ocid="player.mastery_badge"
            style={{
              borderTop: "1px solid rgba(201,162,39,0.2)",
              paddingTop: 6,
            }}
          >
            <CraftsmanBadge
              masteryTier={stats.masteryTier}
              masteryLevel={stats.masteryLevel}
            />
          </div>
        )}
      </div>
    </div>
  );
}
