using UnityEngine;
using DunGen.Networking;

namespace DunGen.Startup
{
    /// <summary>
    /// Spawns or repositions a controllable 3D test player seamlessly on the generated terrain/dungeon rooms.
    /// </summary>
    public static class TestWorldPlayerBootstrap
    {
        public static GameObject EnsurePlayerInCurrentScene()
        {
            var controller = Object.FindAnyObjectByType<SimpleTestPlayerController>();
            if (controller != null)
            {
                RepositionPlayerToSpawn(controller.gameObject);
                return controller.gameObject;
            }

            var spawnPos = ResolveSpawnPosition(out var spawnRot);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "DunGen Test Player";
            player.transform.SetPositionAndRotation(spawnPos, spawnRot);

            var primitiveCollider = player.GetComponent<CapsuleCollider>();
            if (primitiveCollider != null)
                Object.Destroy(primitiveCollider);

            var charController = player.AddComponent<CharacterController>();
            charController.height = 1.8f;
            charController.radius = 0.35f;
            charController.center = new Vector3(0f, 0.9f, 0f);

            player.AddComponent<SimpleTestPlayerController>();

            // Hook up auto-repositioning when new world snapshots arrive
            var sceneRenderer = Object.FindAnyObjectByType<AuthoritativeWorldSceneRenderer>();
            if (sceneRenderer != null)
            {
                sceneRenderer.WorldRendered += () => RepositionPlayerToSpawn(player);
            }

            return player;
        }

        public static void RepositionPlayerToSpawn(GameObject player)
        {
            if (player == null)
                return;

            var spawnPos = ResolveSpawnPosition(out var spawnRot);
            var charController = player.GetComponent<CharacterController>();
            if (charController != null)
            {
                charController.enabled = false;
                player.transform.SetPositionAndRotation(spawnPos, spawnRot);
                charController.enabled = true;
            }
            else
            {
                player.transform.SetPositionAndRotation(spawnPos, spawnRot);
            }
        }

        private static Vector3 ResolveSpawnPosition(out Quaternion rotation)
        {
            rotation = Quaternion.identity;

            var sceneRenderer = Object.FindAnyObjectByType<AuthoritativeWorldSceneRenderer>();
            if (sceneRenderer != null)
            {
                var pos = sceneRenderer.GetPlayerSpawnPosition();
                if (pos.sqrMagnitude > 0.001f)
                    return pos;
            }

            var marker = Object.FindAnyObjectByType<TestWorldSpawnMarker>();
            if (marker != null)
            {
                rotation = marker.SpawnRotation;
                return marker.SpawnPosition;
            }

            return new Vector3(10f, 5f, 10f);
        }
    }
}

