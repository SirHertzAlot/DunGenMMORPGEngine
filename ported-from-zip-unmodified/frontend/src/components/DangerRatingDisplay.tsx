import React from "react";

interface DangerRatingDisplayProps {
  dangerRating: number; // 1-5
  size?: number;
}

const DANGER_LABELS: Record<number, string> = {
  1: "Weak",
  2: "Moderate",
  3: "Dangerous",
  4: "Very Dangerous",
  5: "Extremely Dangerous",
};

export default function DangerRatingDisplay({
  dangerRating,
  size = 14,
}: DangerRatingDisplayProps) {
  const clamped = Math.max(1, Math.min(5, Math.round(dangerRating)));
  const label = DANGER_LABELS[clamped] || "Unknown";

  return (
    <div
      className="flex items-center gap-0.5"
      title={`Danger: ${label} (${clamped}/5 skulls)`}
    >
      {[0, 1, 2, 3, 4].map((i) => (
        <img
          key={`skull-${i}`}
          src="/assets/generated/skull-danger.dim_64x64.png"
          alt="skull"
          style={{
            width: size,
            height: size,
            opacity: i < clamped ? 1 : 0.2,
            filter: i < clamped ? "none" : "grayscale(100%)",
          }}
        />
      ))}
    </div>
  );
}
