using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Authoritative.Domain;
using Authoritative.Multiplayer;
using Authoritative.Services;

#if UNITY_5_3_OR_NEWER
using Assert = NUnit.Framework.Assert;
using FactAttribute = NUnit.Framework.TestAttribute;
#else
using Assert = Xunit.Assert;
using FactAttribute = Xunit.FactAttribute;
#endif

#if !UNITY_5_3_OR_NEWER
namespace Authoritative.Tests
{
    /// <summary>
    /// Contract tests for the server-authoritative simulator. The mirror tests
    /// re-derive the expected outcome from the same DeterministicRng streams the
    /// simulator must consume, pinning the Unity-compatible seeding and draw
    /// order: session rng for initiative, then two independent streams both
    /// seeded with (uint)(seed + turn + combatSessionId) for attack/damage.
    /// </summary>
    public class AuthoritativeSimulatorTests
    {
        // Room covers x=2..17, y=4..15 → center (10,10).
        private static GeneratedWorldArtifact SingleRoomWorld(int seed, params (int X, int Y, int Level)[] enemies)
        {
            return new GeneratedWorldArtifact
            {
                Seed = seed,
                Width = 30,
                Height = 24,
                DungeonLevel = 1,
                Rooms = new List<WorldRoom> { new WorldRoom { Id = 1, X = 2, Y = 4, Width = 16, Height = 12 } },
                Enemies = enemies.Select((e, i) => new WorldEnemy
                {
                    Id = i + 1,
                    Archetype = "goblin",
                    X = e.X,
                    Y = e.Y,
                    Level = e.Level
                }).ToList(),
                Loot = new List<WorldLoot>(),
                TerrainMesh = new GeneratedTerrainMesh()
            };
        }

        private static T RunLocked<T>(AuthoritativeSessionSimulator sim, Func<AuthoritativeSessionSimulator, T> action)
        {
            lock (sim.SyncRoot)
            {
                return action(sim);
            }
        }

        private static string Canonical(AuthoritativeGameStateDto state)
        {
            return JsonSerializer.Serialize(state);
        }

        // ── Move queueing contract ───────────────────────────────────────────

        [FactAttribute]
        public void QueueMove_Accepted_DoesNotAdvanceTurn()
        {
            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(42, (20, 10, 1)));
            var outcome = RunLocked(sim, s => s.QueueMove(1, 0, 0));
            var state = RunLocked(sim, s => s.BuildState());

            Assert.Equal(AuthoritativeActionStatus.Accepted, outcome.Status);
            Assert.True(outcome.Accepted);
            Assert.Equal(0, state.Turn);
            Assert.Equal(10, state.Player.X); // queued, not yet applied
        }

        [FactAttribute]
        public void QueueMove_SameMoveQueuedTwice_IsDuplicate()
        {
            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(42, (20, 10, 1)));
            var first = RunLocked(sim, s => s.QueueMove(1, 0, 0));
            var second = RunLocked(sim, s => s.QueueMove(1, 0, 0));

            Assert.Equal(AuthoritativeActionStatus.Accepted, first.Status);
            Assert.Equal(AuthoritativeActionStatus.Duplicate, second.Status);
            Assert.False(second.Accepted);
        }

        [FactAttribute]
        public void QueueMove_DifferentMovesSameTurn_AreAccepted()
        {
            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(42, (20, 10, 1)));
            Assert.Equal(AuthoritativeActionStatus.Accepted, RunLocked(sim, s => s.QueueMove(1, 0, 0)).Status);
            Assert.Equal(AuthoritativeActionStatus.Accepted, RunLocked(sim, s => s.QueueMove(0, 1, 0)).Status);
        }

        [FactAttribute]
        public void QueueMove_NonSingleStepDelta_IsInvalid()
        {
            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(42, (20, 10, 1)));
            Assert.Equal(AuthoritativeActionStatus.Invalid, RunLocked(sim, s => s.QueueMove(2, 0, 0)).Status);
            Assert.Equal(AuthoritativeActionStatus.Invalid, RunLocked(sim, s => s.QueueMove(1, 1, 0)).Status);
        }

        [FactAttribute]
        public void QueueMove_WrongExpectedTurn_IsStale()
        {
            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(42, (20, 10, 1)));
            Assert.Equal(AuthoritativeActionStatus.Stale, RunLocked(sim, s => s.QueueMove(1, 0, 1)).Status);
        }

        [FactAttribute]
        public void QueueMove_IntoUnwalkableTile_IsBlocked()
        {
            // Single-tile-wide room at x=5 so a single step left is a wall.
            var world = SingleRoomWorld(42);
            world.Rooms = new List<WorldRoom> { new WorldRoom { Id = 1, X = 5, Y = 4, Width = 1, Height = 12 } };
            var sim = new AuthoritativeSessionSimulator(world);

            var west = RunLocked(sim, s => s.QueueMove(-1, 0, 0));
            Assert.Equal(AuthoritativeActionStatus.Blocked, west.Status);

            var east = RunLocked(sim, s => s.QueueMove(1, 0, 0));
            Assert.Equal(AuthoritativeActionStatus.Blocked, east.Status);
        }

        [FactAttribute]
        public void QueueMove_IntoLivingEnemyTile_IsOccupied()
        {
            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(42, (11, 10, 1)));
            var outcome = RunLocked(sim, s => s.QueueMove(1, 0, 0));

            Assert.Equal(AuthoritativeActionStatus.Occupied, outcome.Status);
            Assert.False(outcome.Accepted);
        }

        // ── Turn resolution contract ──────────────────────────────────────────

        [FactAttribute]
        public void ResolveTurn_AppliesQueuedMoveAndAdvancesTurn()
        {
            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(42, (20, 10, 1)));
            RunLocked(sim, s => s.QueueMove(1, 0, 0));

            var response = RunLocked(sim, s => s.ResolveTurn(0));
            Assert.True(response.Accepted);
            Assert.Equal(AuthoritativeActionStatus.Accepted, response.Status);
            Assert.Equal(1, response.Turn);
            Assert.Equal(11, response.State!.Player.X);

            // The queue was consumed, so re-queueing the same move works fresh.
            Assert.Equal(AuthoritativeActionStatus.Accepted, RunLocked(sim, s => s.QueueMove(1, 0, 1)).Status);
        }

        [FactAttribute]
        public void ResolveTurn_WrongExpectedTurn_IsStaleAndResolvesNothing()
        {
            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(42, (20, 10, 1)));
            var response = RunLocked(sim, s => s.ResolveTurn(1));

            Assert.Equal(AuthoritativeActionStatus.Stale, response.Status);
            Assert.False(response.Accepted);
            Assert.Equal(0, response.Turn);
        }

        [FactAttribute]
        public void Combat_EngagesMovingEnemyAndMirrorsRngExactly()
        {
            int seed = 1337;
            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(seed, (12, 10, 1)));
            var response = RunLocked(sim, s => s.ResolveTurn(0));

            // Enemy 1 level: HP 50, AC 10, attack modifier 3 (+1 Str / +2 prof).
            var mirror = MirrorSingleCombatTurn(seed);

            Assert.True(response.Accepted);
            Assert.Equal(1, response.State!.Turn);
            Assert.True(response.State.InCombat, "moving into range must start combat on the resolution turn");
            Assert.False(response.State.GameOver);
            Assert.Equal(mirror.EnimyHp, response.State.Enemies.Single().Health);
            Assert.Equal(mirror.PlayerHp, response.State.Player.Health);
        }

        /// <summary>
        /// Independent re-derivation of one combat turn (turn 1, combat session
        /// 100): initiative from the session rng, then one attack per combatant
        /// from two streams both seeded with (uint)(seed + turn + 100).
        /// </summary>
        private static (int PlayerHp, int EnimyHp) MirrorSingleCombatTurn(int seed)
        {
            int playerX = 10, playerY = 10, playerHp = 100;
            int enemyX = 12, enemyY = 10, enemyHp = 50;

            var sessionRng = new DeterministicRng((ulong)(uint)seed);

            // Turn 1 exploration: the enemy at (12,10) moves one tile toward the
            // player to (11,10), becoming adjacent → combat starts.
            enemyX += Math.Sign(playerX - enemyX);
            enemyY += Math.Sign(playerY - enemyY);

            int playerInit = sessionRng.RollD20() + 1; // Dex modifier 1
            int enemyInit = sessionRng.RollD20();      // Dex modifier 0
            bool playerFirst = playerInit >= enemyInit;

            uint combatSeed = (uint)(seed + 1 + 100);
            var attackRng = new DeterministicRng(combatSeed);
            var damageRng = new DeterministicRng(combatSeed);

            void ResolvePlayerAttack()
            {
                int d20 = attackRng.RollD20();
                bool crit = d20 == 20;
                bool fumble = d20 == 1;
                bool hit = crit || (!fumble && (d20 + 4) >= 10);
                if (!hit)
                    return;

                int d8 = damageRng.RollDice(8);
                int damage = Math.Max(1, (d8 + 4) * (crit ? 2 : 1));
                enemyHp = Math.Max(0, enemyHp - damage);
            }

            void ResolveEnemyAttack()
            {
                int d20 = attackRng.RollD20();
                bool crit = d20 == 20;
                bool fumble = d20 == 1;
                bool hit = crit || (!fumble && (d20 + 3) >= 12);
                if (!hit)
                    return;

                int d8 = damageRng.RollDice(8);
                int damage = Math.Max(1, (d8 + 3) * (crit ? 2 : 1));
                playerHp = Math.Max(0, playerHp - damage);
            }

            if (playerFirst)
            {
                ResolvePlayerAttack();
                if (enemyHp > 0)
                    ResolveEnemyAttack();
            }
            else
            {
                ResolveEnemyAttack();
                if (playerHp > 0)
                    ResolvePlayerAttack();
            }

            return (playerHp, enemyHp);
        }

        // ── Rewards, level-up, game-over ──────────────────────────────────────

        [FactAttribute]
        public void TwoEnemyWorld_ClearsWithDeterministicRewards()
        {
            // Two enemies adjacent to spawn; the world resolves to a player
            // victory for some seed. Find the first winning seed, then assert
            // the reward/level-up/game-over invariants on it deterministically.
            int seed = FindClearingSeed();
            Assert.True(seed > 0, "no seed produced a cleared world within the scan");

            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(seed, (11, 10, 1), (9, 10, 1)));
            var final = PlayToCompletion(sim)!.State!;

            Assert.Equal("Authoritative world cleared.", final.GameOverReason);
            Assert.True(final.GameOver);
            Assert.True(final.PlayerAlive, "player should survive a cleared world");
            Assert.Equal(100, final.Player.Gold);       // 2 × max(10,50) gold per no-loot enemy
            Assert.Equal(2, final.Player.Level);        // 2 × 50 XP → level 2
            Assert.Equal(0, final.Player.Experience);   // 100 XP consumed by level-up
            Assert.Equal(200, final.Player.XpToNextLevel);
            Assert.All(final.Enemies, e => Assert.True(e.IsDead));

            var timeline = RunLocked(sim, s => s.GetRecentEvents(512));
            Assert.Equal(2, timeline.Count(ev => ev.Type == "loot_granted"));
            Assert.Equal(1, timeline.Count(ev => ev.Type == "level_up"));
            Assert.Equal(1, timeline.Count(ev => ev.Type == "game_over"));
            Assert.Equal(2, timeline.Count(ev => ev.Type == "combat_started"));
        }

        [FactAttribute]
        public void SameCommandLog_TwoFreshSessions_ProducesIdenticalState()
        {
            var world = SingleRoomWorld(777, (11, 10, 1), (9, 10, 1));

            var simA = new AuthoritativeSessionSimulator(world);
            var simB = new AuthoritativeSessionSimulator(world);

            var finalA = PlayToCompletion(simA)!.State!;
            var finalB = PlayToCompletion(simB)!.State!;

            Assert.Equal(Canonical(finalA), Canonical(finalB));

            var timelineA = RunLocked(simA, s => s.GetRecentEvents(512));
            var timelineB = RunLocked(simB, s => s.GetRecentEvents(512));
            Assert.Equal(
                JsonSerializer.Serialize(timelineA.Select(ev => (ev.Type, ev.Message))),
                JsonSerializer.Serialize(timelineB.Select(ev => (ev.Type, ev.Message))));
        }

        [FactAttribute]
        public void ClosedSession_RejectsFurtherActions_AsSessionUnavailable()
        {
            int seed = FindClearingSeed();
            Assert.True(seed > 0, "no seed produced a cleared world within the scan");

            var sim = new AuthoritativeSessionSimulator(SingleRoomWorld(seed, (11, 10, 1), (9, 10, 1)));
            PlayToCompletion(sim);

            var attack = RunLocked(sim, s => s.ResolveTurn(s.TurnCount));
            var move = RunLocked(sim, s => s.QueueMove(1, 0, s.TurnCount));

            Assert.Equal(AuthoritativeActionStatus.SessionUnavailable, attack.Status);
            Assert.False(attack.Accepted);
            Assert.Equal(AuthoritativeActionStatus.SessionUnavailable, move.Status);
            Assert.False(move.Accepted);
        }

        // ── World factory determinism ─────────────────────────────────────────

        [FactAttribute]
        public void FallbackWorld_SameSeed_IsIdenticalDifferentSeedDiffers()
        {
            var first = AuthoritativeWorldFactory.BuildFallback(90210);
            var second = AuthoritativeWorldFactory.BuildFallback(90210);
            var other = AuthoritativeWorldFactory.BuildFallback(90211);

            Assert.Equal(JsonSerializer.Serialize(first.Rooms), JsonSerializer.Serialize(second.Rooms));
            Assert.Equal(JsonSerializer.Serialize(first.Enemies), JsonSerializer.Serialize(second.Enemies));
            Assert.Equal(JsonSerializer.Serialize(first.Loot), JsonSerializer.Serialize(second.Loot));

            Assert.NotEqual(JsonSerializer.Serialize(first.Rooms), JsonSerializer.Serialize(other.Rooms));
            Assert.NotEqual(first.Seed, other.Seed);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int FindClearingSeed()
        {
            for (int seed = 1; seed <= 4000; seed++)
            {
                var world = SingleRoomWorld(seed, (11, 10, 1), (9, 10, 1));
                var sim = new AuthoritativeSessionSimulator(world);
                var final = PlayToCompletion(sim);
                if (final != null && final.GameOver &&
                    final.GameOverReason == "Authoritative world cleared." &&
                    final.State!.Player.Gold == 100)
                {
                    return seed;
                }
            }

            return -1;
        }

        private static AuthoritativeActionResponse? PlayToCompletion(AuthoritativeSessionSimulator sim)
        {
            const int maxTurns = 40;
            for (int i = 0; i < maxTurns; i++)
            {
                int expectedTurn;
                AuthoritativeActionResponse response;
                lock (sim.SyncRoot)
                {
                    if (sim.IsGameOver)
                        return null;
                    expectedTurn = sim.TurnCount;
                    response = sim.ResolveTurn(expectedTurn);
                }

                if (response.GameOver)
                    return response;
            }

            return null;
        }
    }
}
#endif