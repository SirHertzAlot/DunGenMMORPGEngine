import { useCallback, useEffect, useRef, useState } from "react";

interface CooldownEntry {
  startTime: number;
  totalDuration: number; // seconds
}

export function useSkillCooldowns() {
  const [cooldowns, setCooldowns] = useState<Record<string, CooldownEntry>>({});
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    intervalRef.current = setInterval(() => {
      const now = Date.now();
      setCooldowns((prev) => {
        const updated: Record<string, CooldownEntry> = {};
        let changed = false;
        for (const [id, entry] of Object.entries(prev)) {
          const elapsed = (now - entry.startTime) / 1000;
          if (elapsed < entry.totalDuration) {
            updated[id] = entry;
          } else {
            changed = true;
          }
        }
        return changed ? updated : prev;
      });
    }, 100);

    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, []);

  const startCooldown = useCallback(
    (skillId: string, durationSeconds: number) => {
      setCooldowns((prev) => ({
        ...prev,
        [skillId]: { startTime: Date.now(), totalDuration: durationSeconds },
      }));
    },
    [],
  );

  const getRemainingCooldown = useCallback(
    (skillId: string): number => {
      const entry = cooldowns[skillId];
      if (!entry) return 0;
      const elapsed = (Date.now() - entry.startTime) / 1000;
      return Math.max(0, entry.totalDuration - elapsed);
    },
    [cooldowns],
  );

  const isCooldownActive = useCallback(
    (skillId: string): boolean => {
      return getRemainingCooldown(skillId) > 0;
    },
    [getRemainingCooldown],
  );

  const getCooldownProgress = useCallback(
    (skillId: string): number => {
      const entry = cooldowns[skillId];
      if (!entry) return 1;
      const elapsed = (Date.now() - entry.startTime) / 1000;
      return Math.min(1, elapsed / entry.totalDuration);
    },
    [cooldowns],
  );

  return {
    startCooldown,
    getRemainingCooldown,
    isCooldownActive,
    getCooldownProgress,
    cooldowns,
  };
}
