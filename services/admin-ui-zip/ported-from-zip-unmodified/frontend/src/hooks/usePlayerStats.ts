import { useEffect, useRef, useState } from "react";
import { getRuntimeManager } from "../lib/runtimeManager";
import type { MasteryTier } from "../types/mastery";

export interface PlayerStats {
  name: string;
  level: number;
  xp: number;
  xpToNext: number;
  health: { current: number; max: number };
  mana: { current: number; max: number };
  stamina: { current: number; max: number };
  strength: number;
  masteryTier: MasteryTier | null;
  masteryXP: number;
  masteryLevel: number;
}

const DEFAULTS: PlayerStats = {
  name: "Hero",
  level: 1,
  xp: 0,
  xpToNext: 100,
  health: { current: 100, max: 100 },
  mana: { current: 50, max: 50 },
  stamina: { current: 75, max: 75 },
  strength: 10,
  masteryTier: null,
  masteryXP: 0,
  masteryLevel: 0,
};

const PLAYER_ID = "player-entity-0";

function readStats(entityId?: string | null): PlayerStats {
  try {
    const rm = getRuntimeManager();
    const entity = rm.getEntity(entityId ?? PLAYER_ID);
    if (!entity) {
      // When spectating a specific entity that isn't found yet,
      // return partial defaults with the entity ID as name fallback
      if (entityId) {
        return {
          ...DEFAULTS,
          name: entityId
            .replace(/-/g, " ")
            .replace(/\b\w/g, (c) => c.toUpperCase()),
        };
      }
      return { ...DEFAULTS };
    }

    const comps = entity.components;

    // Health
    const healthComp = comps.get("health") ?? comps.get("Health");
    const health = healthComp
      ? {
          current:
            healthComp.current ?? healthComp.hp ?? DEFAULTS.health.current,
          max: healthComp.max ?? healthComp.maxHp ?? DEFAULTS.health.max,
        }
      : { ...DEFAULTS.health };

    // Mana
    const manaComp = comps.get("mana") ?? comps.get("Mana");
    const mana = manaComp
      ? {
          current: manaComp.current ?? DEFAULTS.mana.current,
          max: manaComp.max ?? DEFAULTS.mana.max,
        }
      : { ...DEFAULTS.mana };

    // Stamina
    const staminaComp = comps.get("stamina") ?? comps.get("Stamina");
    const stamina = staminaComp
      ? {
          current: staminaComp.current ?? DEFAULTS.stamina.current,
          max: staminaComp.max ?? DEFAULTS.stamina.max,
        }
      : { ...DEFAULTS.stamina };

    // XP / Level — also check MobMeta for mob entities
    const xpComp =
      comps.get("xp") ?? comps.get("XP") ?? comps.get("experience");
    const mobMeta = comps.get("MobMeta") ?? comps.get("mobMeta");
    const level: number =
      xpComp?.level ??
      comps.get("level")?.level ??
      mobMeta?.level ??
      DEFAULTS.level;
    const xp: number = xpComp?.current ?? xpComp?.xp ?? DEFAULTS.xp;
    const xpToNext: number = xpComp?.toNext ?? xpComp?.xpToNext ?? level * 100;

    // Name — check both generic name components and mob-specific MobMeta
    const nameComp =
      comps.get("name") ?? comps.get("Name") ?? comps.get("identity");
    const name: string =
      nameComp?.name ?? nameComp?.displayName ?? mobMeta?.name ?? DEFAULTS.name;

    // Level — also check MobMeta for mobs
    const statsComp =
      comps.get("stats") ?? comps.get("Stats") ?? comps.get("attributes");
    const strength: number =
      statsComp?.strength ?? statsComp?.str ?? DEFAULTS.strength;

    // Mastery
    const masteryComp = comps.get("masterable") ?? comps.get("Masterable");
    const masteryTier: MasteryTier | null = masteryComp?.masteryTier ?? null;
    const masteryXP: number = masteryComp?.masteryPoints ?? 0;
    const masteryLevel: number = masteryComp?.masteryLevel ?? 0;

    return {
      name,
      level,
      xp,
      xpToNext,
      health,
      mana,
      stamina,
      strength,
      masteryTier,
      masteryXP,
      masteryLevel,
    };
  } catch {
    return { ...DEFAULTS };
  }
}

export function usePlayerStats(
  pollMs = 500,
  entityId?: string | null,
): PlayerStats {
  const [stats, setStats] = useState<PlayerStats>(() => readStats(entityId));
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    setStats(readStats(entityId));
    intervalRef.current = setInterval(() => {
      setStats(readStats(entityId));
    }, pollMs);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [pollMs, entityId]);

  return stats;
}
