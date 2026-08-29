using DunGen.ECS.Combat;
using DunGen.ECS.Core;
using DunGen.ECS.Exploration;
using DunGen.ECS.Systems;
using DunGen.ECS.Systems.Combat;
using DunGen.Events;
using DunGen.Events.Combat;
using DunGen.Events.World;
using DunGen.Systems;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace DunGen.Tests.Editor
{
    public sealed class SystemsIntegrationEditModeTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus.Instance.Clear();
            EntityIndexCache.Instance.Clear();
            DirectEntityCache.Instance.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Instance.Clear();
            EntityIndexCache.Instance.Clear();
            DirectEntityCache.Instance.Clear();
        }

        [Test]
        public void MovementSystem_ClampsPositionAndMovementBudget()
        {
            using var world = new World("MovementSystemTestWorld");
            var em = world.EntityManager;
            var system = world.GetOrCreateSystemManaged<MovementSystem>();

            var e = em.CreateEntity(typeof(PositionComponent), typeof(MovementComponent));
            em.SetComponentData(e, new PositionComponent { X = -10, Y = 999, DungeonLevel = 1 });
            em.SetComponentData(e, new MovementComponent { MovementSpeed = -5, TilesMovedThisTurn = 1234 });

            system.Update();

            var pos = em.GetComponentData<PositionComponent>(e);
            var movement = em.GetComponentData<MovementComponent>(e);

            Assert.That(pos.X, Is.EqualTo(1));
            Assert.That(pos.Y, Is.EqualTo(22));
            Assert.That(movement.MovementSpeed, Is.EqualTo(0));
            Assert.That(movement.TilesMovedThisTurn, Is.EqualTo(0));
        }

        [Test]
        public void ExperienceSystem_LevelsUpAndPublishesEvent()
        {
            using var world = new World("ExperienceSystemTestWorld");
            var em = world.EntityManager;
            var system = world.GetOrCreateSystemManaged<ExperienceSystem>();

            LevelUpEventData levelUp = default;
            var levelUpHit = false;
            EventBus.Instance.Subscribe<LevelUpEventData>(evt =>
            {
                levelUp = evt;
                levelUpHit = true;
            });

            var e = em.CreateEntity(typeof(ExperienceComponent), typeof(CombatComponent));
            em.SetComponentData(e, new ExperienceComponent
            {
                CurrentXP = 90,
                XPToNextLevel = 100,
                Level = 1,
            });
            em.SetComponentData(e, new CombatComponent
            {
                CurrentHealth = 10,
                MaxHealth = 10,
                IsInCombat = true,
                CombatSessionId = e.Index,
            });

            system.Update();

            var exp = em.GetComponentData<ExperienceComponent>(e);
            Assert.That(levelUpHit, Is.True);
            Assert.That(levelUp.PreviousLevel, Is.EqualTo(1));
            Assert.That(levelUp.NewLevel, Is.EqualTo(2));
            Assert.That(exp.Level, Is.EqualTo(2));
            Assert.That(exp.CurrentXP, Is.EqualTo(40));
            Assert.That(exp.XPToNextLevel, Is.EqualTo(200));
        }

        [Test]
        public void CombatSystem_InitializationPhase_PublishesCombatStarted()
        {
            using var world = new World("CombatInitSystemTestWorld");
            var em = world.EntityManager;
            var system = world.GetOrCreateSystemManaged<CombatSystem>();

            CombatStartedEventData started = default;
            var startedHit = false;
            EventBus.Instance.Subscribe<CombatStartedEventData>(evt =>
            {
                started = evt;
                startedHit = true;
            });

            var actorA = em.CreateEntity(typeof(CombatComponent), typeof(InitiativeComponent), typeof(CombatRoundComponent));
            em.SetComponentData(actorA, new CombatComponent
            {
                CurrentHealth = 20,
                MaxHealth = 20,
                IsInCombat = true,
                CombatSessionId = 777,
                CombatSeed = 123,
            });
            em.SetComponentData(actorA, new InitiativeComponent { InitiativeScore = 15 });
            em.SetComponentData(actorA, new CombatRoundComponent
            {
                CombatPhase = 0,
                TotalParticipants = 2,
                RoundNumber = 0,
                CurrentTurnIndex = 0,
            });

            var actorB = em.CreateEntity(typeof(CombatComponent), typeof(InitiativeComponent));
            em.SetComponentData(actorB, new CombatComponent
            {
                CurrentHealth = 20,
                MaxHealth = 20,
                IsInCombat = true,
                CombatSessionId = 777,
                CombatSeed = 123,
            });
            em.SetComponentData(actorB, new InitiativeComponent { InitiativeScore = 10 });

            system.Update();

            var roundAfter = em.GetComponentData<CombatRoundComponent>(actorA);
            Assert.That(startedHit, Is.True);
            Assert.That(started.CombatSessionId, Is.EqualTo(777));
            Assert.That(roundAfter.CombatPhase, Is.EqualTo(1));
            Assert.That(roundAfter.TotalParticipants, Is.EqualTo(2));
            Assert.That(roundAfter.RoundNumber, Is.EqualTo(1));
        }

        [Test]
        public void CombatSystem_ActionPhase_AdvancesTurnAndPublishesRoundEnded()
        {
            using var world = new World("CombatTurnSystemTestWorld");
            var em = world.EntityManager;
            var system = world.GetOrCreateSystemManaged<CombatSystem>();

            var roundEndedHit = false;
            EventBus.Instance.Subscribe<RoundEndedEventData>(_ => roundEndedHit = true);

            var e = em.CreateEntity(typeof(CombatComponent), typeof(CombatRoundComponent));
            em.SetComponentData(e, new CombatComponent
            {
                CurrentHealth = 10,
                MaxHealth = 10,
                IsInCombat = true,
                CombatSessionId = 999,
            });
            em.SetComponentData(e, new CombatRoundComponent
            {
                CombatPhase = 1,
                TotalParticipants = 1,
                RoundNumber = 1,
                CurrentTurnIndex = 0,
            });

            system.Update();

            var after = em.GetComponentData<CombatRoundComponent>(e);
            Assert.That(roundEndedHit, Is.True);
            Assert.That(after.RoundNumber, Is.EqualTo(2));
            Assert.That(after.CurrentTurnIndex, Is.EqualTo(0));
        }

        [Test]
        public void WorldReactionEngine_CombatStarted_ProducesNpcReactionEvent()
        {
            using var world = new World("WorldReactionEngineTestWorld");
            var em = world.EntityManager;
            var bus = EventBus.Instance;

            using var engine = new WorldReactionEngine(bus, em);

            NpcReactionEventData reaction = default;
            var reactionHit = false;
            bus.Subscribe<NpcReactionEventData>(evt =>
            {
                reaction = evt;
                reactionHit = true;
            });

            var npc = em.CreateEntity(typeof(NpcPersonalityComponent), typeof(NpcWorldStateComponent), typeof(PositionComponent));
            em.SetComponentData(npc, new NpcPersonalityComponent
            {
                Aggression = 90,
                Cowardice = 5,
                Curiosity = 10,
                Greed = 10,
                Loyalty = 20,
                Vengefulness = 30,
                ArchetypeName = new FixedString32Bytes("test-goblin"),
            });
            em.SetComponentData(npc, new NpcWorldStateComponent
            {
                HasReactedThisTurn = false,
                LocalTension = 0,
                LastDamagedByEntityIndex = 0,
                FleeingTurns = 0,
            });
            em.SetComponentData(npc, new PositionComponent { X = 10, Y = 10, DungeonLevel = 1 });

            bus.Publish(new CombatStartedEventData
            {
                EventId = bus.GetNextEventId(),
                FrameNumber = 1,
                Timestamp = 0.016f,
                CombatSessionId = 555,
                ParticipantEntityIds = new[] { npc.Index },
                InitiativeOrder = new[] { npc.Index },
                CombatPositionX = 10,
                CombatPositionY = 10,
                DungeonLevel = 1,
            });

            Assert.That(reactionHit, Is.True);
            Assert.That(reaction.ReactingEntityIndex, Is.EqualTo(npc.Index));
            Assert.That(reaction.Reaction, Is.Not.EqualTo(NpcReactionType.None));
        }
    }
}
