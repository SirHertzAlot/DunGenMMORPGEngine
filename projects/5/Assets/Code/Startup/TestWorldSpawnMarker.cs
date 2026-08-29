using UnityEngine;

namespace DunGen.Startup
{
    /// <summary>
    /// Place this in a scene to control where the runtime test player spawns.
    /// </summary>
    public sealed class TestWorldSpawnMarker : MonoBehaviour
    {
        [SerializeField] private Vector3 spawnOffset = Vector3.up;

        public Vector3 SpawnPosition => transform.position + spawnOffset;
        public Quaternion SpawnRotation => transform.rotation;
    }
}
