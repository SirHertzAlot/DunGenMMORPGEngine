using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DunGen.Core;
using DunGen.Events;
using DunGen.Gameplay;
using NUnit.Framework;

namespace DunGen.Tests.Editor
{
    public sealed class GameSessionMvpEditModeTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus.Instance.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Instance.Clear();
        }

        [Test]
        public void QueuePlayerMove_RejectsInvalidVector()
        {
            var session = new GameSession(12345);
            session.StartGame();

            var result = session.QueuePlayerMove(0, 0, session.TurnCount);

            Assert.That(result.Status, Is.EqualTo(GameSession.PlayerCommandStatus.Invalid));
            Assert.That(result.IsAccepted, Is.False);
        }

        [Test]
        public void QueuePlayerMove_RejectsStaleTurn()
        {
            var session = new GameSession(12345);
            session.StartGame();

            var staleTurn = session.TurnCount + 1;
            var result = session.QueuePlayerMove(1, 0, staleTurn);

            Assert.That(result.Status, Is.EqualTo(GameSession.PlayerCommandStatus.Stale));
            Assert.That(result.IsAccepted, Is.False);
        }

        [Test]
        public void QueuePlayerMove_RejectsDuplicateForCurrentTurn()
        {
            var session = new GameSession(12345);
            session.StartGame();

            var first = session.QueuePlayerMove(1, 0, session.TurnCount);
            var second = session.QueuePlayerMove(1, 0, session.TurnCount);

            Assert.That(first.IsAccepted, Is.True);
            Assert.That(second.Status, Is.EqualTo(GameSession.PlayerCommandStatus.Duplicate));
        }

        [Test]
        public void QueuePlayerMove_RejectsBlockedTile()
        {
            var session = new GameSession(12345);
            session.StartGame();

            for (int i = 0; i < 60; i++)
            {
                var move = session.QueuePlayerMove(1, 0, session.TurnCount);
                if (!move.IsAccepted)
                    break;

                session.ExecuteTurn();
            }

            var blocked = session.QueuePlayerMove(1, 0, session.TurnCount);
            Assert.That(blocked.Status, Is.EqualTo(GameSession.PlayerCommandStatus.Blocked));
        }

        [Test]
        public void ScriptedCommandSequence_IsDeterministicForSameSeed()
        {
            static GameState RunScriptedSequence(int seed)
            {
                var session = new GameSession(seed);
                session.StartGame();

                session.QueuePlayerMove(1, 0, session.TurnCount);
                session.ExecuteTurn();

                session.QueuePlayerMove(0, 1, session.TurnCount);
                session.ExecuteTurn();

                session.QueuePlayerAttack();
                session.ExecuteTurn();

                session.QueuePlayerAttack();
                session.ExecuteTurn();

                return session.GetGameState();
            }

            var stateA = RunScriptedSequence(12345);
            var stateB = RunScriptedSequence(12345);

            Assert.That(stateA.PlayerX, Is.EqualTo(stateB.PlayerX));
            Assert.That(stateA.PlayerY, Is.EqualTo(stateB.PlayerY));
            Assert.That(stateA.PlayerHealth, Is.EqualTo(stateB.PlayerHealth));
            Assert.That(stateA.PlayerXP, Is.EqualTo(stateB.PlayerXP));
            Assert.That(stateA.PlayerGold, Is.EqualTo(stateB.PlayerGold));
            Assert.That(stateA.LivingEnemies, Is.EqualTo(stateB.LivingEnemies));
            Assert.That(stateA.TurnCount, Is.EqualTo(stateB.TurnCount));
            Assert.That(stateA.SessionSeed, Is.EqualTo(stateB.SessionSeed));
        }

        [Test]
        public void SimulationExportLogHash_IsDeterministicForSameSeedAndSteps()
        {
            static string RunAndHash(ulong seed, int steps)
            {
                var simulation = new DunGen.Core.Simulation();
                simulation.Initialize(seed);

                for (int i = 0; i < steps; i++)
                {
                    simulation.SimulationStep(1f / 60f);
                }

                var payload = simulation.ExportLog();
                var bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
                byte[] hash;
                using (var sha = SHA256.Create())
                {
                    hash = sha.ComputeHash(bytes);
                }

                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }

            var hashA = RunAndHash(42UL, 12);
            var hashB = RunAndHash(42UL, 12);

            Assert.That(hashA, Is.EqualTo(hashB));
        }

        [Test]
        public void ReplayExport_WritesLogToDisk_WithDeterministicHashSuffix()
        {
            var simulation = new DunGen.Core.Simulation();
            simulation.Initialize(77UL);
            simulation.SimulationStep(1f / 60f);

            var payload = simulation.ExportLog();
            var bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            byte[] hashBytes;
            using (var sha = SHA256.Create())
            {
                hashBytes = sha.ComputeHash(bytes);
            }
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            var tempFile = Path.Combine(Path.GetTempPath(), $"mvp_replay_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{hash}.json");
            File.WriteAllText(tempFile, payload ?? string.Empty);

            Assert.That(File.Exists(tempFile), Is.True);
            var roundTrip = File.ReadAllText(tempFile);
            Assert.That(roundTrip, Is.EqualTo(payload));
            Assert.That(Path.GetFileName(tempFile).Contains(hash), Is.True);

            File.Delete(tempFile);
        }
    }
}
