import React, { useEffect, useState } from "react";
import type { InteractableObject } from "../types/interaction";

interface InteractionPromptProps {
  activePrompt: InteractableObject | null;
  onInteract?: (object: InteractableObject) => void;
}

const INTERACTION_ICONS: Record<string, string> = {
  pickup: "🎒",
  interact: "⚙️",
  open: "🚪",
  talk: "💬",
};

export default function InteractionPrompt({
  activePrompt,
  onInteract,
}: InteractionPromptProps) {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (activePrompt) {
      setVisible(true);
    } else {
      const timer = setTimeout(() => setVisible(false), 200);
      return () => clearTimeout(timer);
    }
  }, [activePrompt]);

  if (!visible && !activePrompt) return null;

  const prompt = activePrompt;

  return (
    <div
      className={`fixed left-1/2 -translate-x-1/2 z-50 transition-all duration-200 ${
        activePrompt
          ? "bottom-36 opacity-100 translate-y-0"
          : "bottom-32 opacity-0 translate-y-2"
      }`}
      style={{ pointerEvents: "none" }}
    >
      {prompt && (
        <button
          type="button"
          className="flex items-center gap-2 px-4 py-2 rounded-lg bg-gray-900/95 border border-yellow-500/60 shadow-lg shadow-yellow-500/10 backdrop-blur-sm"
          style={{ pointerEvents: "auto" }}
          onClick={() => onInteract?.(prompt)}
        >
          <span className="text-lg">
            {INTERACTION_ICONS[prompt.interactionType] || "❓"}
          </span>
          <div className="flex flex-col">
            <span className="text-yellow-300 font-bold text-sm">
              {prompt.promptText}
            </span>
            <span className="text-gray-400 text-xs">{prompt.name}</span>
          </div>
          <div className="ml-2 px-2 py-0.5 rounded bg-yellow-600/30 border border-yellow-500/50">
            <span className="text-yellow-300 text-xs font-mono font-bold">
              E
            </span>
          </div>
        </button>
      )}
    </div>
  );
}
