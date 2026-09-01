import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import React, { useState, useEffect } from "react";
import { rollMasteryAdvancement } from "../lib/masteryRollEngine";
import type { MasteryRollResultData } from "../types/mastery";

interface MasteryAdvancementDialogProps {
  open: boolean;
  onClose: () => void;
  objectType: string;
  seed?: number;
  onSkillsUnlocked?: (count: number) => void;
}

export default function MasteryAdvancementDialog({
  open,
  onClose,
  objectType,
  seed = Date.now(),
  onSkillsUnlocked,
}: MasteryAdvancementDialogProps) {
  const [rolling, setRolling] = useState(false);
  const [result, setResult] = useState<MasteryRollResultData | null>(null);
  const [displayRoll1, setDisplayRoll1] = useState(0);
  const [displayRoll2, setDisplayRoll2] = useState(0);

  useEffect(() => {
    if (open) {
      setResult(null);
      setRolling(true);
      setDisplayRoll1(0);
      setDisplayRoll2(0);

      // Animate dice rolling
      let count = 0;
      const interval = setInterval(() => {
        setDisplayRoll1(Math.floor(Math.random() * 15) + 1);
        setDisplayRoll2(Math.floor(Math.random() * 15) + 1);
        count++;
        if (count >= 15) {
          clearInterval(interval);
          const rollResult = rollMasteryAdvancement(seed);
          setResult(rollResult);
          setDisplayRoll1(rollResult.roll1);
          setDisplayRoll2(rollResult.roll2);
          setRolling(false);
          onSkillsUnlocked?.(rollResult.skillsUnlocked);
        }
      }, 80);

      return () => clearInterval(interval);
    }
  }, [open, seed, onSkillsUnlocked]);

  const getResultMessage = (result: MasteryRollResultData) => {
    if (result.skillsUnlocked === 5)
      return {
        text: "🎉 LEGENDARY ROLL! 5 Skills Unlocked!",
        color: "text-yellow-400",
      };
    if (result.skillsUnlocked === 2)
      return {
        text: "✨ Excellent Roll! 2 Skills Unlocked!",
        color: "text-purple-400",
      };
    if (result.skillsUnlocked === 1)
      return { text: "⚡ 1 New Skill Unlocked!", color: "text-blue-400" };
    return { text: "No skills unlocked this time.", color: "text-gray-400" };
  };

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="bg-gray-900 border-gray-700 text-white max-w-sm">
        <DialogHeader>
          <DialogTitle className="text-yellow-400 text-center">
            ⚔️ Mastery Advancement
          </DialogTitle>
          <DialogDescription className="text-gray-400 text-center capitalize">
            {objectType} Mastery Roll
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col items-center gap-4 py-4">
          {/* Dice display */}
          <div className="flex items-center gap-4">
            <div
              className={`w-16 h-16 rounded-xl border-2 flex items-center justify-center text-3xl font-bold transition-all duration-75 ${
                rolling
                  ? "border-yellow-500 bg-yellow-900/30 animate-pulse"
                  : "border-yellow-400 bg-yellow-900/20"
              }`}
            >
              {displayRoll1}
            </div>
            <span className="text-gray-400 text-xl font-bold">+</span>
            <div
              className={`w-16 h-16 rounded-xl border-2 flex items-center justify-center text-3xl font-bold transition-all duration-75 ${
                rolling
                  ? "border-yellow-500 bg-yellow-900/30 animate-pulse"
                  : "border-yellow-400 bg-yellow-900/20"
              }`}
            >
              {displayRoll2}
            </div>
          </div>

          {/* Total */}
          {!rolling && result && (
            <>
              <div className="text-center">
                <div className="text-gray-400 text-sm">Total</div>
                <div className="text-4xl font-bold text-white">
                  {result.total}
                </div>
                <div className="text-xs text-gray-500 mt-1">
                  (15+ = 1 skill · 25+ = 2 skills · 30 = 5 skills)
                </div>
              </div>

              <div
                className={`text-center font-bold text-lg ${getResultMessage(result).color}`}
              >
                {getResultMessage(result).text}
              </div>

              <button
                type="button"
                onClick={onClose}
                className="px-6 py-2 rounded-lg bg-yellow-600 hover:bg-yellow-500 text-white font-bold transition-colors"
              >
                Continue
              </button>
            </>
          )}

          {rolling && (
            <div className="text-gray-400 text-sm animate-pulse">
              Rolling dice...
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
