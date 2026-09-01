/**
 * Combat System
 *
 * Enables mobs to detect and attack nearby mobs.
 * Reads/writes components via Map API (entity.components.get).
 */

import type { Entity, System } from "../../types/runtime";
import { getRuntimeManager } from "../runtimeManager";
import { awardMasteryXP } from "../systems/masterySystem";

type CombatEventCallback = (
  attackerId: string,
  targetId: string,
  damage: number,
) => void;
type DeathEventCallback = (entityId: string) => void;

let onMobAttacked: CombatEventCallback | null = null;
let onMobDied: DeathEventCallback | null = null;

export function setCombatCallbacks(
  attackedCb: CombatEventCallback,
  diedCb: DeathEventCallback,
): void {
  onMobAttacked = attackedCb;
  onMobDied = diedCb;
}

function distance2D(aTransform: any, bTransform: any): number {
  const dx = (aTransform.position?.x ?? 0) - (bTransform.position?.x ?? 0);
  const dz = (aTransform.position?.z ?? 0) - (bTransform.position?.z ?? 0);
  return Math.sqrt(dx * dx + dz * dz);
}

export const CombatSystem: System = {
  id: "combat-system",
  name: "Combat System",
  priority: 15,
  requiredComponents: ["Transform", "Health", "CombatTarget"],
  enabled: true,

  execute(entities: Entity[], _deltaTime: number): void {
    const runtime = getRuntimeManager();
    const now = Date.now();

    const combatEntities = entities.filter(
      (e) =>
        e.active &&
        e.components.has("Transform") &&
        e.components.has("Health") &&
        e.components.has("CombatTarget"),
    );

    const toDestroy: string[] = [];

    for (const entity of combatEntities) {
      if (!entity.active) continue;

      const transform = entity.components.get("Transform") as any;
      const health = entity.components.get("Health") as any;
      const combat = entity.components.get("CombatTarget") as any;

      const curHp = health?.current ?? health?.hp ?? 0;
      if (curHp <= 0) continue;

      let nearestTarget: Entity | null = null;
      let nearestDist = Number.POSITIVE_INFINITY;

      for (const other of combatEntities) {
        if (other.id === entity.id || !other.active) continue;
        const otherHealth = other.components.get("Health") as any;
        const otherHp = otherHealth?.current ?? otherHealth?.hp ?? 0;
        if (otherHp <= 0) continue;

        const otherTransform = other.components.get("Transform") as any;
        const dist = distance2D(transform, otherTransform);
        const attackRange = combat.attackRange ?? 6;

        if (dist <= attackRange && dist < nearestDist) {
          nearestDist = dist;
          nearestTarget = other;
        }
      }

      if (nearestTarget) {
        combat.targetEntityId = nearestTarget.id;

        const cooldown = combat.attackCooldown ?? 1200;
        const lastAttack =
          combat.lastAttackTimestamp ?? combat.lastAttackTime ?? 0;

        if (now - lastAttack >= cooldown) {
          combat.lastAttackTimestamp = now;
          combat.lastAttackTime = now;

          const targetHealth = nearestTarget.components.get("Health") as any;
          const damage = combat.attackDamage ?? 10;
          const newHp = Math.max(
            0,
            (targetHealth?.current ?? targetHealth?.hp ?? 0) - damage,
          );
          if (targetHealth) {
            targetHealth.current = newHp;
            if (targetHealth.hp !== undefined) targetHealth.hp = newHp;
          }

          if (nearestTarget.components.has("WanderBehavior")) {
            const targetWander = nearestTarget.components.get(
              "WanderBehavior",
            ) as any;
            targetWander.state = "idle";
            targetWander.targetPosition = null;
          }

          onMobAttacked?.(entity.id, nearestTarget.id, damage);
          awardMasteryXP(entity, "combat", 10);

          if (newHp <= 0) {
            toDestroy.push(nearestTarget.id);
          }
        }
      } else {
        combat.targetEntityId = null;
      }
    }

    for (const deadId of toDestroy) {
      const deadEntity = runtime.getEntity(deadId);
      if (deadEntity?.active) {
        deadEntity.active = false;
        // Fire death callback BEFORE destroying so callers can read entity data
        onMobDied?.(deadId);
        runtime.destroyEntity(deadId);
      }
    }
  },
};
