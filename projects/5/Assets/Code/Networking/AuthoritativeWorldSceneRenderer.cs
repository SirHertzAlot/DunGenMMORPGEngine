using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DunGen.Networking
{
    [RequireComponent(typeof(AuthoritativeSessionStateStore))]
    public sealed class AuthoritativeWorldSceneRenderer : MonoBehaviour
    {
        [SerializeField] private Material terrainMaterialTemplate;
        [SerializeField] private Material roomMaterialTemplate;
        [SerializeField] private Material enemyMaterialTemplate;
        [SerializeField] private Material lootMaterialTemplate;

        private AuthoritativeSessionStateStore _stateStore;
        private string _lastRenderToken = string.Empty;
        private GameObject _root;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private readonly List<GameObject> _markers = new();

        public event System.Action WorldRendered;

        private void Awake()
        {
            _stateStore = GetComponent<AuthoritativeSessionStateStore>();
        }

        private void LateUpdate()
        {
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (_stateStore == null)
            {
                _stateStore = GetComponent<AuthoritativeSessionStateStore>();
                if (_stateStore == null)
                    return;
            }

            if (!_stateStore.HasWorldSnapshot)
                return;

            var renderToken = $"{_stateStore.ExecutionId}|{_stateStore.TerrainMeshId}|{_stateStore.RoomCount}|{_stateStore.EnemyCount}|{_stateStore.LootCount}";
            if (renderToken == _lastRenderToken)
                return;

            EnsureRoot();
            RebuildTerrain();
            RebuildMarkers();
            _lastRenderToken = renderToken;
            WorldRendered?.Invoke();
        }

        private void OnDisable()
        {
            if (_root != null)
            {
                Destroy(_root);
                _root = null;
                _meshFilter = null;
                _meshRenderer = null;
                _meshCollider = null;
            }
        }

        private void EnsureRoot()
        {
            if (_root != null)
                return;

            _root = new GameObject("Authoritative World");
            _root.transform.SetParent(transform, false);

            var terrainObject = new GameObject("Terrain Mesh");
            terrainObject.transform.SetParent(_root.transform, false);
            _meshFilter = terrainObject.AddComponent<MeshFilter>();
            _meshRenderer = terrainObject.AddComponent<MeshRenderer>();
            _meshCollider = terrainObject.AddComponent<MeshCollider>();
        }

        private void RebuildTerrain()
        {
            var mesh = new Mesh
            {
                name = string.IsNullOrWhiteSpace(_stateStore.TerrainMeshId) ? "AuthoritativeTerrain" : _stateStore.TerrainMeshId,
                indexFormat = _stateStore.TerrainVertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };

            if (_stateStore.HasTerrainMesh)
            {
                var vertices = new Vector3[_stateStore.TerrainVertices.Count];
                var normals = new Vector3[_stateStore.TerrainVertices.Count];
                var uvs = new Vector2[_stateStore.TerrainVertices.Count];
                for (int i = 0; i < _stateStore.TerrainVertices.Count; i++)
                {
                    var vertex = _stateStore.TerrainVertices[i];
                    vertices[i] = new Vector3(vertex.X, vertex.Y, vertex.Z);
                    normals[i] = new Vector3(vertex.NormalX, vertex.NormalY, vertex.NormalZ);
                    uvs[i] = new Vector2(vertex.U, vertex.V);
                }

                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.triangles = BuildTriangleArray();
            }
            else
            {
                mesh.vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(_stateStore.WorldWidth - 1, 0f, 0f),
                    new Vector3(0f, 0f, _stateStore.WorldHeight - 1),
                    new Vector3(_stateStore.WorldWidth - 1, 0f, _stateStore.WorldHeight - 1),
                };
                mesh.uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                };
                mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            if (_meshFilter.sharedMesh != null)
            {
                Destroy(_meshFilter.sharedMesh);
            }

            _meshFilter.sharedMesh = mesh;
            _meshRenderer.sharedMaterial = GetOrCreateMaterial(terrainMaterialTemplate, new Color(0.33f, 0.55f, 0.39f, 1f));

            if (_meshCollider != null)
            {
                _meshCollider.sharedMesh = null;
                _meshCollider.sharedMesh = mesh;
            }
        }

        private void RebuildMarkers()
        {
            for (int i = 0; i < _markers.Count; i++)
            {
                if (_markers[i] != null)
                {
                    Destroy(_markers[i]);
                }
            }

            _markers.Clear();

            for (int i = 0; i < _stateStore.Rooms.Count; i++)
            {
                var room = _stateStore.Rooms[i];
                var roomMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roomMarker.name = $"Room {room.Id}";
                roomMarker.transform.SetParent(_root.transform, false);
                roomMarker.transform.position = new Vector3(room.X + (room.Width * 0.5f), 0.15f, room.Y + (room.Height * 0.5f));
                roomMarker.transform.localScale = new Vector3(Mathf.Max(1f, room.Width), 0.15f, Mathf.Max(1f, room.Height));
                ConfigureMarker(roomMarker, roomMaterialTemplate, new Color(0.89f, 0.78f, 0.43f, 1f));
            }

            for (int i = 0; i < _stateStore.Enemies.Count; i++)
            {
                var enemy = _stateStore.Enemies[i];
                var enemyMarker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                enemyMarker.name = $"Enemy {enemy.Id}";
                enemyMarker.transform.SetParent(_root.transform, false);
                enemyMarker.transform.position = new Vector3(enemy.X, SampleHeight(enemy.X, enemy.Y) + 0.9f, enemy.Y);
                enemyMarker.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
                ConfigureMarker(enemyMarker, enemyMaterialTemplate, new Color(0.72f, 0.26f, 0.23f, 1f));
            }

            for (int i = 0; i < _stateStore.Loot.Count; i++)
            {
                var loot = _stateStore.Loot[i];
                var lootMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lootMarker.name = $"Loot {loot.ItemId}";
                lootMarker.transform.SetParent(_root.transform, false);
                lootMarker.transform.position = new Vector3(loot.X, SampleHeight(loot.X, loot.Y) + 0.35f, loot.Y);
                lootMarker.transform.localScale = Vector3.one * 0.35f;
                ConfigureMarker(lootMarker, lootMaterialTemplate, new Color(0.21f, 0.60f, 0.70f, 1f));
            }
        }

        private int[] BuildTriangleArray()
        {
            var triangles = new int[_stateStore.TerrainTriangles.Count];
            for (int i = 0; i < _stateStore.TerrainTriangles.Count; i++)
            {
                triangles[i] = _stateStore.TerrainTriangles[i];
            }

            return triangles;
        }

        private void ConfigureMarker(GameObject marker, Material template, Color fallbackColor)
        {
            _markers.Add(marker);
            if (marker.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            if (marker.TryGetComponent<MeshRenderer>(out var renderer))
            {
                renderer.sharedMaterial = GetOrCreateMaterial(template, fallbackColor);
            }
        }

        private Material GetOrCreateMaterial(Material template, Color fallbackColor)
        {
            if (template != null)
                return template;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", fallbackColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.color = fallbackColor;
            }

            return material;
        }

        public float SampleHeight(float worldX, float worldZ)
        {
            return SampleHeight(Mathf.RoundToInt(worldX), Mathf.RoundToInt(worldZ));
        }

        public Vector3 GetPlayerSpawnPosition()
        {
            if (_stateStore != null && _stateStore.Rooms.Count > 0)
            {
                var room = _stateStore.Rooms[0];
                float spawnX = room.X + (room.Width * 0.5f);
                float spawnZ = room.Y + (room.Height * 0.5f);
                float spawnY = SampleHeight(spawnX, spawnZ) + 1.2f;
                return new Vector3(spawnX, spawnY, spawnZ);
            }

            if (_stateStore != null && _stateStore.WorldWidth > 0 && _stateStore.WorldHeight > 0)
            {
                float spawnX = _stateStore.WorldWidth * 0.5f;
                float spawnZ = _stateStore.WorldHeight * 0.5f;
                float spawnY = SampleHeight(spawnX, spawnZ) + 1.2f;
                return new Vector3(spawnX, spawnY, spawnZ);
            }

            return new Vector3(0f, 2f, 0f);
        }

        public float SampleHeight(int x, int y)
        {
            if (_stateStore != null && _stateStore.HasTerrainMesh && _stateStore.TerrainVertices.Count == _stateStore.WorldWidth * _stateStore.WorldHeight)
            {
                int clampedX = Mathf.Clamp(x, 0, _stateStore.WorldWidth - 1);
                int clampedY = Mathf.Clamp(y, 0, _stateStore.WorldHeight - 1);
                return _stateStore.TerrainVertices[(clampedY * _stateStore.WorldWidth) + clampedX].Y;
            }

            return 0f;
        }
    }
}
