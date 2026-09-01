import React, { useEffect, useRef, useCallback } from "react";
import type { DungeonData } from "../lib/rotjsDungeonGenerator";
import { getRuntimeManager } from "../lib/runtimeManager";
import { CELL_SIZE } from "../types/dungeon3d";

interface MinimapHUDProps {
  dungeonData?: DungeonData | null;
}

const MINIMAP_SIZE = 160; // outer circle diameter px
const DRAW_SIZE = 140; // canvas inner drawing area (fits inside the border)
const UPDATE_MS = 500;

// Colours (hex only — no oklch)
const COL_BG = "#0f0a04";
const COL_FLOOR = "#3a3530";
const COL_CORRIDOR = "#4a4540";
const COL_DOOR = "#7a6a40";
const COL_BOSS = "#5a1a1a";
const COL_PLAYER = "#1a6fc4";
const COL_ENEMY = "#dc2626";
const COL_GOLD = "#c9a227";

export default function MinimapHUD({ dungeonData }: MinimapHUDProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  const draw = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const w = canvas.width;
    const h = canvas.height;
    const cx = w / 2;
    const cy = h / 2;
    const radius = w / 2 - 1;

    // Clear
    ctx.clearRect(0, 0, w, h);

    // Clip to circle
    ctx.save();
    ctx.beginPath();
    ctx.arc(cx, cy, radius, 0, Math.PI * 2);
    ctx.clip();

    // Background fill
    ctx.fillStyle = COL_BG;
    ctx.fillRect(0, 0, w, h);

    if (!dungeonData) {
      ctx.restore();
      ctx.fillStyle = COL_GOLD;
      ctx.font = "11px monospace";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillText("No dungeon", cx, cy);
      return;
    }

    const dw = dungeonData.width;
    const dh = dungeonData.height;

    // Scale so the full dungeon fits inside DRAW_SIZE circle
    const scale = Math.min(DRAW_SIZE / dw, DRAW_SIZE / dh);
    const offsetX = cx - (dw * scale) / 2;
    const offsetY = cy - (dh * scale) / 2;

    // Draw cells
    for (let y = 0; y < dh; y++) {
      for (let x = 0; x < dw; x++) {
        const cell = dungeonData.cells[y]?.[x];
        if (!cell || cell === 0) continue; // wall / empty = skip
        let color = COL_FLOOR;
        if (cell === 2) color = COL_CORRIDOR;
        else if (cell === 3) color = COL_DOOR;
        else if (cell === 4) color = COL_BOSS;
        ctx.fillStyle = color;
        ctx.fillRect(
          Math.round(offsetX + x * scale),
          Math.round(offsetY + y * scale),
          Math.max(1, Math.ceil(scale)),
          Math.max(1, Math.ceil(scale)),
        );
      }
    }

    // Read entity positions from RuntimeManager
    const rm = getRuntimeManager();
    const entities = rm.getAllEntities();

    for (const entity of entities) {
      if (!entity.active) continue;

      // Try both casing variants for Transform
      const transform =
        entity.components.get("Transform") ??
        entity.components.get("transform");
      if (!transform) continue;

      const pos = (
        transform as { position?: { x: number; y: number; z: number } }
      ).position;
      if (!pos) continue;

      // Convert world-space position to minimap grid coords
      // CELL_SIZE = 10 world units per tile
      const gridX = pos.x / CELL_SIZE;
      const gridY = pos.z / CELL_SIZE;

      const mapX = offsetX + gridX * scale;
      const mapY = offsetY + gridY * scale;

      const isPlayer = entity.id === "player-entity-0";

      if (isPlayer) {
        ctx.beginPath();
        ctx.arc(mapX, mapY, 4, 0, Math.PI * 2);
        ctx.fillStyle = COL_PLAYER;
        ctx.fill();
        // White outline so it's visible on dark floor
        ctx.strokeStyle = "#ffffff";
        ctx.lineWidth = 1;
        ctx.stroke();
      } else {
        // Check if it's an AI / mob entity
        const hasAI =
          entity.components.has("AI") ||
          entity.components.has("ai") ||
          entity.components.has("WanderBehavior") ||
          entity.components.has("wanderBehavior");
        const hasHealth =
          entity.components.has("Health") || entity.components.has("health");

        if (hasAI || hasHealth) {
          ctx.beginPath();
          ctx.arc(mapX, mapY, 2, 0, Math.PI * 2);
          ctx.fillStyle = COL_ENEMY;
          ctx.fill();
        }
      }
    }

    ctx.restore();
  }, [dungeonData]);

  // Interval-based updates
  useEffect(() => {
    draw();
    const id = setInterval(draw, UPDATE_MS);
    return () => clearInterval(id);
  }, [draw]);

  return (
    <div
      data-ocid="minimap.panel"
      style={{
        position: "absolute",
        top: 12,
        right: 12,
        zIndex: 50,
        width: MINIMAP_SIZE,
        height: MINIMAP_SIZE,
        pointerEvents: "none",
        userSelect: "none",
      }}
    >
      {/* Cardinal direction labels outside the circle */}
      {/* North */}
      <span
        style={{
          position: "absolute",
          top: -14,
          left: "50%",
          transform: "translateX(-50%)",
          color: COL_GOLD,
          fontSize: 10,
          fontWeight: 700,
          fontFamily: "monospace",
          letterSpacing: 1,
          lineHeight: 1,
        }}
      >
        N
      </span>
      {/* South */}
      <span
        style={{
          position: "absolute",
          bottom: -14,
          left: "50%",
          transform: "translateX(-50%)",
          color: COL_GOLD,
          fontSize: 10,
          fontWeight: 700,
          fontFamily: "monospace",
          letterSpacing: 1,
          lineHeight: 1,
        }}
      >
        S
      </span>
      {/* West */}
      <span
        style={{
          position: "absolute",
          left: -14,
          top: "50%",
          transform: "translateY(-50%)",
          color: COL_GOLD,
          fontSize: 10,
          fontWeight: 700,
          fontFamily: "monospace",
          letterSpacing: 1,
          lineHeight: 1,
        }}
      >
        W
      </span>
      {/* East */}
      <span
        style={{
          position: "absolute",
          right: -14,
          top: "50%",
          transform: "translateY(-50%)",
          color: COL_GOLD,
          fontSize: 10,
          fontWeight: 700,
          fontFamily: "monospace",
          letterSpacing: 1,
          lineHeight: 1,
        }}
      >
        E
      </span>

      {/* Circular compass frame */}
      <div
        style={{
          width: MINIMAP_SIZE,
          height: MINIMAP_SIZE,
          borderRadius: "50%",
          border: `2px solid ${COL_GOLD}`,
          boxShadow:
            "0 0 8px 2px rgba(201, 162, 39, 0.35), inset 0 0 12px rgba(0,0,0,0.7)",
          overflow: "hidden",
          background: COL_BG,
          position: "relative",
        }}
      >
        <canvas
          ref={canvasRef}
          width={MINIMAP_SIZE}
          height={MINIMAP_SIZE}
          style={{ display: "block", width: "100%", height: "100%" }}
        />
      </div>
    </div>
  );
}
