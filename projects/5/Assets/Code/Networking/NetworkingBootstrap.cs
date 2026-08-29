using System;
using UnityEngine;

namespace DunGen.Networking
{
    /// <summary>
    /// Auto-bootstraps the networking layer at runtime.
    /// Creates <see cref="AuthoritativeSessionClient"/>, <see cref="AuthoritativeSessionEventBridge"/>,
    /// <see cref="AuthoritativeSessionStateStore"/>, <see cref="AuthoritativeSessionDebugOverlay"/>,
    /// and <see cref="BackendObservabilityBridge"/>
    /// on a persistent GameObject. Config is loaded from Resources/DunGenNetworkingConfig if present,
    /// otherwise a default in-memory instance is used.
    /// </summary>
    public sealed class NetworkingBootstrap : MonoBehaviour
    {
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (_initialized)
                return;

            // Skip if someone already placed networking components in the scene manually.
            if (FindAnyObjectByType<AuthoritativeSessionClient>() != null)
            {
                _initialized = true;
                return;
            }

            var config = Resources.Load<BackendConnectionConfig>("DunGenNetworkingConfig");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BackendConnectionConfig>();
                Debug.Log("[DunGen.Networking] No DunGenNetworkingConfig asset found in Resources — " +
                          "using default settings (localhost:8081). " +
                          "Create Resources/DunGenNetworkingConfig.asset to customise.");
            }

            var go = new GameObject("DunGen Networking");
            DontDestroyOnLoad(go);

            var client = go.AddComponent<AuthoritativeSessionClient>();
            client.SetConfig(config);

            AddOptionalComponent(go, "DunGen.Networking.AuthoritativeSessionEventBridge");
            AddOptionalComponent(go, "DunGen.Networking.AuthoritativeSessionStateStore");
            AddOptionalComponent(go, "DunGen.Networking.AuthoritativeWorldSceneRenderer");
            AddOptionalComponent(go, "DunGen.Networking.AuthoritativeSessionDebugOverlay");

            var bridge = go.AddComponent<BackendObservabilityBridge>();
            bridge.SetConfig(config);

            go.AddComponent<NetworkingBootstrap>();
            _initialized = true;

            Debug.Log($"[DunGen.Networking] Bootstrapped — backend: {config.AuthoritativeBaseUrl}, " +
                      $"session: {config.DefaultSessionId}");
        }

        private static void AddOptionalComponent(GameObject gameObject, string typeName)
        {
            var componentType = typeof(NetworkingBootstrap).Assembly.GetType(typeName);
            if (componentType == null)
            {
                Debug.LogWarning($"[DunGen.Networking] Could not resolve component type '{typeName}'.");
                return;
            }

            gameObject.AddComponent(componentType);
        }
    }
}
