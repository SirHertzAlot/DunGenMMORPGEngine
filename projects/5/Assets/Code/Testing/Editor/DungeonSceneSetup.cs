using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DunGen.Networking;

namespace DunGen.Testing.Editor
{
    /// <summary>
    /// One-click scene setup for dungeon playtest.
    /// Opens SampleScene, instantiates the POLYGON Dungeons FBX, adds MeshColliders
    /// to all geometry children, spawns a player with CharacterController, and wires
    /// up the backend networking components for live world data.
    ///
    /// Menu: DunGen/Testing/Setup Dungeon Playtest Scene
    /// </summary>
    public static class DungeonSceneSetup
    {
        private const string SampleScenePath    = "Assets/Scenes/SampleScene.unity";
        private const string DungeonFbxPath     = "Assets/Models/SourceFiles/POLYGON_Dungeons_Demo_Scene.fbx";
        private const string DungeonRootName    = "POLYGON_Dungeons_Demo_Scene";
        private const string PlayerName         = "DunGen Test Player";
        private const string NetworkingRootName = "DunGen Backend";
        private const string BackendConfigPath  = "Assets/DunGenMMORPGEngine/projects/5/Assets/Config/BackendConnectionConfig.asset";

        [MenuItem("DunGen/Testing/Setup Dungeon Playtest Scene")]
        public static void SetupDungeonPlaytestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var dungeon = SetupDungeon();
            SetupPlayer(dungeon);
            SetupBackendNetworking();
            EnsureSimulationStarter();
            EnsureDirectionalLight();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[DunGen] Dungeon playtest scene ready. Press Play to test.");
        }

        // -----------------------------------------------------------------------

        private static GameObject SetupDungeon()
        {
            // Reuse existing instance so running the menu twice stays idempotent.
            var existing = GameObject.Find(DungeonRootName);
            if (existing != null)
            {
                AddMissingMeshColliders(existing);
                return existing;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DungeonFbxPath);
            if (prefab == null)
            {
                Debug.LogError($"[DunGen] Could not load dungeon FBX at '{DungeonFbxPath}'. " +
                               "Check that the file has been imported into the project. Creating a fallback marker.");
                return CreateFallbackDungeonMarker();
            }

            var dungeon = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            dungeon.name = DungeonRootName;
            dungeon.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(dungeon, "Setup Dungeon");

            AddMissingMeshColliders(dungeon);
            return dungeon;
        }

        /// <summary>
        /// Adds a MeshCollider to every child that has a MeshFilter but no Collider yet.
        /// </summary>
        private static void AddMissingMeshColliders(GameObject root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            int added = 0;
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null)
                    continue;
                if (filter.GetComponent<Collider>() != null)
                    continue;

                var col = Undo.AddComponent<MeshCollider>(filter.gameObject);
                col.sharedMesh = filter.sharedMesh;
                added++;
            }

            if (added > 0)
                Debug.Log($"[DunGen] Added {added} MeshCollider(s) to '{root.name}'.");
        }

        private static GameObject CreateFallbackDungeonMarker()
        {
            var root = new GameObject(DungeonRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Fallback Dungeon Marker");

            var marker = GameObject.CreatePrimitive(PrimitiveType.Plane);
            marker.name = "MissingDungeonFbxMarker";
            marker.transform.SetParent(root.transform, false);
            marker.transform.localScale = new Vector3(5f, 1f, 5f);

            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial.color = Color.red;

            return root;
        }

        // Test hook for validating fallback creation behavior without invoking full menu flow.
        public static GameObject CreateFallbackDungeonMarkerForTests()
        {
            return CreateFallbackDungeonMarker();
        }

        private static void SetupPlayer(GameObject dungeon)
        {
            var existing = GameObject.Find(PlayerName);
            if (existing != null)
                return;

            var player = new GameObject(PlayerName);
            Undo.RegisterCreatedObjectUndo(player, "Setup Dungeon Player");

            // Place player above dungeon floor so they land on it.
            player.transform.position = new Vector3(0f, 3f, 0f);

            // Visual — capsule so the character is visible.
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Visual";
            capsule.transform.SetParent(player.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 0f, 0f);
            Object.DestroyImmediate(capsule.GetComponent<CapsuleCollider>()); // CC provides collision

            // Physics.
            var cc = player.AddComponent<CharacterController>();
            cc.height  = 2f;
            cc.radius  = 0.4f;
            cc.center  = new Vector3(0f, 0f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;

            // Controller script.
            player.AddComponent<DunGen.Startup.SimpleTestPlayerController>();

            // Third-person orbital camera centered around the player.
            var camGo = new GameObject("Orbital Camera");
            camGo.transform.position = player.transform.position + new Vector3(0f, 3f, -6f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane  = 0.1f;
            cam.farClipPlane   = 500f;
            var orbital = camGo.AddComponent<DunGen.Startup.OrbitalCameraFollow>();
            orbital.SetTarget(player.transform);
            camGo.tag = "MainCamera";

            Debug.Log($"[DunGen] Player '{PlayerName}' created.");
        }

        private static void SetupBackendNetworking()
        {
            var existing = GameObject.Find(NetworkingRootName);
            if (existing != null)
                return;

            var root = new GameObject(NetworkingRootName);
            Undo.RegisterCreatedObjectUndo(root, "Setup Backend Networking");

            // Load or create the BackendConnectionConfig asset.
            var config = AssetDatabase.LoadAssetAtPath<BackendConnectionConfig>(BackendConfigPath);
            if (config == null)
            {
                var dir = System.IO.Path.GetDirectoryName(BackendConfigPath);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir!);

                config = ScriptableObject.CreateInstance<BackendConnectionConfig>();
                AssetDatabase.CreateAsset(config, BackendConfigPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[DunGen] Created BackendConnectionConfig at '{BackendConfigPath}'. Edit it to set your server URL and session ID.");
            }

            // Wire up all three networking components onto the same GameObject.
            var stateStore = root.AddComponent<AuthoritativeSessionStateStore>();
            var renderer   = root.AddComponent<AuthoritativeWorldSceneRenderer>();
            var client     = root.AddComponent<AuthoritativeSessionClient>();
            client.SetConfig(config);

            // Enable world polling so it auto-fetches on Play.
            var so = new SerializedObject(client);
            so.FindProperty("autoPollWorld").boolValue = true;
            so.ApplyModifiedProperties();

            Debug.Log($"[DunGen] Backend networking object '{NetworkingRootName}' added. Press Play to fetch world data from the server.");
        }

        private static void EnsureSimulationStarter()
        {
            if (Object.FindAnyObjectByType<DunGen.Startup.SimulationStarter>() != null)
                return;

            var starter = new GameObject("DunGen Simulation Starter");
            Undo.RegisterCreatedObjectUndo(starter, "Setup Simulation Starter");
            starter.AddComponent<DunGen.Startup.SimulationStarter>();
            Debug.Log("[DunGen] Added SimulationStarter to scene for immediate MVP playtest readiness.");
        }

        // Test hook for validating starter injection behavior without invoking full menu flow.
        public static void EnsureSimulationStarterForTests()
        {
            EnsureSimulationStarter();
        }

        private static void EnsureDirectionalLight()
        {
            // If there's already a directional light leave it alone.
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                    return;
            }

            var lightGo = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(lightGo, "Setup Dungeon Light");
            lightGo.transform.SetPositionAndRotation(
                new Vector3(0f, 10f, 0f),
                Quaternion.Euler(50f, -30f, 0f));
            var light = lightGo.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 1f;
            light.color     = new Color(1f, 0.95f, 0.84f);
        }
    }
}
