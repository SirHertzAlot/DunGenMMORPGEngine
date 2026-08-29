using DunGen.ECS.Combat;
using DunGen.Events;
using DunGen.Events.Combat;
using NUnit.Framework;
using System.Collections.Generic;

namespace DunGen.Tests.Combat
{
    /// <summary>
    /// Tests for Week 4 Advanced Combat System.
    /// Validates action queue, turn management, and action resolution.
    /// </summary>
    public class AdvancedCombatSystemTests
    {
        [SetUp]
        public void Setup()
        {
            EventBus.Instance.Clear();
        }

        #region Action Queue Tests

        [Test]
        public void ActionQueue_CanQueueAction()
        {
            // Arrange
            var queue = new ActionQueueComponent();
            var action = new CombatAction
            {
                Type = ActionType.Attack,
                TargetEntityId = 2,
                Name = "Sword Attack",
                ActionCost = 1,
                ManaCost = 0
            };

            // Act
            queue.QueueAction(action);

            // Assert
            Assert.AreEqual(1, queue.QueuedActionCount);
        }

        [Test]
        public void ActionQueue_CanQueueMultipleActions()
        {
            // Arrange
            var queue = new ActionQueueComponent();

            // Act
            for (int i = 0; i < 3; i++)
            {
                queue.QueueAction(new CombatAction
                {
                    Type = ActionType.Attack,
                    Name = $"Attack {i}",
                    ActionCost = 1
                });
            }

            // Assert
            Assert.AreEqual(3, queue.QueuedActionCount);
        }

        [Test]
        public void ActionQueue_RespectsMaxQueueSize()
        {
            // Arrange
            var queue = new ActionQueueComponent();

            // Act - Queue maximum actions
            for (int i = 0; i < 10; i++)  // Try to queue more than MAX_QUEUED_ACTIONS
            {
                queue.QueueAction(new CombatAction
                {
                    Type = ActionType.Attack,
                    ActionCost = 1
                });
            }

            // Assert - Should only have MAX_QUEUED_ACTIONS
            Assert.AreEqual(ActionQueueComponent.MAX_QUEUED_ACTIONS, queue.QueuedActionCount);
        }

        [Test]
        public void ActionQueue_GetNextAction_InOrder()
        {
            // Arrange
            var queue = new ActionQueueComponent();
            var action1 = new CombatAction { Name = "Attack", ActionCost = 1 };
            var action2 = new CombatAction { Name = "Dodge", ActionCost = 0 };
            
            queue.QueueAction(action1);
            queue.QueueAction(action2);

            // Act
            var next1 = queue.GetNextAction();
            queue.AdvanceAction();
            var next2 = queue.GetNextAction();

            // Assert
            Assert.AreEqual("Attack", next1.Name);
            Assert.AreEqual("Dodge", next2.Name);
        }

        [Test]
        public void ActionQueue_ClearQueue_ResetsState()
        {
            // Arrange
            var queue = new ActionQueueComponent();
            queue.QueueAction(new CombatAction { ActionCost = 1 });
            queue.QueueAction(new CombatAction { ActionCost = 1 });
            Assert.AreEqual(2, queue.QueuedActionCount);

            // Act
            queue.ClearQueue();

            // Assert
            Assert.AreEqual(0, queue.QueuedActionCount);
            Assert.AreEqual(0, queue.ExecutedActionCount);
        }

        #endregion

        #region Action Cost Tests

        [Test]
        public void ActionCost_CanAfford_Action()
        {
            // Arrange
            var costs = new ActionCostComponent
            {
                ActionsRemaining = 1,
                BonusActionsRemaining = 0,
                ReactionsRemaining = 1
            };

            // Act
            bool canAfford = costs.CanAfford(1);  // Cost 1 = action

            // Assert
            Assert.IsTrue(canAfford);
        }

        [Test]
        public void ActionCost_CannotAfford_NoResources()
        {
            // Arrange
            var costs = new ActionCostComponent
            {
                ActionsRemaining = 0,
                BonusActionsRemaining = 0,
                ReactionsRemaining = 0
            };

            // Act
            bool canAfford = costs.CanAfford(1);

            // Assert
            Assert.IsFalse(canAfford);
        }

        [Test]
        public void ActionCost_SpendAction_ReducesCount()
        {
            // Arrange
            var costs = new ActionCostComponent { ActionsRemaining = 2 };

            // Act
            costs.SpendAction(1);

            // Assert
            Assert.AreEqual(1, costs.ActionsRemaining);
        }

        [Test]
        public void ActionCost_ResetForNewTurn()
        {
            // Arrange
            var costs = new ActionCostComponent
            {
                ActionsRemaining = 0,
                BonusActionsRemaining = 0,
                ReactionsRemaining = 0
            };

            // Act
            costs.ResetForNewTurn();

            // Assert
            Assert.AreEqual(1, costs.ActionsRemaining);
            Assert.AreEqual(0, costs.BonusActionsRemaining);
            Assert.AreEqual(1, costs.ReactionsRemaining);
            Assert.AreEqual(30, costs.MovementRemaining);
        }

        #endregion

        #region Turn Queue Tests

        [Test]
        public void TurnQueue_AddCombatant()
        {
            // Arrange
            var queue = new TurnQueueComponent();

            // Act
            queue.AddCombatant(1);
            queue.AddCombatant(2);
            queue.AddCombatant(3);

            // Assert
            Assert.AreEqual(3, queue.TotalCombatants);
            Assert.AreEqual(1, queue.GetCurrentActor());  // First actor
        }

        [Test]
        public void TurnQueue_AdvanceTurn()
        {
            // Arrange
            var queue = new TurnQueueComponent();
            queue.AddCombatant(1);
            queue.AddCombatant(2);
            queue.AddCombatant(3);

            // Act
            queue.AdvanceTurn();

            // Assert
            Assert.AreEqual(2, queue.GetCurrentActor());  // Second actor
        }

        [Test]
        public void TurnQueue_IsRoundComplete()
        {
            // Arrange
            var queue = new TurnQueueComponent();
            queue.AddCombatant(1);
            queue.AddCombatant(2);

            // Act & Assert
            Assert.IsFalse(queue.IsRoundComplete());
            
            queue.AdvanceTurn();
            Assert.IsFalse(queue.IsRoundComplete());
            
            queue.AdvanceTurn();
            Assert.IsTrue(queue.IsRoundComplete());
        }

        [Test]
        public void TurnQueue_ResetForNewRound()
        {
            // Arrange
            var queue = new TurnQueueComponent();
            queue.AddCombatant(1);
            queue.AddCombatant(2);
            queue.AdvanceTurn();
            queue.AdvanceTurn();
            Assert.IsTrue(queue.IsRoundComplete());

            // Act
            queue.ResetForNewRound();

            // Assert
            Assert.AreEqual(0, queue.CurrentTurnIndex);
            Assert.AreEqual(1, queue.GetCurrentActor());
            Assert.IsFalse(queue.IsRoundComplete());
        }

        #endregion

        #region Condition Tests

        [Test]
        public void Condition_ApplyCondition()
        {
            // Arrange
            var conditions = new ConditionComponent();

            // Act
            conditions.ApplyCondition("Prone");

            // Assert
            Assert.IsTrue(conditions.IsProne);
            Assert.AreEqual(1, conditions.ActiveConditionCount);
        }

        [Test]
        public void Condition_HasCondition()
        {
            // Arrange
            var conditions = new ConditionComponent();
            conditions.ApplyCondition("Stunned");

            // Act
            bool has = conditions.HasCondition("Stunned");

            // Assert
            Assert.IsTrue(has);
            Assert.IsFalse(conditions.HasCondition("Prone"));
        }

        [Test]
        public void Condition_RemoveCondition()
        {
            // Arrange
            var conditions = new ConditionComponent();
            conditions.ApplyCondition("Charmed");
            Assert.IsTrue(conditions.IsCharmed);

            // Act
            conditions.RemoveCondition("Charmed");

            // Assert
            Assert.IsFalse(conditions.IsCharmed);
            Assert.AreEqual(0, conditions.ActiveConditionCount);
        }

        [Test]
        public void Condition_MultipleConditions()
        {
            // Arrange
            var conditions = new ConditionComponent();

            // Act
            conditions.ApplyCondition("Prone");
            conditions.ApplyCondition("Stunned");
            conditions.ApplyCondition("Restrained");

            // Assert
            Assert.AreEqual(3, conditions.ActiveConditionCount);
            Assert.IsTrue(conditions.IsProne);
            Assert.IsTrue(conditions.IsStunned);
            Assert.IsTrue(conditions.IsRestrained);
        }

        #endregion

        #region Combat Events Tests

        [Test]
        public void ActionEventData_CanBeCreated()
        {
            // Arrange & Act
            var evt = new ActionQueuedEventData
            {
                EventId = 1,
                FrameNumber = 10,
                Timestamp = 0.167f,
                ActorEntityId = 1,
                ActionType = 0,  // Attack
                TargetEntityId = 2,
                ActionName = "Sword Slash",
                ActionCost = 1
            };

            // Assert
            Assert.AreEqual(1, evt.ActorEntityId);
            Assert.AreEqual(2, evt.TargetEntityId);
        }

        [Test]
        public void ConditionEventData_CanBeCreated()
        {
            // Arrange & Act
            var evt = new ConditionAppliedEventData
            {
                EventId = 1,
                FrameNumber = 5,
                Timestamp = 0.083f,
                TargetEntityId = 3,
                ConditionName = "Prone",
                DurationFrames = 60,
                SourceEntityId = 1
            };

            // Assert
            Assert.AreEqual("Prone", evt.ConditionName);
            Assert.AreEqual(60, evt.DurationFrames);
        }

        [Test]
        public void RoundTransitionEventData_CanBeCreated()
        {
            // Arrange & Act
            var evt = new RoundTransitionEventData
            {
                EventId = 1,
                FrameNumber = 100,
                Timestamp = 1.667f,
                CompletedRoundNumber = 1,
                NextRoundNumber = 2,
                TotalDamageThisRound = 25,
                ActionsExecuted = 6
            };

            // Assert
            Assert.AreEqual(1, evt.CompletedRoundNumber);
            Assert.AreEqual(2, evt.NextRoundNumber);
            Assert.AreEqual(25, evt.TotalDamageThisRound);
        }

        #endregion

        #region Integration Tests

        [Test]
        public void CompleteActionFlow_QueueExecuteEvent()
        {
            // Arrange
            var bus = EventBus.Instance;
            int actionResolvedCount = 0;

            bus.Subscribe<ActionResolvedEventData>(_ => actionResolvedCount++);

            var queue = new ActionQueueComponent();
            var costs = new ActionCostComponent { ActionsRemaining = 1 };
            
            queue.QueueAction(new CombatAction
            {
                Type = ActionType.Attack,
                TargetEntityId = 2,
                Name = "Attack",
                ActionCost = 1
            });

            // Assert queued
            Assert.AreEqual(1, queue.QueuedActionCount);
            Assert.AreEqual(1, costs.ActionsRemaining);

            // Simulate action execution
            if (costs.CanAfford(1))
            {
                costs.SpendAction(1);
                bus.Publish(new ActionResolvedEventData
                {
                    EventId = bus.GetNextEventId(),
                    FrameNumber = 10,
                    Timestamp = 0.167f,
                    ActorEntityId = 1,
                    ActionType = 0,
                    TargetEntityId = 2,
                    IsSuccessful = true,
                    EffectValue = 8
                });
            }

            // Assert
            Assert.AreEqual(0, costs.ActionsRemaining);
            Assert.AreEqual(1, actionResolvedCount);
        }

        [Test]
        public void CombatRound_CompleteSequence()
        {
            // Arrange
            var round = new CombatRoundComponent
            {
                RoundNumber = 1,
                TotalParticipants = 3,
                CombatPhase = 1  // In combat
            };

            var queue = new TurnQueueComponent();
            queue.AddCombatant(1);
            queue.AddCombatant(2);
            queue.AddCombatant(3);

            // Act - Simulate round
            int turnsTaken = 0;
            while (!queue.IsRoundComplete())
            {
                var actor = queue.GetCurrentActor();
                Assert.IsTrue(actor > 0);
                turnsTaken++;
                queue.AdvanceTurn();
            }

            // Assert
            Assert.AreEqual(3, turnsTaken);
            Assert.IsTrue(queue.IsRoundComplete());
            queue.ResetForNewRound();  // Mirrors RoundTransitionSystem behaviour
            Assert.AreEqual(0, queue.CurrentTurnIndex);  // Reset to start
        }

        #endregion
    }
}
