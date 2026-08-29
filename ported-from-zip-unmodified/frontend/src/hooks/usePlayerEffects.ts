import { useCallback, useEffect, useRef, useState } from "react";

export interface ActiveEffect {
  effectId: string;
  name: string;
  isBuff: boolean;
  durationSeconds: number;
  startTimestamp: number;
  magnitude: number;
  description: string;
  icon: string;
}

export function usePlayerEffects() {
  const [effects, setEffects] = useState<ActiveEffect[]>([]);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    intervalRef.current = setInterval(() => {
      const now = Date.now();
      setEffects((prev) =>
        prev.filter((e) => {
          const elapsed = (now - e.startTimestamp) / 1000;
          return elapsed < e.durationSeconds;
        }),
      );
    }, 200);

    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, []);

  const addEffect = useCallback(
    (effect: Omit<ActiveEffect, "startTimestamp">) => {
      setEffects((prev) => {
        const filtered = prev.filter((e) => e.effectId !== effect.effectId);
        return [...filtered, { ...effect, startTimestamp: Date.now() }];
      });
    },
    [],
  );

  const removeEffect = useCallback((effectId: string) => {
    setEffects((prev) => prev.filter((e) => e.effectId !== effectId));
  }, []);

  const getRemainingDuration = useCallback(
    (effectId: string): number => {
      const effect = effects.find((e) => e.effectId === effectId);
      if (!effect) return 0;
      const elapsed = (Date.now() - effect.startTimestamp) / 1000;
      return Math.max(0, effect.durationSeconds - elapsed);
    },
    [effects],
  );

  const getDurationProgress = useCallback(
    (effectId: string): number => {
      const effect = effects.find((e) => e.effectId === effectId);
      if (!effect) return 0;
      const elapsed = (Date.now() - effect.startTimestamp) / 1000;
      return Math.max(0, 1 - elapsed / effect.durationSeconds);
    },
    [effects],
  );

  const activeBuffs = effects.filter((e) => e.isBuff);
  const activeDebuffs = effects.filter((e) => !e.isBuff);

  return {
    effects,
    activeBuffs,
    activeDebuffs,
    addEffect,
    removeEffect,
    getRemainingDuration,
    getDurationProgress,
  };
}
