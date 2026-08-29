using System.Collections.Generic;
using UnityEngine;
using DunGen.Gameplay;

namespace DunGen.Runtime
{
    /// <summary>
    /// Instantiates a runtime dungeon in the Unity scene from an AuthoritativeWorldBlueprint or procedural data.
    /// Only instantiates when the core runtime (Simulation + GameSession) is active.
    /// </summary>
    public class RuntimeDungeonInstantiator : MonoBehaviour
    {
        [Header("Dungeon Prefabs")]
        public GameObject roomPrefab;
        public GameObject enemyPrefab;
        public GameObject lootPrefab;

        private bool _isDungeonActive = false;
        public bool IsDungeonActive => _isDungeonActive;

        /// <summary>Instantiate dungeon only if runtime systems are active.</summary>
        public bool TryInstantiateDungeon(AuthoritativeWorldBlueprint blueprint, bool isRuntimeActive)
        {
            // Guard: Don't instantiate if runtime isn't active
            if (!isRuntimeActive)
            {
                Debug.LogWarning("[DunGen] Cannot instantiate dungeon: runtime system is not active.");
                return false;
            }

            if (blueprint == null)
            {
                Debug.LogError("[DunGen] Missing blueprint for dungeon instantiation.");
                return false;
            }

            InstantiateDungeon(blueprint);
            _isDungeonActive = true;
            return true;
        }

        /// <summary>Instantiate the actual dungeon in the scene (called only when runtime is validated).</summary>
        private void InstantiateDungeon(AuthoritativeWorldBlueprint blueprint)
        {
            if (blueprint == null)
            {
                Debug.LogError("[DunGen] Missing blueprint for dungeon instantiation.");
                return;
            }

            // Instantiate rooms
            foreach (var room in blueprint.Rooms)
            {
                var roomGO = roomPrefab != null
                    ? Instantiate(roomPrefab, new Vector3(room.X, 0, room.Y), Quaternion.identity, transform)
                    : CreatePrimitiveMarker(PrimitiveType.Cube, new Vector3(room.X, 0, room.Y), new Vector3(room.Width, 0.1f, room.Height), Color.gray);
                roomGO.name = $"Room_{room.Id}";
                roomGO.transform.localScale = new Vector3(room.Width, 1, room.Height);
            }

            // Instantiate enemies
            if (enemyPrefab != null)
            {
                foreach (var enemy in blueprint.Enemies)
                {
                    var enemyGO = Instantiate(enemyPrefab, new Vector3(enemy.X, 0.5f, enemy.Y), Quaternion.identity, transform);
                    enemyGO.name = $"Enemy_{enemy.Id}";
                }
            }
            else
            {
                foreach (var enemy in blueprint.Enemies)
                {
                    var enemyGO = CreatePrimitiveMarker(PrimitiveType.Capsule, new Vector3(enemy.X, 0.6f, enemy.Y), new Vector3(0.6f, 1.2f, 0.6f), Color.red);
                    enemyGO.name = $"Enemy_{enemy.Id}";
                }
            }

            // Instantiate loot
            if (lootPrefab != null)
            {
                foreach (var loot in blueprint.Loot)
                {
                    var lootGO = Instantiate(lootPrefab, new Vector3(loot.X, 0.25f, loot.Y), Quaternion.identity, transform);
                    lootGO.name = $"Loot_{loot.ItemId}";
                }
            }
            else
            {
                foreach (var loot in blueprint.Loot)
                {
                    var lootGO = CreatePrimitiveMarker(PrimitiveType.Sphere, new Vector3(loot.X, 0.25f, loot.Y), new Vector3(0.35f, 0.35f, 0.35f), Color.yellow);
                    lootGO.name = $"Loot_{loot.ItemId}";
                }
            }

            Debug.Log("[DunGen] Dungeon instantiated at runtime.");
        }

        /// <summary>Clean up dungeon when runtime is no longer active.</summary>
        public void CleanupDungeon()
        {
            if (!_isDungeonActive)
                return;

            _isDungeonActive = false;
            
            // Destroy all child GameObjects (rooms, enemies, loot)
            var childCount = transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            Debug.Log("[DunGen] Dungeon cleaned up.");
        }

        private GameObject CreatePrimitiveMarker(PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var marker = GameObject.CreatePrimitive(type);
            marker.transform.SetParent(transform, false);
            marker.transform.position = position;
            marker.transform.localScale = scale;
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;

            return marker;
        }
    }
}