using System;
using DunGen.Client;
using DunGen.Core;
using DunGen.Gameplay;
using UnityEngine;
using CoreSimulation = DunGen.Core.Simulation;

namespace DunGen.Startup
{
    /// <summary>
    /// MonoBehaviour that initializes the simulation and local client session on play.
    /// </summary>
    public class SimulationStarter : MonoBehaviour
    {
        private CoreSimulation _simulation;
        private GameSession _gameSession;
        private IClientSession _clientSession;
        private int _nextClientSequence = 1;
        private string _lastClientMessage = "";

        public ulong SimulationSeed = 42;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (FindAnyObjectByType<SimulationStarter>() != null)
                return;

            var bootstrap = new GameObject("DunGen Simulation Starter");
            DontDestroyOnLoad(bootstrap);
            bootstrap.AddComponent<SimulationStarter>();
        }

        private void Start()
        {
            var starters = FindObjectsByType<SimulationStarter>(FindObjectsSortMode.None);
            if (starters.Length > 1 && starters[0] != this)
            {
                Destroy(gameObject);
                return;
            }

            _simulation = new CoreSimulation();
            _simulation.Initialize(SimulationSeed);

            Debug.Log($"Simulation initialized with seed: {SimulationSeed}");
            Debug.Log("Event Bus ready");
            Debug.Log("Event Log started");

            _gameSession = new GameSession((int)SimulationSeed);
            _gameSession.StartGame();
            _clientSession = new LocalGameSessionClient(_gameSession);

            Debug.Log("Game session started - Ready to play.");
        }

        private void Update()
        {
            if (_simulation != null && _simulation.IsRunning)
                _simulation.SimulationStep(Time.deltaTime);

            HandleClientInput();
        }

        private void HandleClientInput()
        {
            if (_clientSession == null)
                return;

            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                SubmitClientCommand(ClientCommand.Move(0, -1));
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                SubmitClientCommand(ClientCommand.Move(0, 1));
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                SubmitClientCommand(ClientCommand.Move(-1, 0));
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                SubmitClientCommand(ClientCommand.Move(1, 0));
            else if (Input.GetKeyDown(KeyCode.Space))
                SubmitClientCommand(ClientCommand.Wait());
            else if (Input.GetKeyDown(KeyCode.F))
                SubmitClientCommand(ClientCommand.AttackNearest());
        }

        private void SubmitClientCommand(ClientCommand command)
        {
            var envelope = ClientCommandEnvelope.Create(
                Guid.NewGuid().ToString("N"),
                _nextClientSequence,
                command);

            var result = _clientSession.Submit(envelope);
            _lastClientMessage = result.Message;
            if (result.Accepted && !result.Duplicate)
                _nextClientSequence++;

            if (!result.Accepted)
                Debug.LogWarning(result.Message);
        }

        private void OnGUI()
        {
            if (_simulation == null)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 520, 430));

            GUILayout.Label("=== SIMULATION ===", GUI.skin.box);
            GUILayout.Label($"Status: {(_simulation.IsRunning ? "Running" : "Stopped")}", GUI.skin.box);
            GUILayout.Label($"Frame: {_simulation.GetFrameNumber()}", GUI.skin.box);
            GUILayout.Label($"Seed: {_simulation.GetSeed()}", GUI.skin.box);
            GUILayout.Label($"Events: {_simulation.GetEventLog().GetEvents().Count}", GUI.skin.box);

            if (_gameSession != null)
            {
                GUILayout.Label("=== LOCAL CLIENT ===", GUI.skin.box);
                var snapshot = _clientSession?.GetSnapshot();
                GUILayout.Label(snapshot?.ToString() ?? _gameSession.GetGameState().ToString(), GUI.skin.box);
                if (!string.IsNullOrWhiteSpace(_lastClientMessage))
                    GUILayout.Label(_lastClientMessage, GUI.skin.box);

                if (GUILayout.Button("Wait / End Turn"))
                    SubmitClientCommand(ClientCommand.Wait());

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Up"))
                    SubmitClientCommand(ClientCommand.Move(0, -1));
                if (GUILayout.Button("Down"))
                    SubmitClientCommand(ClientCommand.Move(0, 1));
                if (GUILayout.Button("Left"))
                    SubmitClientCommand(ClientCommand.Move(-1, 0));
                if (GUILayout.Button("Right"))
                    SubmitClientCommand(ClientCommand.Move(1, 0));
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Attack Nearest"))
                    SubmitClientCommand(ClientCommand.AttackNearest());
            }

            GUILayout.Label("=== CONTROLS ===", GUI.skin.box);
            if (GUILayout.Button("Export Log"))
            {
                string json = _simulation.ExportLog();
                Debug.Log(json);
            }

            if (GUILayout.Button("Stop"))
                _simulation.Stop();

            GUILayout.EndArea();
        }

        /// <summary>Get the active simulation (for testing/debugging).</summary>
        public CoreSimulation GetSimulation() => _simulation;
    }
}
