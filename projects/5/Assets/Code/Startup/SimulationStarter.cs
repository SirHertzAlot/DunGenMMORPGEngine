using System.Collections;
using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using System.Text;
using DunGen.Core;
using DunGen.Events;
using DunGen.Gameplay;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DunGen.Startup
{
    public static class ClientAuthState
    {
        public const string DefaultTestUsername = "test";
        public const string DefaultTestPassword = "test";

        public static bool IsAuthenticated { get; private set; }
        public static string AuthenticatedUsername { get; private set; } = "";
        public static string AuthToken { get; private set; } = "";
        public static string RequestCanary { get; private set; } = "";
        public static DateTime TokenExpiresUtc { get; private set; } = DateTime.MinValue;

        public static bool HasValidToken =>
            IsAuthenticated &&
            !string.IsNullOrWhiteSpace(AuthToken) &&
            !string.IsNullOrWhiteSpace(RequestCanary) &&
            TokenExpiresUtc > DateTime.UtcNow;

        public static bool TryAuthenticate(string username, string password)
        {
            var isValid = username == DefaultTestUsername && password == DefaultTestPassword;
            if (!isValid)
                return false;

            IsAuthenticated = true;
            AuthenticatedUsername = username;
            AuthToken = string.Empty;
            RequestCanary = string.Empty;
            TokenExpiresUtc = DateTime.MinValue;
            return true;
        }

        public static bool SetAuthenticatedSession(string username, string token, string canary, DateTime expiresUtc)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(canary))
                return false;

            IsAuthenticated = true;
            AuthenticatedUsername = username.Trim();
            AuthToken = token.Trim();
            RequestCanary = canary.Trim();
            TokenExpiresUtc = expiresUtc;
            return true;
        }

        public static void Clear()
        {
            IsAuthenticated = false;
            AuthenticatedUsername = string.Empty;
            AuthToken = string.Empty;
            RequestCanary = string.Empty;
            TokenExpiresUtc = DateTime.MinValue;
        }
    }

    /// <summary>
    /// MonoBehaviour that initializes the simulation and game session on play.
    /// Attach to a GameObject in the scene and press Play to start a playable MVP game.
    /// </summary>
    public class SimulationStarter : MonoBehaviour
    {
        private DunGen.Core.Simulation _simulation;
        private GameSession _gameSession;
        private string _worldSource = "local";
        [SerializeField] private float minClientTurnIntervalSeconds = 0.1f;
        [SerializeField] private bool allowLocalOfflineBootstrap = false;
        [SerializeField] private bool autoCreateMvpVisualMarkers = true;
        [SerializeField] private float tileToWorldScale = 1f;
        public ulong SimulationSeed = 42;

        [Header("Runtime Dungeon Instantiation")]
        [SerializeField] private DunGen.Runtime.RuntimeDungeonInstantiator runtimeDungeonInstantiator;
        [SerializeField] private bool autoInstantiateDungeon = true;

        private string _pendingTurnReason;
        private float _nextAllowedTurnAt;
        private string _lastCommandFeedback = "Ready";
        private readonly Dictionary<int, GameObject> _enemyMarkers = new();
        private GameObject _playerMarker;
        private readonly Queue<string> _recentEvents = new();
        private int _lastEventCount;
        private string _lastReplayHash = "n/a";
        private string _lastReplayExportPath = "n/a";

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
            if (!ClientAuthState.IsAuthenticated)
            {
                if (!allowLocalOfflineBootstrap || !ClientAuthState.TryAuthenticate(ClientAuthState.DefaultTestUsername, ClientAuthState.DefaultTestPassword))
                {
                    Debug.Log("[DunGen] Simulation start blocked until user logs in.");
                    Destroy(gameObject);
                    return;
                }

                Debug.Log("[DunGen] Local MVP offline bootstrap enabled with test credentials.");
            }

            var starters = FindObjectsByType<SimulationStarter>(FindObjectsInactive.Exclude);
            if (starters.Length > 1 && starters[0] != this)
            {
                Destroy(gameObject);
                return;
            }

            StartCoroutine(BootstrapGameplaySession());
        }

        private IEnumerator BootstrapGameplaySession()
        {
            var authoritativeStore = FindAuthoritativeStateStore();
            float waitBudgetSeconds = authoritativeStore != null ? 5f : 0f;
            while (authoritativeStore != null && !GetPropertyValue(authoritativeStore, "HasWorldSnapshot", false) && string.IsNullOrWhiteSpace(GetPropertyValue(authoritativeStore, "LastError", string.Empty)) && waitBudgetSeconds > 0f)
            {
                waitBudgetSeconds -= Time.unscaledDeltaTime;
                yield return null;
            }

            var authoritativeWorld = BuildAuthoritativeWorldBlueprint(authoritativeStore);
            if (authoritativeWorld != null)
            {
                SimulationSeed = (ulong)authoritativeWorld.Seed;
                _worldSource = $"authoritative:{GetPropertyValue(authoritativeStore, "ExecutionId", string.Empty)}";
            }

            _simulation = new DunGen.Core.Simulation();
            _simulation.Initialize(SimulationSeed);
            
            Debug.Log($"✓ Simulation initialized with seed: {SimulationSeed}");
            Debug.Log($"✓ Event Bus ready");
            Debug.Log($"✓ Event Log started");
            
            // Initialize game session for MVP
            _gameSession = new GameSession((int)SimulationSeed);
            _gameSession.StartGame(authoritativeWorld);
            
            Debug.Log($"✓ Game session started - Ready to play! ({_worldSource})");

            // Instantiate runtime dungeon in the scene only if runtime is fully active
            if (autoInstantiateDungeon && _simulation != null && _gameSession != null)
            {
                if (runtimeDungeonInstantiator == null)
                {
                    runtimeDungeonInstantiator = FindAnyObjectByType<DunGen.Runtime.RuntimeDungeonInstantiator>();
                    if (runtimeDungeonInstantiator == null)
                    {
                        var go = new GameObject("RuntimeDungeonInstantiator");
                        runtimeDungeonInstantiator = go.AddComponent<DunGen.Runtime.RuntimeDungeonInstantiator>();
                    }
                }
                
                // Only instantiate if runtime is fully initialized and active
                bool isRuntimeActive = _simulation != null && _gameSession != null && !_gameSession.IsGameOver;
                var blueprint = authoritativeWorld ?? BuildDefaultBlueprintFromSession(_gameSession);
                runtimeDungeonInstantiator.TryInstantiateDungeon(blueprint, isRuntimeActive);
            }
        }

        // Build a session-derived blueprint so scene instantiation aligns with gameplay state.
        private static AuthoritativeWorldBlueprint BuildDefaultBlueprintFromSession(GameSession session)
        {
            if (session == null)
                return null;

            var state = session.GetGameState();
            var enemies = session.GetLivingEnemySnapshots();

            const int width = 80;
            const int height = 24;
            return new AuthoritativeWorldBlueprint
            {
                Seed = state.SessionSeed,
                Width = width,
                Height = height,
                DungeonLevel = state.CurrentLevel,
                Rooms = new List<AuthoritativeWorldRoomBlueprint>
                {
                    new AuthoritativeWorldRoomBlueprint
                    {
                        Id = 1,
                        X = 1,
                        Y = 1,
                        Width = width - 2,
                        Height = height - 2,
                    }
                },
                Enemies = BuildEnemyBlueprints(enemies),
                Loot = new List<AuthoritativeWorldLootBlueprint>()
            };
        }

        private static List<AuthoritativeWorldEnemyBlueprint> BuildEnemyBlueprints(IReadOnlyList<GameSession.EnemySnapshot> snapshots)
        {
            var result = new List<AuthoritativeWorldEnemyBlueprint>();
            if (snapshots == null)
                return result;

            for (int i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                result.Add(new AuthoritativeWorldEnemyBlueprint
                {
                    Id = snapshot.EntityId,
                    Archetype = "enemy",
                    X = snapshot.X,
                    Y = snapshot.Y,
                    Level = 1,
                });
            }

            return result;
        }

        private void Update()
        {
            if (_simulation != null && _simulation.IsRunning)
            {
                _simulation.SimulationStep(Time.deltaTime);
            }

            ProcessClientTurnLoop();
            ProcessKeyboardInput();
            SyncVisualMarkers();
            RefreshRecentEvents();

            // Clean up dungeon if game ends (runtime becomes inactive)
            if (_gameSession != null && _gameSession.IsGameOver && runtimeDungeonInstantiator != null && runtimeDungeonInstantiator.IsDungeonActive)
            {
                runtimeDungeonInstantiator.CleanupDungeon();
            }
        }

        private void ProcessClientTurnLoop()
        {
            if (_gameSession == null || _gameSession.IsGameOver)
                return;

            if (string.IsNullOrWhiteSpace(_pendingTurnReason))
                return;

            if (Time.unscaledTime < _nextAllowedTurnAt)
                return;

            ExecuteTurnInternal(_pendingTurnReason);
            _pendingTurnReason = null;
        }

        public bool TryExecuteTurn(string reason = "manual")
        {
            if (_gameSession == null || _gameSession.IsGameOver)
                return false;

            if (Time.unscaledTime < _nextAllowedTurnAt)
            {
                _pendingTurnReason = reason;
                return true;
            }

            ExecuteTurnInternal(reason);
            return true;
        }

        public bool TrySubmitPlayerMove(int deltaX, int deltaY, string reason = "player-move")
        {
            if (_gameSession == null || _gameSession.IsGameOver)
                return false;

            var moveResult = _gameSession.QueuePlayerMove(deltaX, deltaY, _gameSession.TurnCount);
            _lastCommandFeedback = moveResult.Message;
            if (!moveResult.IsAccepted)
            {
                Debug.Log($"[DunGen] {moveResult.Message}");
                return false;
            }

            return TryExecuteTurn(reason);
        }

        public bool TrySubmitPlayerAttack(string reason = "player-attack")
        {
            if (_gameSession == null || _gameSession.IsGameOver)
                return false;

            _gameSession.QueuePlayerAttack();
            _lastCommandFeedback = "Queued attack command.";
            return TryExecuteTurn(reason);
        }

        private void ExecuteTurnInternal(string reason)
        {
            _gameSession.ExecuteTurn();
            _nextAllowedTurnAt = Time.unscaledTime + Mathf.Max(0.01f, minClientTurnIntervalSeconds);
            _lastCommandFeedback = $"Executed turn via {reason}.";
            Debug.Log($"[DunGen] Executed turn via {reason}.");
        }

        public bool TryGetCurrentGameState(out GameState state)
        {
            if (_gameSession == null)
            {
                state = default;
                return false;
            }

            state = _gameSession.GetGameState();
            return true;
        }

        private void OnGUI()
        {
            if (_simulation == null)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 620, 520));
            
            // Simulation info
            GUILayout.Label("=== SIMULATION ===", GUI.skin.box);
            GUILayout.Label($"Status: {(_simulation.IsRunning ? "Running" : "Stopped")}", GUI.skin.box);
            GUILayout.Label($"Frame: {_simulation.GetFrameNumber()}", GUI.skin.box);
            GUILayout.Label($"Seed: {_simulation.GetSeed()}", GUI.skin.box);
            GUILayout.Label($"Events: {_simulation.GetEventLog().GetEvents().Count}", GUI.skin.box);
            GUILayout.Label($"World Source: {_worldSource}", GUI.skin.box);
            GUILayout.Label($"Replay Hash: {_lastReplayHash}", GUI.skin.box);
            GUILayout.Label($"Replay Export: {_lastReplayExportPath}", GUI.skin.box);
            GUILayout.Label($"MVP Smoke: {BuildMvpSmokeStatus()}", GUI.skin.box);
            
            // Game session info
            if (_gameSession != null)
            {
                GUILayout.Label("=== GAME SESSION ===", GUI.skin.box);
                var gameState = _gameSession.GetGameState();
                GUILayout.Label(gameState.ToString(), GUI.skin.box);
                GUILayout.Label($"Player Tile: ({gameState.PlayerX}, {gameState.PlayerY}) | Living Enemies: {gameState.LivingEnemies}", GUI.skin.box);
                GUILayout.Label($"Last Command Feedback: {_lastCommandFeedback}", GUI.skin.box);
                GUILayout.Label("Commands: W/A/S/D move, F or Left Mouse attack", GUI.skin.box);

                GUILayout.Label("Recent Events", GUI.skin.box);
                foreach (var evt in _recentEvents)
                {
                    GUILayout.Label(evt, GUI.skin.box);
                }
                
                if (GUILayout.Button("Execute Turn"))
                {
                    TryExecuteTurn("gui");
                }
            }
            
            // Simulation controls
            GUILayout.Label("=== CONTROLS ===", GUI.skin.box);
            if (GUILayout.Button("Export Log"))
            {
                string json = _simulation.ExportLog();
                _lastReplayHash = ComputeReplayHash(json);
                _lastReplayExportPath = WriteReplayLogToDisk(json, _lastReplayHash);
                Debug.Log(json);
                Debug.Log($"[DunGen] Replay hash: {_lastReplayHash}");
                Debug.Log($"[DunGen] Replay exported: {_lastReplayExportPath}");
            }
            
            if (GUILayout.Button("Stop"))
            {
                _simulation.Stop();
            }

            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            // Clean up dungeon when SimulationStarter is destroyed (runtime shutdown)
            if (runtimeDungeonInstantiator != null && runtimeDungeonInstantiator.IsDungeonActive)
            {
                runtimeDungeonInstantiator.CleanupDungeon();
            }

            if (_playerMarker != null)
                Destroy(_playerMarker);

            foreach (var marker in _enemyMarkers.Values)
            {
                if (marker != null)
                    Destroy(marker);
            }
            _enemyMarkers.Clear();
        }

        private void ProcessKeyboardInput()
        {
            if (_gameSession == null || _gameSession.IsGameOver)
                return;

            if (TryReadMoveInput(out var dx, out var dy))
            {
                TrySubmitPlayerMove(dx, dy, "keyboard");
            }

            if (WasAttackPressed())
            {
                TrySubmitPlayerAttack("keyboard");
            }
        }

        private void SyncVisualMarkers()
        {
            if (!autoCreateMvpVisualMarkers || _gameSession == null)
                return;

            var state = _gameSession.GetGameState();
            EnsurePlayerMarker();
            if (_playerMarker != null)
                _playerMarker.transform.position = new UnityEngine.Vector3(state.PlayerX * tileToWorldScale, 0.75f, state.PlayerY * tileToWorldScale);

            var snapshots = _gameSession.GetLivingEnemySnapshots();
            var activeIds = new HashSet<int>();
            for (int i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                activeIds.Add(snapshot.EntityId);
                if (!_enemyMarkers.TryGetValue(snapshot.EntityId, out var marker) || marker == null)
                {
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    marker.name = $"EnemyMarker_{snapshot.EntityId}";
                    marker.transform.localScale = new UnityEngine.Vector3(0.7f, 0.7f, 0.7f);
                    var renderer = marker.GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.material.color = Color.red;
                    _enemyMarkers[snapshot.EntityId] = marker;
                }

                marker.transform.position = new UnityEngine.Vector3(snapshot.X * tileToWorldScale, 0.5f, snapshot.Y * tileToWorldScale);
            }

            var removeIds = new List<int>();
            foreach (var kvp in _enemyMarkers)
            {
                if (!activeIds.Contains(kvp.Key))
                    removeIds.Add(kvp.Key);
            }

            for (int i = 0; i < removeIds.Count; i++)
            {
                var id = removeIds[i];
                if (_enemyMarkers.TryGetValue(id, out var marker) && marker != null)
                    Destroy(marker);

                _enemyMarkers.Remove(id);
            }
        }

        private void EnsurePlayerMarker()
        {
            if (_playerMarker != null)
                return;

            _playerMarker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _playerMarker.name = "PlayerMarker";
            _playerMarker.transform.localScale = new UnityEngine.Vector3(0.8f, 1.2f, 0.8f);
            var renderer = _playerMarker.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = Color.cyan;
        }

        private void RefreshRecentEvents()
        {
            if (_simulation == null)
                return;

            var events = _simulation.GetEventLog().GetEvents();
            if (events.Count == _lastEventCount)
                return;

            for (int i = _lastEventCount; i < events.Count; i++)
            {
                _recentEvents.Enqueue(events[i].ToString());
                while (_recentEvents.Count > 6)
                {
                    _recentEvents.Dequeue();
                }
            }

            _lastEventCount = events.Count;
            _lastReplayHash = ComputeReplayHash(_simulation.ExportLog());
        }

        private static string ComputeReplayHash(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return "empty";

            var bytes = Encoding.UTF8.GetBytes(payload);
            byte[] hash;
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(bytes);
            }

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private string BuildMvpSmokeStatus()
        {
            if (_simulation == null)
                return "simulation:down";

            var simState = _simulation.IsRunning ? "simulation:up" : "simulation:stopped";
            var sessionState = _gameSession != null ? "session:up" : "session:down";
            var commandState = string.IsNullOrWhiteSpace(_lastCommandFeedback) ? "command:unknown" : "command:ok";
            var replayState = string.Equals(_lastReplayHash, "n/a", StringComparison.OrdinalIgnoreCase) ? "replay:pending" : "replay:ok";
            var markerState = autoCreateMvpVisualMarkers ? "markers:on" : "markers:off";
            return $"{simState} | {sessionState} | {commandState} | {replayState} | {markerState}";
        }

        private static string WriteReplayLogToDisk(string payload, string replayHash)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var fileName = $"replay_{timestamp}_{replayHash}.json";
                var path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
                System.IO.File.WriteAllText(path, payload ?? string.Empty);
                return path;
            }
            catch (Exception ex)
            {
                return $"export_failed:{ex.Message}";
            }
        }

        private static bool TryReadMoveInput(out int dx, out int dy)
        {
            dx = 0;
            dy = 0;

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            if (Keyboard.current == null)
                return false;

            if (Keyboard.current.wKey.wasPressedThisFrame) { dy = 1; return true; }
            if (Keyboard.current.sKey.wasPressedThisFrame) { dy = -1; return true; }
            if (Keyboard.current.aKey.wasPressedThisFrame) { dx = -1; return true; }
            if (Keyboard.current.dKey.wasPressedThisFrame) { dx = 1; return true; }
            return false;
#else
            if (Input.GetKeyDown(KeyCode.W)) { dy = 1; return true; }
            if (Input.GetKeyDown(KeyCode.S)) { dy = -1; return true; }
            if (Input.GetKeyDown(KeyCode.A)) { dx = -1; return true; }
            if (Input.GetKeyDown(KeyCode.D)) { dx = 1; return true; }
            return false;
#endif
        }

        private static bool WasAttackPressed()
        {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            var keyboardPressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            var mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            return keyboardPressed || mousePressed;
#else
            return Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0);
#endif
        }

        /// <summary>Get the active simulation (for testing/debugging).</summary>
        public DunGen.Core.Simulation GetSimulation() => _simulation;

        private static AuthoritativeWorldBlueprint BuildAuthoritativeWorldBlueprint(Component authoritativeStore)
        {
            if (authoritativeStore == null || !GetPropertyValue(authoritativeStore, "HasWorldSnapshot", false))
                return null;

            var blueprint = new AuthoritativeWorldBlueprint
            {
                Seed = GetPropertyValue(authoritativeStore, "WorldSeed", 0),
                Width = GetPropertyValue(authoritativeStore, "WorldWidth", 80),
                Height = GetPropertyValue(authoritativeStore, "WorldHeight", 24),
                DungeonLevel = GetPropertyValue(authoritativeStore, "WorldDungeonLevel", 1),
            };

            foreach (var room in EnumerateProperty(authoritativeStore, "Rooms"))
            {
                blueprint.Rooms.Add(new AuthoritativeWorldRoomBlueprint
                {
                    Id = GetFieldOrPropertyValue(room, "Id", 0),
                    X = GetFieldOrPropertyValue(room, "X", 0),
                    Y = GetFieldOrPropertyValue(room, "Y", 0),
                    Width = GetFieldOrPropertyValue(room, "Width", 1),
                    Height = GetFieldOrPropertyValue(room, "Height", 1),
                });
            }

            foreach (var enemy in EnumerateProperty(authoritativeStore, "Enemies"))
            {
                blueprint.Enemies.Add(new AuthoritativeWorldEnemyBlueprint
                {
                    Id = GetFieldOrPropertyValue(enemy, "Id", 0),
                    Archetype = GetFieldOrPropertyValue(enemy, "Archetype", string.Empty),
                    X = GetFieldOrPropertyValue(enemy, "X", 0),
                    Y = GetFieldOrPropertyValue(enemy, "Y", 0),
                    Level = GetFieldOrPropertyValue(enemy, "Level", 1),
                });
            }

            foreach (var loot in EnumerateProperty(authoritativeStore, "Loot"))
            {
                blueprint.Loot.Add(new AuthoritativeWorldLootBlueprint
                {
                    ItemId = GetFieldOrPropertyValue(loot, "ItemId", string.Empty),
                    ItemType = GetFieldOrPropertyValue(loot, "ItemType", string.Empty),
                    Tier = GetFieldOrPropertyValue(loot, "Tier", string.Empty),
                    X = GetFieldOrPropertyValue(loot, "X", 0),
                    Y = GetFieldOrPropertyValue(loot, "Y", 0),
                });
            }

            return blueprint;
        }

        private static Component FindAuthoritativeStateStore()
        {
            var storeType = System.Type.GetType("DunGen.Networking.AuthoritativeSessionStateStore, DunGen.Networking");
            if (storeType == null)
                return null;

            var instances = Resources.FindObjectsOfTypeAll(storeType);
            if (instances == null || instances.Length == 0)
                return null;

            for (int i = 0; i < instances.Length; i++)
            {
                if (instances[i] is Component component)
                    return component;
            }

            return null;
        }

        private static IEnumerable EnumerateProperty(object source, string propertyName)
        {
            var property = source?.GetType().GetProperty(propertyName);
            if (property?.GetValue(source) is IEnumerable enumerable)
                return enumerable;

            return System.Array.Empty<object>();
        }

        private static T GetPropertyValue<T>(object source, string propertyName, T fallback)
        {
            var property = source?.GetType().GetProperty(propertyName);
            if (property == null)
                return fallback;

            var value = property.GetValue(source);
            return ConvertValue(value, fallback);
        }

        private static T GetFieldOrPropertyValue<T>(object source, string memberName, T fallback)
        {
            if (source == null)
                return fallback;

            var field = source.GetType().GetField(memberName);
            if (field != null)
                return ConvertValue(field.GetValue(source), fallback);

            var property = source.GetType().GetProperty(memberName);
            if (property != null)
                return ConvertValue(property.GetValue(source), fallback);

            return fallback;
        }

        private static T ConvertValue<T>(object value, T fallback)
        {
            if (value is T typed)
                return typed;

            if (value == null)
                return fallback;

            try
            {
                return (T)System.Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return fallback;
            }
        }
    }
}
