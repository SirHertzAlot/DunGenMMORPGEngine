using System;
using DunGen.Client;
using DunGen.Events;
using DunGen.Gameplay;
using NUnit.Framework;

namespace DunGen.Tests.Client
{
    public class ClientSessionTests
    {
        [SetUp]
        public void Setup()
        {
            EventBus.Instance.Clear();
        }

        [Test]
        public void Submit_WaitCommand_AcceptsAndAdvancesTurn()
        {
            var session = CreateStartedSession();
            var client = new LocalGameSessionClient(session);

            var result = client.Submit(Envelope("cmd-1", 1, ClientCommand.Wait()));

            Assert.IsTrue(result.Accepted);
            Assert.IsFalse(result.Duplicate);
            Assert.AreEqual(1, result.Snapshot.TurnCount);
        }

        [Test]
        public void Submit_InvalidMove_RejectsWithoutAdvancingTurn()
        {
            var session = CreateStartedSession();
            var client = new LocalGameSessionClient(session);

            var result = client.Submit(Envelope("cmd-1", 1, ClientCommand.Move(1, 1)));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.Snapshot.TurnCount);
            StringAssert.Contains("one cardinal tile", result.Message);
        }

        [Test]
        public void Submit_ExpiredCommand_RejectsWithoutAdvancingTurn()
        {
            var session = CreateStartedSession();
            var client = new LocalGameSessionClient(session);
            var envelope = Envelope("cmd-1", 1, ClientCommand.Wait());
            envelope.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);

            var result = client.Submit(envelope);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.Snapshot.TurnCount);
            StringAssert.Contains("stale", result.Message);
        }

        [Test]
        public void Submit_DuplicateCommand_AcksWithoutAdvancingAgain()
        {
            var session = CreateStartedSession();
            var client = new LocalGameSessionClient(session);
            var envelope = Envelope("cmd-1", 1, ClientCommand.Wait());

            var first = client.Submit(envelope);
            var second = client.Submit(envelope);

            Assert.IsTrue(first.Accepted);
            Assert.IsTrue(second.Accepted);
            Assert.IsTrue(second.Duplicate);
            Assert.AreEqual(1, second.Snapshot.TurnCount);
        }

        [Test]
        public void Submit_StaleSequence_RejectsWithoutAdvancingAgain()
        {
            var session = CreateStartedSession();
            var client = new LocalGameSessionClient(session);

            client.Submit(Envelope("cmd-1", 1, ClientCommand.Wait()));
            var result = client.Submit(Envelope("cmd-2", 1, ClientCommand.Wait()));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(1, result.Snapshot.TurnCount);
            StringAssert.Contains("stale", result.Message);
        }

        static GameSession CreateStartedSession()
        {
            var session = new GameSession(12345);
            session.StartGame();
            return session;
        }

        static ClientCommandEnvelope Envelope(string commandId, int sequence, ClientCommand command)
        {
            return new ClientCommandEnvelope
            {
                ContractVersion = ClientCommandEnvelope.CurrentContractVersion,
                CommandId = commandId,
                Sequence = sequence,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(30),
                Command = command
            };
        }
    }
}
