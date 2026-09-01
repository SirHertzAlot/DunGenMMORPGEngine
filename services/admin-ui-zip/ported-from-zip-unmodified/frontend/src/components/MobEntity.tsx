/**
 * MobEntity
 *
 * Renders a single mob as a colored box with a floating EnemyHealthBarOverlay.
 * Reads fresh Transform data from the ECS Map-based components on every useFrame call.
 * Position changes written by the wander system are immediately reflected in the mesh.
 */

import { Html } from "@react-three/drei";
import { useFrame } from "@react-three/fiber";
import React, { useRef, useEffect } from "react";
import * as THREE from "three";
import type { Entity, Masterable } from "../types/runtime";
import type { EnemyEffect } from "./EnemyEffectBadges";
import EnemyHealthBarOverlay from "./EnemyHealthBarOverlay";

interface MobEntityProps {
  entityId: string;
  runtime: { getEntity: (id: string) => Entity | undefined };
  onClick?: (entityId: string) => void;
  isSelected?: boolean;
}

const MOB_COLORS: Record<string, number> = {
  goblin: 0x4ade80,
  skeleton: 0xe2e8f0,
  orc: 0xfb923c,
  troll: 0xa78bfa,
  wraith: 0x67e8f9,
};

const MOB_DISPLAY_NAMES: Record<string, string> = {
  goblin: "Goblin",
  skeleton: "Skeleton",
  orc: "Orc Warrior",
  troll: "Cave Troll",
  wraith: "Shadow Wraith",
};

const MOB_DANGER_RATINGS: Record<string, number> = {
  goblin: 1,
  skeleton: 2,
  orc: 3,
  troll: 4,
  wraith: 5,
};

export default function MobEntity({
  entityId,
  runtime,
  onClick,
  isSelected,
}: MobEntityProps) {
  const meshRef = useRef<THREE.Mesh>(null);
  const ringRef = useRef<THREE.Mesh>(null);
  const healthFillRef = useRef<THREE.Mesh>(null);
  const healthBgRef = useRef<THREE.Mesh>(null);

  // Read initial state for first render
  const entity = runtime.getEntity(entityId);
  const initTransform = entity?.components.get("Transform") as any;
  const initPos = initTransform?.position ?? { x: 0, y: 0, z: 0 };
  const initAI = entity?.components.get("AI") as any;
  const _initMobType = initAI?.mobType ?? initAI?.behaviorType ?? "goblin";

  // biome-ignore lint/correctness/useExhaustiveDependencies: intentional mount-only effect — seeds initial mesh position from ECS; initPos is stable at mount time
  useEffect(() => {
    if (meshRef.current) {
      meshRef.current.position.set(initPos.x, initPos.y + 5, initPos.z);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entityId]);

  useFrame(() => {
    const ent = runtime.getEntity(entityId);
    if (!ent || !meshRef.current) return;

    // Always read fresh Transform from ECS Map
    const transform = ent.components.get("Transform") as any;
    if (!transform?.position) return;

    const { x, y, z } = transform.position;

    // Update Three.js mesh position directly — no React state, no stale closure
    meshRef.current.position.x = x;
    meshRef.current.position.y = y + 5;
    meshRef.current.position.z = z;

    if (ringRef.current) {
      ringRef.current.position.x = x;
      ringRef.current.position.y = 0.1;
      ringRef.current.position.z = z;
    }

    // Update health bar positions
    const health = ent.components.get("Health") as any;
    if (health && healthFillRef.current && healthBgRef.current) {
      const cur = health.current ?? health.hp ?? 100;
      const max = health.max ?? health.maxHp ?? 100;
      const pct = max > 0 ? Math.max(0, cur / max) : 0;
      const hpColor = pct > 0.6 ? 0x22c55e : pct > 0.3 ? 0xeab308 : 0xef4444;
      (healthFillRef.current.material as THREE.MeshBasicMaterial).color.setHex(
        hpColor,
      );
      healthFillRef.current.scale.x = Math.max(0.001, pct);
      healthFillRef.current.position.set(x - 1.5 * (1 - pct), y + 8, z);
      healthBgRef.current.position.set(x, y + 8, z);
    }
  });

  if (!entity) return null;

  const health = entity.components.get("Health") as any;
  const ai = entity.components.get("AI") as any;
  const buffState = entity.components.get("BuffDebuffState") as any;
  const dangerComp = entity.components.get("DangerRating") as any;
  const masterable = entity.components.get("masterable") as
    | Masterable
    | undefined;

  const mobType = ai?.mobType ?? ai?.behaviorType ?? "goblin";
  const bodyColor = MOB_COLORS[mobType] ?? 0x4ade80;
  const displayName = MOB_DISPLAY_NAMES[mobType] ?? mobType;
  const dangerRating =
    dangerComp?.rating ??
    dangerComp?.dangerRating ??
    MOB_DANGER_RATINGS[mobType] ??
    1;
  const level = dangerRating;

  const curHp = health?.current ?? health?.hp ?? 100;
  const maxHp = health?.max ?? health?.maxHp ?? 100;
  const pct = maxHp > 0 ? Math.max(0, curHp / maxHp) : 1;
  const hpColor = pct > 0.6 ? 0x22c55e : pct > 0.3 ? 0xeab308 : 0xef4444;

  const activeBuffs: EnemyEffect[] = (buffState?.activeBuffs ?? []).map(
    (b: any) => ({
      effectId: b.effectId ?? b.name,
      name: b.name,
      isBuff: true,
      icon: b.icon,
      description: b.description,
    }),
  );
  const activeDebuffs: EnemyEffect[] = (buffState?.activeDebuffs ?? []).map(
    (d: any) => ({
      effectId: d.effectId ?? d.name,
      name: d.name,
      isBuff: false,
      icon: d.icon,
      description: d.description,
    }),
  );

  return (
    <group>
      {/* Selection ring */}
      {isSelected && (
        <mesh
          ref={ringRef}
          position={[initPos.x, 0.1, initPos.z]}
          rotation={[-Math.PI / 2, 0, 0]}
        >
          <ringGeometry args={[3.5, 4.5, 32]} />
          <meshBasicMaterial
            color={0xfbbf24}
            transparent
            opacity={0.85}
            side={THREE.DoubleSide}
          />
        </mesh>
      )}

      {/* Mob body */}
      {/* biome-ignore lint/a11y/useKeyWithClickEvents: R3F mesh — not a DOM element, keyboard events inapplicable */}
      <mesh
        ref={meshRef}
        position={[initPos.x, initPos.y + 5, initPos.z]}
        onClick={(e) => {
          e.stopPropagation();
          onClick?.(entityId);
        }}
        castShadow
      >
        <boxGeometry args={[3, 6, 3]} />
        <meshStandardMaterial
          color={bodyColor}
          emissive={isSelected ? 0xfbbf24 : 0x000000}
          emissiveIntensity={isSelected ? 0.25 : 0}
          roughness={0.6}
          metalness={0.1}
        />
      </mesh>

      {/* Health bar background */}
      <mesh ref={healthBgRef} position={[initPos.x, initPos.y + 8, initPos.z]}>
        <planeGeometry args={[3, 0.4]} />
        <meshBasicMaterial
          color={0x1f2937}
          transparent
          opacity={0.8}
          side={THREE.DoubleSide}
        />
      </mesh>

      {/* Health bar fill */}
      <mesh
        ref={healthFillRef}
        position={[initPos.x - 1.5 * (1 - pct), initPos.y + 8, initPos.z]}
      >
        <planeGeometry args={[3, 0.4]} />
        <meshBasicMaterial
          color={hpColor}
          transparent
          opacity={0.9}
          side={THREE.DoubleSide}
        />
      </mesh>

      {/* HTML overlay */}
      <Html
        position={[initPos.x, initPos.y + 11, initPos.z]}
        center
        distanceFactor={80}
        occlude={false}
        style={{ pointerEvents: "none" }}
      >
        <EnemyHealthBarOverlay
          name={displayName}
          level={level}
          dangerRating={dangerRating}
          healthCurrent={curHp}
          healthMax={maxHp}
          activeBuffs={activeBuffs}
          activeDebuffs={activeDebuffs}
          masteryTier={masterable?.masteryTier ?? null}
          masteryLevel={masterable?.masteryLevel ?? 0}
        />
      </Html>
    </group>
  );
}
