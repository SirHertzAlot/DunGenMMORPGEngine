using System;
using DunGen.Gameplay;
using UnityEngine;

namespace DunGen.Testing
{
    [Serializable]
    [CreateAssetMenu(fileName = "GameplayAutomationScript", menuName = "DunGen/Testing/Gameplay Automation Script")]
    public sealed class GameplayAutomationScript : ScriptableObject
    {
        public int Seed = 12345;
        public int MaxTurns = 12;
        public bool StopWhenGameOver = true;
        public AuthoritativeWorldBlueprint AuthoritativeWorld;
    }

    public sealed class GameplayAutomationResult
    {
        public int ExecutedTurns { get; set; }
        public bool IsGameOver { get; set; }
        public string GameOverReason { get; set; } = string.Empty;
        public GameState FinalState { get; set; }
    }

    public sealed class GameplayAutomationRunner
    {
        public GameplayAutomationResult Run(
            GameplayAutomationScript script,
            Action<GameSession> configureSession = null,
            Func<GameplayAutomationResult, bool> stopPredicate = null)
        {
            if (script == null)
                throw new ArgumentNullException(nameof(script));

            var maxTurns = Math.Max(1, script.MaxTurns);
            var session = new GameSession(script.Seed);
            session.StartGame(script.AuthoritativeWorld);
            configureSession?.Invoke(session);

            var result = new GameplayAutomationResult
            {
                FinalState = session.GetGameState(),
                IsGameOver = session.IsGameOver,
                GameOverReason = session.GameOverReason ?? string.Empty,
            };

            for (int turn = 0; turn < maxTurns; turn++)
            {
                if (script.StopWhenGameOver && session.IsGameOver)
                    break;

                session.QueuePlayerAttack();
                session.ExecuteTurn();
                result.ExecutedTurns++;
                result.FinalState = session.GetGameState();
                result.IsGameOver = session.IsGameOver;
                result.GameOverReason = session.GameOverReason ?? string.Empty;

                if (stopPredicate != null && stopPredicate(result))
                    break;
            }

            return result;
        }
    }

    public sealed class GameplayAutomationBootstrap : MonoBehaviour
    {
        [SerializeField] private GameplayAutomationScript automationScript;
        [SerializeField] private bool runOnStart = true;

        public GameplayAutomationResult LastResult { get; private set; }

        private void Start()
        {
            if (!runOnStart || automationScript == null)
                return;

            LastResult = new GameplayAutomationRunner().Run(automationScript);
            Debug.Log($"[DunGen.Testing] Automation finished after {LastResult.ExecutedTurns} turns. GameOver={LastResult.IsGameOver} Reason='{LastResult.GameOverReason}'.");
        }

        public void Configure(GameplayAutomationScript script, bool autoRun = true)
        {
            automationScript = script;
            runOnStart = autoRun;
        }
    }
}