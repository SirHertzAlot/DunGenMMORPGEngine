using System;
using System.Collections.Generic;
using DunGen.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DunGen.Startup
{
    /// <summary>
    /// Emits client interaction telemetry and periodic player heartbeat snapshots.
    /// </summary>
    public sealed class ClientInteractionTelemetry : MonoBehaviour
    {
        [SerializeField] private float heartbeatIntervalSeconds = 1f;

        private float _nextHeartbeatAt;
        private string _lastLifecycleState = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (FindAnyObjectByType<ClientInteractionTelemetry>() != null)
                return;

            var go = new GameObject("DunGen Client Telemetry");
            DontDestroyOnLoad(go);
            go.AddComponent<ClientInteractionTelemetry>();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            EmitLifecycleState("client_loaded", "Client telemetry bootstrap initialized.");
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void Update()
        {
            EmitStateIfChanged();
            EmitInputActionIfNeeded();

            if (Time.unscaledTime < _nextHeartbeatAt)
                return;

            _nextHeartbeatAt = Time.unscaledTime + Mathf.Max(0.25f, heartbeatIntervalSeconds);
            EmitHeartbeat();
        }

        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            var data = BuildCommonData();
            data["previousScene"] = previous.name ?? string.Empty;
            data["currentScene"] = next.name ?? string.Empty;
            BackendObservabilityBridge.TryEmitClientEvent(
                "client.scene.changed",
                "client.lifecycle",
                ResolveEntityId(),
                $"Scene changed from '{previous.name}' to '{next.name}'.",
                data,
                (uint)Time.frameCount);
        }

        private void EmitStateIfChanged()
        {
            var state = ResolveLifecycleState();
            if (string.Equals(state, _lastLifecycleState, StringComparison.Ordinal))
                return;

            _lastLifecycleState = state;
            EmitLifecycleState(state, $"Client transitioned to state '{state}'.");
        }

        private void EmitLifecycleState(string state, string message)
        {
            var data = BuildCommonData();
            data["lifecycleState"] = state;

            BackendObservabilityBridge.TryEmitClientEvent(
                "client.lifecycle.state",
                "client.lifecycle",
                ResolveEntityId(),
                message,
                data,
                (uint)Time.frameCount);
        }

        private void EmitInputActionIfNeeded()
        {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            var anyKeyDown   = keyboard != null && keyboard.anyKey.wasPressedThisFrame;
            var mouseLeft    = mouse != null && mouse.leftButton.wasPressedThisFrame;
            var mouseRight   = mouse != null && mouse.rightButton.wasPressedThisFrame;
            var mouseMiddle  = mouse != null && mouse.middleButton.wasPressedThisFrame;

            if (!anyKeyDown && !mouseLeft && !mouseRight && !mouseMiddle)
                return;

            var horizontal = 0f;
            var vertical   = 0f;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) horizontal -= 1f;
                if (keyboard.dKey.isPressed) horizontal += 1f;
                if (keyboard.sKey.isPressed) vertical   -= 1f;
                if (keyboard.wKey.isPressed) vertical   += 1f;
            }
#else
            var anyKeyDown  = Input.anyKeyDown;
            var mouseLeft   = Input.GetMouseButtonDown(0);
            var mouseRight  = Input.GetMouseButtonDown(1);
            var mouseMiddle = Input.GetMouseButtonDown(2);

            if (!anyKeyDown && !mouseLeft && !mouseRight && !mouseMiddle)
                return;

            var horizontal = Input.GetAxisRaw("Horizontal");
            var vertical   = Input.GetAxisRaw("Vertical");
#endif
            var data = BuildCommonData();
            data["anyKeyDown"]     = anyKeyDown  ? "1" : "0";
            data["mouseLeftDown"]  = mouseLeft   ? "1" : "0";
            data["mouseRightDown"] = mouseRight  ? "1" : "0";
            data["mouseMiddleDown"]= mouseMiddle ? "1" : "0";
            data["horizontal"]     = horizontal.ToString("F3");
            data["vertical"]       = vertical.ToString("F3");

            BackendObservabilityBridge.TryEmitClientEvent(
                "client.input.action",
                "client.input",
                ResolveEntityId(),
                "User input detected.",
                data,
                (uint)Time.frameCount);
        }

        private void EmitHeartbeat()
        {
            var data = BuildCommonData();
            data["lifecycleState"] = ResolveLifecycleState();

            var playerController = FindAnyObjectByType<SimpleTestPlayerController>();
            if (playerController != null)
            {
                var t = playerController.transform;
                var p = t.position;
                data["playerPositionX"] = p.x.ToString("F3");
                data["playerPositionY"] = p.y.ToString("F3");
                data["playerPositionZ"] = p.z.ToString("F3");
                data["playerYaw"] = t.eulerAngles.y.ToString("F2");
            }

            var starter = FindAnyObjectByType<SimulationStarter>();
            if (starter != null && starter.TryGetCurrentGameState(out var state))
            {
                data["playerTileX"] = state.PlayerX.ToString();
                data["playerTileY"] = state.PlayerY.ToString();
                data["playerHealth"] = state.PlayerHealth.ToString();
                data["playerMaxHealth"] = state.PlayerMaxHealth.ToString();
                data["playerLevel"] = state.PlayerLevel.ToString();
                data["playerXP"] = state.PlayerXP.ToString();
                data["playerGold"] = state.PlayerGold.ToString();
                data["playerInventoryItemCount"] = state.PlayerInventoryItemCount.ToString();
                data["dungeonLevel"] = state.CurrentLevel.ToString();
                data["turnCount"] = state.TurnCount.ToString();
                data["sessionState"] = state.SessionState ?? string.Empty;
            }

            BackendObservabilityBridge.TryEmitClientEvent(
                "client.player.heartbeat",
                "client.heartbeat",
                ResolveEntityId(),
                "Client heartbeat snapshot.",
                data,
                (uint)Time.frameCount);
        }

        private static Dictionary<string, string> BuildCommonData()
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["unityTime"] = Time.unscaledTime.ToString("F3"),
                ["frame"] = Time.frameCount.ToString(),
                ["scene"] = SceneManager.GetActiveScene().name ?? string.Empty,
                ["authenticated"] = ClientAuthState.IsAuthenticated ? "1" : "0",
                ["username"] = ClientAuthState.AuthenticatedUsername ?? string.Empty,
            };

            var networking = FindAnyObjectByType<BackendObservabilityBridge>();
            if (networking != null)
            {
                data["observabilityBridgePresent"] = "1";
            }

            return data;
        }

        private static string ResolveEntityId()
        {
            var username = ClientAuthState.AuthenticatedUsername;
            if (!string.IsNullOrWhiteSpace(username))
                return $"player:{username.Trim()}";

            return "player:anonymous";
        }

        private static string ResolveLifecycleState()
        {
            var sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            if (!ClientAuthState.IsAuthenticated)
                return "login_screen";

            if (sceneName.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0)
                return "character_select";

            if (sceneName.IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0)
                return "server_transfer";

            if (sceneName.IndexOf("dungeon", StringComparison.OrdinalIgnoreCase) >= 0)
                return "in_dungeon";

            return "in_world";
        }
    }
}
