using DunGen.Core;
using DunGen.Events;
using DunGen.Gameplay;
using UnityEngine;

namespace DunGen.Startup
{
    /// <summary>
    /// MonoBehaviour that initializes the simulation and game session on play.
    /// Attach to a GameObject in the scene and press Play to start a playable MVP game.
    /// </summary>
    public class SimulationStarter : MonoBehaviour
    {
        private Simulation _simulation;
        private GameSession _gameSession;
        public ulong SimulationSeed = 42;

        private void Start()
        {
            _simulation = new Simulation();
            _simulation.Initialize(SimulationSeed);
            
            Debug.Log($"✓ Simulation initialized with seed: {SimulationSeed}");
            Debug.Log($"✓ Event Bus ready");
            Debug.Log($"✓ Event Log started");
            
            // Initialize game session for MVP
            _gameSession = new GameSession((int)SimulationSeed);
            _gameSession.StartGame();
            
            Debug.Log($"✓ Game session started - Ready to play!");
        }

        private void Update()
        {
            if (_simulation != null && _simulation.IsRunning)
            {
                _simulation.SimulationStep(Time.deltaTime);
            }
            
            // Simple demo: execute game turn every 100 frames
            if (_gameSession != null && !_gameSession.IsGameOver && (Time.frameCount % 100 == 0))
            {
                _gameSession.ExecuteTurn();
            }
        }

        private void OnGUI()
        {
            if (_simulation == null)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 500, 400));
            
            // Simulation info
            GUILayout.Label("=== SIMULATION ===", GUI.skin.box);
            GUILayout.Label($"Status: {(_simulation.IsRunning ? "Running" : "Stopped")}", GUI.skin.box);
            GUILayout.Label($"Frame: {_simulation.GetFrameNumber()}", GUI.skin.box);
            GUILayout.Label($"Seed: {_simulation.GetSeed()}", GUI.skin.box);
            GUILayout.Label($"Events: {_simulation.GetEventLog().GetEvents().Count}", GUI.skin.box);
            
            // Game session info
            if (_gameSession != null)
            {
                GUILayout.Label("=== GAME SESSION ===", GUI.skin.box);
                var gameState = _gameSession.GetGameState();
                GUILayout.Label(gameState.ToString(), GUI.skin.box);
                
                if (GUILayout.Button("Execute Turn"))
                {
                    _gameSession.ExecuteTurn();
                }
            }
            
            // Simulation controls
            GUILayout.Label("=== CONTROLS ===", GUI.skin.box);
            if (GUILayout.Button("Export Log"))
            {
                string json = _simulation.ExportLog();
                Debug.Log(json);
            }
            
            if (GUILayout.Button("Stop"))
            {
                _simulation.Stop();
            }

            GUILayout.EndArea();
        }

        /// <summary>Get the active simulation (for testing/debugging).</summary>
        public Simulation GetSimulation() => _simulation;
    }
}
