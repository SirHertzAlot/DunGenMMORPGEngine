using System;
using System.Collections.Generic;
using DunGen.Events;
using UnityEngine;

namespace DunGen.Networking
{
    /// <summary>
    /// Durable in-process view of authoritative session state for gameplay and UI.
    /// Stores the latest bootstrap snapshot, connection health, and a bounded timeline.
    /// </summary>
    public sealed class AuthoritativeSessionStateStore : MonoBehaviour
    {
        [SerializeField] private int maxTimelineEvents = 32;

        private readonly List<AuthoritativeTimelineEntry> _recentTimeline = new();
        private EventBus _eventBus;
        private bool _isSubscribed;

        public bool HasBootstrap { get; private set; }
        public bool HasWorld { get; private set; }
        public bool IsConnected { get; private set; }
        public string SessionId { get; private set; } = string.Empty;
        public string ExecutionId { get; private set; } = string.Empty;
        public bool HasWorldSnapshot { get; private set; }
        public int WorldSeed { get; private set; }
        public int WorldWidth { get; private set; }
        public int WorldHeight { get; private set; }
        public int WorldDungeonLevel { get; private set; }
        public int RoomCount { get; private set; }
        public int EnemyCount { get; private set; }
        public int LootCount { get; private set; }
        public string SnapshotUrl { get; private set; } = string.Empty;
        public string TimelineUrl { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public float LastBootstrapTimestamp { get; private set; }
        public float LastTimelineTimestamp { get; private set; }
        public float LastWorldTimestamp { get; private set; }

        public IReadOnlyList<AuthoritativeWorldRoomData> Rooms => _rooms;
        public IReadOnlyList<AuthoritativeWorldEnemyData> Enemies => _enemies;
        public IReadOnlyList<AuthoritativeWorldLootData> Loot => _loot;
        public bool HasTerrainMesh { get; private set; }
        public string TerrainMeshId { get; private set; } = string.Empty;
        public string TerrainAlgorithm { get; private set; } = string.Empty;
        public float TerrainWaterLevel { get; private set; }
        public float TerrainHeightScale { get; private set; }
        public float TerrainMinHeight { get; private set; }
        public float TerrainMaxHeight { get; private set; }
        public IReadOnlyList<AuthoritativeTerrainMeshVertexData> TerrainVertices => _terrainVertices;
        public IReadOnlyList<int> TerrainTriangles => _terrainTriangles;

        public IReadOnlyList<AuthoritativeTimelineEntry> RecentTimeline => _recentTimeline;

        private readonly List<AuthoritativeWorldRoomData> _rooms = new();
        private readonly List<AuthoritativeWorldEnemyData> _enemies = new();
        private readonly List<AuthoritativeWorldLootData> _loot = new();
        private readonly List<AuthoritativeTerrainMeshVertexData> _terrainVertices = new();
        private readonly List<int> _terrainTriangles = new();

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Subscribe()
        {
            if (_isSubscribed)
                return;

            _eventBus = EventBus.Instance;
            _eventBus.Subscribe<AuthoritativeBootstrapReceivedEventData>(HandleBootstrapReceived);
            _eventBus.Subscribe<AuthoritativeTimelineEventReceivedEventData>(HandleTimelineEventReceived);
            _eventBus.Subscribe<AuthoritativeWorldReceivedEventData>(HandleWorldReceived);
            _eventBus.Subscribe<AuthoritativeRequestFailedEventData>(HandleRequestFailed);
            _isSubscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_isSubscribed || _eventBus == null)
                return;

            _eventBus.Unsubscribe<AuthoritativeBootstrapReceivedEventData>(HandleBootstrapReceived);
            _eventBus.Unsubscribe<AuthoritativeTimelineEventReceivedEventData>(HandleTimelineEventReceived);
            _eventBus.Unsubscribe<AuthoritativeWorldReceivedEventData>(HandleWorldReceived);
            _eventBus.Unsubscribe<AuthoritativeRequestFailedEventData>(HandleRequestFailed);
            _isSubscribed = false;
        }

        private void HandleBootstrapReceived(AuthoritativeBootstrapReceivedEventData evt)
        {
            HasBootstrap = true;
            HasWorld = evt.HasWorld;
            IsConnected = true;
            SessionId = evt.SessionId ?? string.Empty;
            ExecutionId = evt.ExecutionId ?? string.Empty;
            RoomCount = evt.RoomCount;
            EnemyCount = evt.EnemyCount;
            LootCount = evt.LootCount;
            SnapshotUrl = evt.SnapshotUrl ?? string.Empty;
            TimelineUrl = evt.TimelineUrl ?? string.Empty;
            LastBootstrapTimestamp = evt.Timestamp;
            LastError = string.Empty;
        }

        private void HandleWorldReceived(AuthoritativeWorldReceivedEventData evt)
        {
            IsConnected = true;
            HasWorld = true;
            HasWorldSnapshot = true;
            SessionId = evt.SessionId ?? string.Empty;
            ExecutionId = evt.ExecutionId ?? string.Empty;
            WorldSeed = evt.Seed;
            WorldWidth = evt.Width;
            WorldHeight = evt.Height;
            WorldDungeonLevel = evt.DungeonLevel;
            RoomCount = evt.Rooms?.Length ?? 0;
            EnemyCount = evt.Enemies?.Length ?? 0;
            LootCount = evt.Loot?.Length ?? 0;
            LastWorldTimestamp = evt.Timestamp;
            LastError = string.Empty;

            ReplaceList(_rooms, evt.Rooms);
            ReplaceList(_enemies, evt.Enemies);
            ReplaceList(_loot, evt.Loot);
            ReplaceList(_terrainVertices, evt.TerrainMesh.Vertices);
            ReplaceList(_terrainTriangles, evt.TerrainMesh.Triangles);
            TerrainMeshId = evt.TerrainMesh.MeshId ?? string.Empty;
            TerrainAlgorithm = evt.TerrainMesh.Algorithm ?? string.Empty;
            TerrainWaterLevel = evt.TerrainMesh.WaterLevel;
            TerrainHeightScale = evt.TerrainMesh.HeightScale;
            TerrainMinHeight = evt.TerrainMesh.MinHeight;
            TerrainMaxHeight = evt.TerrainMesh.MaxHeight;
            HasTerrainMesh = _terrainVertices.Count > 0 && _terrainTriangles.Count > 0;
        }

        private void HandleTimelineEventReceived(AuthoritativeTimelineEventReceivedEventData evt)
        {
            IsConnected = true;
            if (!string.IsNullOrWhiteSpace(evt.SessionId))
                SessionId = evt.SessionId;

            LastTimelineTimestamp = evt.Timestamp;
            LastError = string.Empty;

            _recentTimeline.Add(new AuthoritativeTimelineEntry
            {
                RemoteEventId = evt.RemoteEventId ?? string.Empty,
                EventType = evt.RemoteEventType ?? string.Empty,
                Category = evt.Category ?? string.Empty,
                Frame = evt.RemoteFrame,
                EntityId = evt.EntityId ?? string.Empty,
                Message = evt.Message ?? string.Empty,
                TimestampUtc = evt.TimestampUtc ?? string.Empty,
            });

            var maxEvents = Mathf.Max(4, maxTimelineEvents);
            if (_recentTimeline.Count > maxEvents)
                _recentTimeline.RemoveRange(0, _recentTimeline.Count - maxEvents);
        }

        private void HandleRequestFailed(AuthoritativeRequestFailedEventData evt)
        {
            IsConnected = false;
            if (!string.IsNullOrWhiteSpace(evt.SessionId))
                SessionId = evt.SessionId;

            LastError = evt.ErrorMessage ?? string.Empty;
        }

        [Serializable]
        public struct AuthoritativeTimelineEntry
        {
            public string RemoteEventId;
            public string EventType;
            public string Category;
            public uint Frame;
            public string EntityId;
            public string Message;
            public string TimestampUtc;
        }

        private static void ReplaceList<T>(List<T> target, T[] source)
        {
            target.Clear();
            if (source == null || source.Length == 0)
                return;

            target.AddRange(source);
        }
    }
}