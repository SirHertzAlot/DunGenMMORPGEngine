import React, { useEffect, useRef } from "react";

interface CooldownOverlayProps {
  remainingSeconds: number;
  totalSeconds: number;
  size?: number;
}

export default function CooldownOverlay({
  remainingSeconds,
  totalSeconds,
  size = 52,
}: CooldownOverlayProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const progress = totalSeconds > 0 ? remainingSeconds / totalSeconds : 0;
    const cx = size / 2;
    const cy = size / 2;
    const radius = size / 2 - 2;

    ctx.clearRect(0, 0, size, size);

    if (progress > 0) {
      // Dark overlay
      ctx.fillStyle = "rgba(0,0,0,0.65)";
      ctx.beginPath();
      ctx.arc(cx, cy, radius, 0, Math.PI * 2);
      ctx.fill();

      // Sweep arc (remaining portion)
      const startAngle = -Math.PI / 2;
      const endAngle = startAngle + progress * Math.PI * 2;
      ctx.fillStyle = "rgba(0,0,0,0.75)";
      ctx.beginPath();
      ctx.moveTo(cx, cy);
      ctx.arc(cx, cy, radius, startAngle, endAngle);
      ctx.closePath();
      ctx.fill();

      // Cooldown text
      ctx.fillStyle = "#ffffff";
      ctx.font = `bold ${size * 0.28}px sans-serif`;
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillText(
        remainingSeconds > 1
          ? Math.ceil(remainingSeconds).toString()
          : remainingSeconds.toFixed(1),
        cx,
        cy,
      );
    }
  }, [remainingSeconds, totalSeconds, size]);

  if (remainingSeconds <= 0) return null;

  return (
    <canvas
      ref={canvasRef}
      width={size}
      height={size}
      className="absolute inset-0 pointer-events-none rounded"
      style={{ width: size, height: size }}
    />
  );
}
