/**
 * MobInspectionPanel
 *
 * Fixed panel showing real-time stats for the selected mob.
 * Accepts an Entity directly (Map-based components) or a mobId + runtime.
 * Reads ECS data via entity.components.get() (Map API).
 */

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Brain, Heart, MapPin, Tag, X } from "lucide-react";
import React from "react";
import type { RuntimeManager } from "../core/runtime/runtimeManager";
import type { Entity } from "../types/runtime";

// Support both usage patterns
interface MobInspectionPanelPropsWithEntity {
  entity: Entity;
  onClose: () => void;
  mobId?: never;
  runtime?: never;
}

interface MobInspectionPanelPropsWithRuntime {
  mobId: string;
  runtime: RuntimeManager;
  onClose: () => void;
  entity?: never;
}

type MobInspectionPanelProps =
  | MobInspectionPanelPropsWithEntity
  | MobInspectionPanelPropsWithRuntime;

export default function MobInspectionPanel(props: MobInspectionPanelProps) {
  let entity: Entity | undefined;
  let mobId: string;

  if ("entity" in props && props.entity) {
    entity = props.entity;
    mobId = entity.id;
  } else if ("mobId" in props && props.mobId && props.runtime) {
    entity = props.runtime.getEntity(props.mobId);
    mobId = props.mobId;
  } else {
    return null;
  }

  if (!entity) return null;

  const transform = entity.components.get("Transform") as any;
  const health = entity.components.get("Health") as any;
  const ai = entity.components.get("AI") as any;
  const wander = entity.components.get("WanderBehavior") as any;

  const posX = (transform?.position?.x ?? 0).toFixed(2);
  const posZ = (transform?.position?.z ?? 0).toFixed(2);

  const currentHp = health?.current ?? health?.hp ?? 0;
  const maxHp = health?.max ?? health?.maxHp ?? 1;
  const hpPct = Math.max(0, Math.min(100, (currentHp / maxHp) * 100));

  const mobType = ai?.mobType ?? ai?.behaviorType ?? "mob";
  const aiState = ai?.currentState ?? ai?.state ?? "unknown";
  const wanderState = wander?.state ?? null;
  const displayState = wanderState ?? aiState;

  const hpColor =
    hpPct > 60 ? "bg-green-500" : hpPct > 30 ? "bg-yellow-500" : "bg-red-500";

  return (
    <div className="absolute top-4 right-4 z-30 w-56">
      <Card className="bg-black/80 backdrop-blur-sm border border-white/20 text-white shadow-xl">
        <CardHeader className="pb-2 pt-3 px-3">
          <div className="flex items-center justify-between">
            <CardTitle className="text-sm font-bold text-yellow-400 flex items-center gap-1.5">
              <Tag className="w-3.5 h-3.5" />
              Mob Inspector
            </CardTitle>
            <button
              type="button"
              onClick={props.onClose}
              className="text-white/40 hover:text-white transition-colors"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </div>
        </CardHeader>
        <CardContent className="px-3 pb-3 space-y-2.5">
          <div className="flex items-center gap-2">
            <span className="text-white/50 text-xs w-14 shrink-0">ID</span>
            <span className="text-white/90 text-xs font-mono truncate">
              {mobId.slice(0, 12)}…
            </span>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-white/50 text-xs w-14 shrink-0">Type</span>
            <span className="text-white/90 text-xs capitalize">{mobType}</span>
          </div>

          <div className="space-y-1">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-1.5">
                <Heart className="w-3 h-3 text-red-400" />
                <span className="text-white/50 text-xs">Health</span>
              </div>
              <span className="text-white/90 text-xs font-mono">
                {currentHp} / {maxHp}
              </span>
            </div>
            <div className="w-full h-1.5 bg-white/10 rounded-full overflow-hidden">
              <div
                className={`h-full rounded-full transition-all ${hpColor}`}
                style={{ width: `${hpPct}%` }}
              />
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Brain className="w-3 h-3 text-blue-400 shrink-0" />
            <span className="text-white/50 text-xs w-10 shrink-0">State</span>
            <span className="text-blue-300 text-xs capitalize">
              {displayState}
            </span>
          </div>

          <div className="flex items-center gap-2">
            <MapPin className="w-3 h-3 text-purple-400 shrink-0" />
            <span className="text-white/50 text-xs w-10 shrink-0">Pos</span>
            <span className="text-purple-300 text-xs font-mono">
              ({posX}, {posZ})
            </span>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
