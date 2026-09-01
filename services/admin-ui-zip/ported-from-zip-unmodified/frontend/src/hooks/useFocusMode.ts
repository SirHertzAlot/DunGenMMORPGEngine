import { useCallback, useEffect, useState } from "react";

interface UseFocusModeOptions {
  mobIds: string[];
}

export interface UseFocusModeReturn {
  isActive: boolean;
  currentIndex: number;
  totalMobs: number;
  focusedMobId: string | null;
  activate: () => void;
  deactivate: () => void;
  toggle: () => void;
  next: () => void;
  previous: () => void;
  focusMob: (mobId: string) => void;
}

export function useFocusMode({
  mobIds,
}: UseFocusModeOptions): UseFocusModeReturn {
  const [isActive, setIsActive] = useState(false);
  const [currentIndex, setCurrentIndex] = useState(0);

  // Auto-advance if focused mob disappears
  useEffect(() => {
    if (!isActive) return;
    if (mobIds.length === 0) {
      setIsActive(false);
      setCurrentIndex(0);
      return;
    }
    // Clamp index to valid range
    if (currentIndex >= mobIds.length) {
      setCurrentIndex(mobIds.length - 1);
    }
  }, [mobIds, isActive, currentIndex]);

  const activate = useCallback(() => {
    if (mobIds.length > 0) {
      setIsActive(true);
      setCurrentIndex(0);
    }
  }, [mobIds.length]);

  const deactivate = useCallback(() => {
    setIsActive(false);
  }, []);

  const toggle = useCallback(() => {
    if (isActive) {
      setIsActive(false);
    } else if (mobIds.length > 0) {
      setIsActive(true);
      setCurrentIndex(0);
    }
  }, [isActive, mobIds.length]);

  const next = useCallback(() => {
    if (mobIds.length === 0) return;
    setCurrentIndex((prev) => (prev + 1) % mobIds.length);
  }, [mobIds.length]);

  const previous = useCallback(() => {
    if (mobIds.length === 0) return;
    setCurrentIndex((prev) => (prev - 1 + mobIds.length) % mobIds.length);
  }, [mobIds.length]);

  const focusMob = useCallback(
    (mobId: string) => {
      const idx = mobIds.indexOf(mobId);
      if (idx !== -1) {
        setCurrentIndex(idx);
        setIsActive(true);
      }
    },
    [mobIds],
  );

  const focusedMobId =
    isActive && mobIds.length > 0
      ? mobIds[Math.min(currentIndex, mobIds.length - 1)]
      : null;

  return {
    isActive,
    currentIndex: Math.min(currentIndex, Math.max(0, mobIds.length - 1)),
    totalMobs: mobIds.length,
    focusedMobId,
    activate,
    deactivate,
    toggle,
    next,
    previous,
    focusMob,
  };
}
