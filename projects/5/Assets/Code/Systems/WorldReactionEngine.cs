using System.Collections.Generic;
using DunGen.ECS.Core;
using DunGen.ECS.Exploration;
using DunGen.Events;
using DunGen.Events.Combat;
using DunGen.Events.World;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DunGen.Systems
{
    /// <summary>
    /// Subscribes to combat events and drives NPC personality-based reactions.
    ///
    /// Reaction chain:
    ///  1. DamageInflictedEventData  → wounded NPC updates LastDamagedBy; nearby NPCs may join
    ///  2. DeathEventData            → nearby loyal NPCs rally; cowardly ones flee
    ///  3. CombatStartedEventData    → curious NPCs investigate; aggressive NPCs engage
    ///  4. NpcReactionEventData      → chain reaction: neighbouring NPCs respond to NPC reactions
    ///
    /// Every turn WorldMoodEventData is published summarising overall tension.
    /// </summary>
    public class WorldReactionEngine : System.IDisposable
    {
        private readonly EventBus _bus;
        private readonly EntityManager _entityManager;

        // proximity radius (Manhattan tiles) within which an NPC "witnesses" an event
        private const int WitnessRadius = 10;

        // faction mood tracker: archetype -> hostile reaction count this turn
        private readonly Dictionary<string, int> _factionHostileCount  = new();
        private readonly Dictionary<string, int> _factionTotalCount    = new();

        // turn-level accumulators
        private int _activeCombatants;
        private int _fleeingNpcs;
        private int _deadThisTurn;

        public WorldReactionEngine(EventBus bus, EntityManager entityManager)
        {
            _bus           = bus;
            _entityManager = entityManager;

            _bus.Subscribe<DamageInflictedEventData>(OnDamageInflicted);
            _bus.Subscribe<DeathEventData>(OnDeath);
            _bus.Subscribe<CombatStartedEventData>(OnCombatStarted);
            _bus.Subscribe<NpcReactionEventData>(OnNpcReacted);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<DamageInflictedEventData>(OnDamageInflicted);
            _bus.Unsubscribe<DeathEventData>(OnDeath);
            _bus.Unsubscribe<CombatStartedEventData>(OnCombatStarted);
            _bus.Unsubscribe<NpcReactionEventData>(OnNpcReacted);
        }

        // ---- event handlers ---------------------------------------------------

        private void OnDamageInflicted(DamageInflictedEventData ev)
        {
            _activeCombatants++;

            // Update LastDamagedBy on the victim so EnemyAI can target their attacker.
            // VictimEntityId maps to Entity.Index stored via EntityIndexCache.
            ForEachNpcNear(ev.VictimEntityId, (entity, personality, state, pos) =>
            {
                bool isVictim = entity.Index == ev.VictimEntityId;

                if (isVictim)
                {
                    // The victim records who hit them.
                    var updatedState = state;
                    updatedState.LastDamagedByEntityIndex = ev.VictimEntityId; // stored as "attacker" context
                    updatedState.LocalTension = (byte)Mathf.Min(100, state.LocalTension + 15);
                    _entityManager.SetComponentData(entity, updatedState);
                    return;
                }

                if (state.HasReactedThisTurn) return;

                var reaction = DecideReactionToDamage(personality, state, ev);
                if (reaction == NpcReactionType.None) return;

                PublishReaction(entity, personality, state, pos, ev.VictimEntityId, reaction);
            });
        }

        private void OnDeath(DeathEventData ev)
        {
            _deadThisTurn++;

            ForEachNpcNear(ev.DeceasedEntityId, (entity, personality, state, pos) =>
            {
                if (entity.Index == ev.DeceasedEntityId) return;
                if (state.HasReactedThisTurn) return;

                var reaction = DecideReactionToDeath(personality, state, ev);
                if (reaction == NpcReactionType.None) return;

                PublishReaction(entity, personality, state, pos, ev.DeceasedEntityId, reaction);
            });
        }

        private void OnCombatStarted(CombatStartedEventData ev)
        {
            ForEachNpcNearTile(ev.CombatPositionX, ev.CombatPositionY, ev.DungeonLevel,
                (entity, personality, state, pos) =>
                {
                    if (state.HasReactedThisTurn) return;

                    var reaction = DecideReactionToCombatStart(personality, state, ev);
                    if (reaction == NpcReactionType.None) return;

                    PublishReaction(entity, personality, state, pos, 0, reaction);
                });
        }

        private void OnNpcReacted(NpcReactionEventData ev)
        {
            // Chain reaction: NPCs near the reacting NPC can respond.
            ForEachNpcNearTile(ev.TileX, ev.TileY, ev.DungeonLevel,
                (entity, personality, state, pos) =>
                {
                    if (entity.Index == ev.ReactingEntityIndex) return;
                    if (state.HasReactedThisTurn) return;

                    // Only chain-react if the witnessed reaction is violent or a death-flee.
                    if (ev.Reaction != NpcReactionType.Engage &&
                        ev.Reaction != NpcReactionType.Flee  &&
                        ev.Reaction != NpcReactionType.Aid)
                        return;

                    var reaction = DecideChainReaction(personality, state, ev);
                    if (reaction == NpcReactionType.None) return;

                    PublishReaction(entity, personality, state, pos, ev.ReactingEntityIndex, reaction);
                });
        }

        // ---- turn-level API ---------------------------------------------------

        /// <summary>
        /// Call once per turn from GameSession.ExecuteTurn() to publish the
        /// WorldMoodEventData snapshot and reset per-turn counters.
        /// </summary>
        public void EndTurn()
        {
            // Aggregate faction moods.
            foreach (var kvp in _factionHostileCount)
            {
                string archetype = kvp.Key;
                int hostile = kvp.Value;
                int total = _factionTotalCount.TryGetValue(archetype, out int t) ? t : 1;

                string mood = hostile > total * 0.75f ? "hostile"
                            : hostile > total * 0.40f ? "agitated"
                            : "neutral";

                if (mood != "neutral")
                {
                    _bus.Publish(new FactionStateChangedEventData
                    {
                        EventId          = _bus.GetNextEventId(),
                        Timestamp        = Time.time,
                        FactionArchetype = archetype,
                        OldMood          = "neutral",
                        NewMood          = mood,
                        MemberCount      = total
                    });
                }
            }

            // Compute global tension from per-entity LocalTension average.
            byte globalTension = ComputeGlobalTension();

            _bus.Publish(new WorldMoodEventData
            {
                EventId          = _bus.GetNextEventId(),
                Timestamp        = Time.time,
                GlobalTension    = globalTension,
                ActiveCombatants = _activeCombatants,
                FleingNpcs       = _fleeingNpcs,
                DeadThisTurn     = _deadThisTurn
            });

            // Reset per-turn state.
            _activeCombatants = 0;
            _fleeingNpcs      = 0;
            _deadThisTurn     = 0;
            _factionHostileCount.Clear();
            _factionTotalCount.Clear();

            ClearReactedFlags();
        }

        // ---- decision logic ---------------------------------------------------

        private NpcReactionType DecideReactionToDamage(
            NpcPersonalityComponent p, NpcWorldStateComponent state, DamageInflictedEventData ev)
        {
            if (!_entityManager.Exists(new Entity { Index = ev.VictimEntityId, Version = 0 }))
                return NpcReactionType.None;

            // Loyal NPC rushes to aid a wounded ally.
            if (p.PrioritisesAllies)
                return NpcReactionType.Aid;

            // Vengeful NPC wants to engage whoever is hurting people.
            if (p.IsVengeful && p.Aggression > 50)
                return NpcReactionType.Engage;

            // Curious but non-aggressive NPCs investigate first.
            if (p.InvestigatesFirst && p.Aggression < 55)
                return NpcReactionType.Investigate;

            // Aggressive NPCs pile in.
            if (p.Aggression > 65)
                return NpcReactionType.Engage;

            return NpcReactionType.None;
        }

        private NpcReactionType DecideReactionToDeath(
            NpcPersonalityComponent p, NpcWorldStateComponent state, DeathEventData ev)
        {
            // Cowardly NPC: ally just died → run.
            if (p.Cowardice > 55)
            {
                _fleeingNpcs++;
                return NpcReactionType.Flee;
            }

            // Loyal NPC: avenge fallen ally.
            if (p.PrioritisesAllies && p.Aggression > 40)
                return NpcReactionType.Engage;

            // Vengeful NPC: same
            if (p.IsVengeful)
                return NpcReactionType.Engage;

            // Passive/curious NPC: stands guard.
            if (p.InvestigatesFirst)
                return NpcReactionType.Guard;

            return NpcReactionType.None;
        }

        private NpcReactionType DecideReactionToCombatStart(
            NpcPersonalityComponent p, NpcWorldStateComponent state, CombatStartedEventData ev)
        {
            if (p.Aggression > 70) return NpcReactionType.Engage;
            if (p.InvestigatesFirst) return NpcReactionType.Investigate;
            if (p.Cowardice > 60) return NpcReactionType.Flee;
            if (p.PrioritisesAllies) return NpcReactionType.Aid;
            return NpcReactionType.None;
        }

        private NpcReactionType DecideChainReaction(
            NpcPersonalityComponent p, NpcWorldStateComponent state, NpcReactionEventData trigger)
        {
            // Witnessing a fleeing NPC makes cowards flee too.
            if (trigger.Reaction == NpcReactionType.Flee && p.Cowardice > 45)
            {
                _fleeingNpcs++;
                return NpcReactionType.Flee;
            }

            // Witnessing combat triggers rally in aggressive/loyal NPCs.
            if (trigger.Reaction == NpcReactionType.Engage &&
                (p.Aggression > 60 || p.PrioritisesAllies))
                return NpcReactionType.Engage;

            // Curious NPCs always investigate whatever is happening nearby.
            if (p.InvestigatesFirst)
                return NpcReactionType.Investigate;

            return NpcReactionType.None;
        }

        // ---- helpers ----------------------------------------------------------

        private void PublishReaction(
            Entity entity,
            NpcPersonalityComponent personality,
            NpcWorldStateComponent state,
            PositionComponent pos,
            int stimulusEntityIndex,
            NpcReactionType reaction)
        {
            // Mark as reacted this turn.
            var updated = state;
            updated.HasReactedThisTurn = true;
            updated.LocalTension = (byte)Mathf.Min(100, state.LocalTension + 10);
            _entityManager.SetComponentData(entity, updated);

            // Track faction mood.
            string archetype = personality.ArchetypeName.ToString();
            _factionTotalCount.TryGetValue(archetype, out int total);
            _factionTotalCount[archetype] = total + 1;
            if (reaction == NpcReactionType.Engage || reaction == NpcReactionType.Aid)
            {
                _factionHostileCount.TryGetValue(archetype, out int hostile);
                _factionHostileCount[archetype] = hostile + 1;
            }

            _bus.Publish(new NpcReactionEventData
            {
                EventId             = _bus.GetNextEventId(),
                Timestamp           = Time.time,
                ReactingEntityIndex = entity.Index,
                ArchetypeName       = archetype,
                StimulusEntityIndex = stimulusEntityIndex,
                Reaction            = reaction,
                Aggression          = personality.Aggression,
                Cowardice           = personality.Cowardice,
                Loyalty             = personality.Loyalty,
                Vengefulness        = personality.Vengefulness,
                TileX               = pos.X,
                TileY               = pos.Y,
                DungeonLevel        = pos.DungeonLevel
            });

            Debug.Log($"[WorldReaction] {archetype} (entity {entity.Index}) → {reaction}" +
                      $" | Agg={personality.Aggression} Cow={personality.Cowardice}" +
                      $" Loy={personality.Loyalty} Ven={personality.Vengefulness}");
        }

        private void ForEachNpcNear(int anchorEntityIndex,
            System.Action<Entity, NpcPersonalityComponent, NpcWorldStateComponent, PositionComponent> callback)
        {
            if (!TryGetEntityPosition(anchorEntityIndex, out var anchorPos)) return;
            ForEachNpcNearTile(anchorPos.X, anchorPos.Y, anchorPos.DungeonLevel, callback);
        }

        private void ForEachNpcNearTile(int tileX, int tileY, int level,
            System.Action<Entity, NpcPersonalityComponent, NpcWorldStateComponent, PositionComponent> callback)
        {
            using var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NpcPersonalityComponent>(),
                ComponentType.ReadWrite<NpcWorldStateComponent>(),
                ComponentType.ReadOnly<PositionComponent>());

            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var pos = _entityManager.GetComponentData<PositionComponent>(entity);
                if (pos.DungeonLevel != level) continue;
                int dist = Mathf.Abs(pos.X - tileX) + Mathf.Abs(pos.Y - tileY);
                if (dist > WitnessRadius) continue;

                var personality = _entityManager.GetComponentData<NpcPersonalityComponent>(entity);
                var state       = _entityManager.GetComponentData<NpcWorldStateComponent>(entity);
                callback(entity, personality, state, pos);
            }
        }

        private bool TryGetEntityPosition(int entityIndex, out PositionComponent pos)
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PositionComponent>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (entity.Index == entityIndex)
                {
                    pos = _entityManager.GetComponentData<PositionComponent>(entity);
                    return true;
                }
            }
            pos = default;
            return false;
        }

        private byte ComputeGlobalTension()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<NpcWorldStateComponent>());
            using var states = query.ToComponentDataArray<NpcWorldStateComponent>(Allocator.Temp);
            if (states.Length == 0) return 0;
            int sum = 0;
            foreach (var s in states) sum += s.LocalTension;
            return (byte)(sum / states.Length);
        }

        // EndTurn clears HasReactedThisTurn via EntityManager
        private void ClearReactedFlags()
        {
            using var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NpcPersonalityComponent>(),
                ComponentType.ReadWrite<NpcWorldStateComponent>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var state = _entityManager.GetComponentData<NpcWorldStateComponent>(entity);
                state.HasReactedThisTurn = false;
                // Cool down local tension slightly each turn.
                state.LocalTension = (byte)Mathf.Max(0, state.LocalTension - 5);
                _entityManager.SetComponentData(entity, state);
            }
        }
    }
}
