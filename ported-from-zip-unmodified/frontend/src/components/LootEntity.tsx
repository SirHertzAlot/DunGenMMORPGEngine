/**
 * LootEntity — renders a small glowing sphere for a loot item in the dungeon.
 * Rendered for every entity that has a LootItem component.
 */

import { useFrame } from "@react-three/fiber";
import React, { useRef } from "react";
import type { Mesh, PointLight } from "three";
import type { LootItemData } from "../types/loot";

interface LootEntityProps {
  entityId: string;
  position: { x: number; y: number; z: number };
  itemData: LootItemData;
  isNearby?: boolean;
}

const TIER_COLORS: Record<string, string> = {
  common: "#9ca3af",
  rare: "#3b82f6",
  epic: "#a855f7",
  legendary: "#f59e0b",
};

const TIER_EMISSIVE: Record<string, string> = {
  common: "#374151",
  rare: "#1d4ed8",
  epic: "#6b21a8",
  legendary: "#b45309",
};

export default function LootEntity({
  entityId: _entityId,
  position,
  itemData,
  isNearby,
}: LootEntityProps) {
  const meshRef = useRef<Mesh>(null);
  const lightRef = useRef<PointLight>(null);
  const bobOffset = useRef(Math.random() * Math.PI * 2);

  const color = TIER_COLORS[itemData.tier] ?? "#f59e0b";
  const emissive = TIER_EMISSIVE[itemData.tier] ?? "#b45309";
  const bobAmp = isNearby ? 1.5 : 1.0;

  useFrame((state) => {
    const t = state.clock.getElapsedTime() + bobOffset.current;
    if (meshRef.current) {
      meshRef.current.position.y = 2 + Math.sin(t * 2) * 0.8 * bobAmp;
      meshRef.current.rotation.y = t * 1.2;
    }
    if (lightRef.current) {
      lightRef.current.intensity = 0.6 + Math.sin(t * 3) * 0.2;
    }
  });

  return (
    <group position={[position.x, 0, position.z]}>
      <mesh ref={meshRef} castShadow>
        <sphereGeometry args={[1.5, 12, 12]} />
        <meshStandardMaterial
          color={color}
          emissive={emissive}
          emissiveIntensity={isNearby ? 1.2 : 0.6}
          metalness={0.6}
          roughness={0.3}
        />
      </mesh>
      <pointLight
        ref={lightRef}
        color={color}
        intensity={0.6}
        distance={20}
        decay={2}
      />
    </group>
  );
}
