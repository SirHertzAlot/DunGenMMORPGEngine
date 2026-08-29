/**
 * ActionButtonsHUD — 6 circular action buttons at bottom-right.
 * Context-sensitive glow states for Attack, Defend, Interact.
 * Inventory, Character, Skills buttons with active/toggled blue glow.
 */

import React from "react";

interface ActionButtonsHUDProps {
  onAttack?: () => void;
  onDefend?: () => void;
  onInteract?: () => void;
  onInventory?: () => void;
  onCharacter?: () => void;
  onSkills?: () => void;
  isNearHostile?: boolean;
  isUnderAttack?: boolean;
  isNearInteractable?: boolean;
  inventoryOpen?: boolean;
  characterOpen?: boolean;
  skillsOpen?: boolean;
}

interface ActionButton {
  id: string;
  icon: string;
  label: string;
  keyHint: string;
  tooltip: string;
  onClick?: () => void;
  glowClass: string;
}

export default function ActionButtonsHUD({
  onAttack,
  onDefend,
  onInteract,
  onInventory,
  onCharacter,
  onSkills,
  isNearHostile = false,
  isUnderAttack = false,
  isNearInteractable = false,
  inventoryOpen = false,
  characterOpen = false,
  skillsOpen = false,
}: ActionButtonsHUDProps) {
  const buttons: ActionButton[] = [
    {
      id: "attack",
      icon: "⚔",
      label: "Attack",
      keyHint: "Q",
      tooltip: "Attack nearest hostile (Q)",
      onClick: onAttack,
      glowClass: isNearHostile ? "glow-red" : "",
    },
    {
      id: "defend",
      icon: "🛡",
      label: "Defend",
      keyHint: "E",
      tooltip: "Raise defenses (E)",
      onClick: onDefend,
      glowClass: isUnderAttack ? "glow-gold" : "",
    },
    {
      id: "interact",
      icon: "🤲",
      label: "Interact",
      keyHint: "F",
      tooltip: "Interact with nearby object (F)",
      onClick: onInteract,
      glowClass: isNearInteractable ? "glow-green" : "",
    },
    {
      id: "inventory",
      icon: "🎒",
      label: "Bag",
      keyHint: "I",
      tooltip: "Open inventory (I)",
      onClick: onInventory,
      glowClass: inventoryOpen ? "glow-blue" : "",
    },
    {
      id: "character",
      icon: "👤",
      label: "Char",
      keyHint: "C",
      tooltip: "Character sheet (C)",
      onClick: onCharacter,
      glowClass: characterOpen ? "glow-blue" : "",
    },
    {
      id: "skills",
      icon: "✨",
      label: "Skills",
      keyHint: "S",
      tooltip: "Skill tree (S)",
      onClick: onSkills,
      glowClass: skillsOpen ? "glow-blue" : "",
    },
  ];

  return (
    <>
      <style>{`
        .action-btn {
          width: 48px;
          height: 48px;
          min-width: 48px;
          min-height: 48px;
          border-radius: 50%;
          background: #1a1208;
          border: 2px solid #c9a227;
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          cursor: pointer;
          position: relative;
          transition: transform 0.15s ease, box-shadow 0.2s ease;
          -webkit-tap-highlight-color: transparent;
        }
        .action-btn:hover {
          transform: scale(1.08);
          border-color: #e8be45;
        }
        .action-btn:active {
          transform: scale(0.95);
        }
        .action-btn.glow-red {
          box-shadow: 0 0 10px 3px #dc2626, 0 0 20px 4px #dc262640;
          border-color: #ef4444;
          animation: pulse-red 1.4s ease-in-out infinite;
        }
        .action-btn.glow-gold {
          box-shadow: 0 0 10px 3px #c9a227, 0 0 20px 4px #c9a22740;
          border-color: #e8be45;
          animation: pulse-gold 1.4s ease-in-out infinite;
        }
        .action-btn.glow-green {
          box-shadow: 0 0 10px 3px #16a34a, 0 0 20px 4px #16a34a40;
          border-color: #22c55e;
          animation: pulse-green 1.4s ease-in-out infinite;
        }
        .action-btn.glow-blue {
          box-shadow: 0 0 8px 2px #1a6fc4, 0 0 16px 4px #1a6fc440;
          border-color: #3b9eff;
        }
        @keyframes pulse-red {
          0%, 100% { box-shadow: 0 0 10px 3px #dc2626, 0 0 20px 4px #dc262640; }
          50% { box-shadow: 0 0 16px 6px #dc2626, 0 0 28px 8px #dc262660; }
        }
        @keyframes pulse-gold {
          0%, 100% { box-shadow: 0 0 10px 3px #c9a227, 0 0 20px 4px #c9a22740; }
          50% { box-shadow: 0 0 16px 6px #c9a227, 0 0 28px 8px #c9a22760; }
        }
        @keyframes pulse-green {
          0%, 100% { box-shadow: 0 0 10px 3px #16a34a, 0 0 20px 4px #16a34a40; }
          50% { box-shadow: 0 0 16px 6px #16a34a, 0 0 28px 8px #16a34a60; }
        }
      `}</style>

      <div
        style={{
          position: "absolute",
          bottom: "120px",
          right: "12px",
          zIndex: 50,
          display: "grid",
          gridTemplateColumns: "repeat(3, 48px)",
          gap: "8px",
          pointerEvents: "auto",
        }}
        data-ocid="action-buttons-hud"
      >
        {buttons.map((btn) => (
          <button
            key={btn.id}
            type="button"
            className={`action-btn ${btn.glowClass}`}
            onClick={btn.onClick}
            title={btn.tooltip}
            data-ocid={`action.${btn.id}_button`}
            aria-label={btn.tooltip}
          >
            {/* Icon */}
            <span
              style={{
                fontSize: "16px",
                lineHeight: 1,
                display: "block",
                marginBottom: "1px",
              }}
            >
              {btn.icon}
            </span>
            {/* Label */}
            <span
              style={{
                fontSize: "7px",
                color: "#c9a227",
                fontWeight: 700,
                lineHeight: 1,
                letterSpacing: "0.03em",
                textTransform: "uppercase",
              }}
            >
              {btn.label}
            </span>
            {/* Key hint */}
            <span
              style={{
                position: "absolute",
                top: "2px",
                right: "4px",
                fontSize: "6px",
                color: "#c9a22799",
                fontWeight: 600,
                lineHeight: 1,
              }}
            >
              {btn.keyHint}
            </span>
          </button>
        ))}
      </div>
    </>
  );
}
