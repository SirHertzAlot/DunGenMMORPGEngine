using System;
using System.Collections.Generic;
using DunGen.Events;
using UnityEngine;

namespace DunGen.Networking
{
    /// <summary>
    /// Publishes authoritative backend session updates onto the local EventBus.
    /// This keeps gameplay systems decoupled from transport and DTO concerns.
    /// </summary>
    [RequireComponent(typeof(AuthoritativeSessionClient))]
    public sealed class AuthoritativeSessionEventBridge : MonoBehaviour
    {
        [SerializeField] private bool fetchBootstrapOnEnable = true;
        [SerializeField] private bool fetchTimelineOnEnable = true;

        private readonly HashSet<string> _publishedTimelineEventIds = new(StringComparer.Ordinal);
        private AuthoritativeSessionClient _client;
        private EventBus _eventBus;
        private string _activeSessionId = string.Empty;
        private string _activeExecutionId = string.Empty;

        private void OnEnable()
        {
            _client = GetComponent<AuthoritativeSessionClient>();
            _eventBus = EventBus.Instance;

            if (_client == null)
            {
                Debug.LogWarning("AuthoritativeSessionEventBridge requires AuthoritativeSessionClient.");
                enabled = false;
                return;
            }

            _client.BootstrapUpdated += HandleBootstrapUpdated;
            _client.TimelineUpdated += HandleTimelineUpdated;
            _client.WorldUpdated += HandleWorldUpdated;
            _client.RequestFailed += HandleRequestFailed;

            if (fetchBootstrapOnEnable)
                _client.RefreshBootstrap();

            if (fetchTimelineOnEnable)
                _client.RefreshTimeline();
        }

        private void OnDisable()
        {
            if (_client == null)
                return;

            _client.BootstrapUpdated -= HandleBootstrapUpdated;
            _client.TimelineUpdated -= HandleTimelineUpdated;
            _client.WorldUpdated -= HandleWorldUpdated;
            _client.RequestFailed -= HandleRequestFailed;
        }

        private void HandleBootstrapUpdated(UnitySessionBootstrapDto dto)
        {
            ResetTimelineCacheIfSessionChanged(dto.sessionId);

            var executionId = dto.executionId ?? string.Empty;
            if (dto.hasWorld && !string.Equals(_activeExecutionId, executionId, StringComparison.Ordinal))
            {
                _activeExecutionId = executionId;
                _client.RefreshWorld();
            }
            else if (!dto.hasWorld)
            {
                _activeExecutionId = string.Empty;
            }

            _eventBus.Publish(new AuthoritativeBootstrapReceivedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = unchecked((uint)Time.frameCount),
                Timestamp = Time.realtimeSinceStartup,
                SessionId = dto.sessionId ?? string.Empty,
                HasWorld = dto.hasWorld,
                ExecutionId = dto.executionId ?? string.Empty,
                RoomCount = dto.roomCount,
                EnemyCount = dto.enemyCount,
                LootCount = dto.lootCount,
                SnapshotUrl = dto.snapshotUrl ?? string.Empty,
                StreamUrl = dto.streamUrl ?? string.Empty,
                WebSocketUrl = dto.webSocketUrl ?? string.Empty,
                TimelineUrl = dto.timelineUrl ?? string.Empty,
            });
        }

        private void HandleWorldUpdated(UnitySessionWorldDto dto)
        {
            if (dto?.world == null)
                return;

            var rooms = dto.world.rooms ?? Array.Empty<UnityWorldRoomDto>();
            var enemies = dto.world.enemies ?? Array.Empty<UnityWorldEnemyDto>();
            var loot = dto.world.loot ?? Array.Empty<UnityWorldLootDto>();

            var roomData = new AuthoritativeWorldRoomData[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                roomData[i] = new AuthoritativeWorldRoomData
                {
                    Id = rooms[i].id,
                    X = rooms[i].x,
                    Y = rooms[i].y,
                    Width = rooms[i].width,
                    Height = rooms[i].height,
                };
            }

            var enemyData = new AuthoritativeWorldEnemyData[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                enemyData[i] = new AuthoritativeWorldEnemyData
                {
                    Id = enemies[i].id,
                    Archetype = enemies[i].archetype ?? string.Empty,
                    X = enemies[i].x,
                    Y = enemies[i].y,
                    Level = enemies[i].level,
                };
            }

            var lootData = new AuthoritativeWorldLootData[loot.Length];
            for (int i = 0; i < loot.Length; i++)
            {
                lootData[i] = new AuthoritativeWorldLootData
                {
                    ItemId = loot[i].itemId ?? string.Empty,
                    ItemType = loot[i].itemType ?? string.Empty,
                    Tier = loot[i].tier ?? string.Empty,
                    X = loot[i].x,
                    Y = loot[i].y,
                };
            }

            var terrainVertices = dto.world.terrainMesh?.vertices ?? Array.Empty<UnityTerrainMeshVertexDto>();
            var terrainMeshVertices = new AuthoritativeTerrainMeshVertexData[terrainVertices.Length];
            for (int i = 0; i < terrainVertices.Length; i++)
            {
                terrainMeshVertices[i] = new AuthoritativeTerrainMeshVertexData
                {
                    X = terrainVertices[i].x,
                    Y = terrainVertices[i].y,
                    Z = terrainVertices[i].z,
                    U = terrainVertices[i].u,
                    V = terrainVertices[i].v,
                    NormalX = terrainVertices[i].normalX,
                    NormalY = terrainVertices[i].normalY,
                    NormalZ = terrainVertices[i].normalZ,
                };
            }

            var terrainMeshData = new AuthoritativeTerrainMeshData
            {
                MeshId = dto.world.terrainMesh?.meshId ?? string.Empty,
                Width = dto.world.terrainMesh?.width ?? dto.world.width,
                Height = dto.world.terrainMesh?.height ?? dto.world.height,
                Seed = dto.world.terrainMesh?.seed ?? dto.world.seed,
                Algorithm = dto.world.terrainMesh?.algorithm ?? string.Empty,
                WaterLevel = dto.world.terrainMesh?.waterLevel ?? 0f,
                HeightScale = dto.world.terrainMesh?.heightScale ?? 0f,
                MinHeight = dto.world.terrainMesh?.minHeight ?? 0f,
                MaxHeight = dto.world.terrainMesh?.maxHeight ?? 0f,
                Vertices = terrainMeshVertices,
                Triangles = dto.world.terrainMesh?.triangles ?? Array.Empty<int>(),
            };

            _eventBus.Publish(new AuthoritativeWorldReceivedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = unchecked((uint)Time.frameCount),
                Timestamp = Time.realtimeSinceStartup,
                SessionId = dto.sessionId ?? string.Empty,
                ExecutionId = dto.executionId ?? string.Empty,
                Seed = dto.world.seed,
                Width = dto.world.width,
                Height = dto.world.height,
                DungeonLevel = dto.world.dungeonLevel,
                Rooms = roomData,
                Enemies = enemyData,
                Loot = lootData,
                TerrainMesh = terrainMeshData,
            });
        }

        private void HandleTimelineUpdated(UnitySessionTimelineDto dto)
        {
            ResetTimelineCacheIfSessionChanged(dto.sessionId);

            if (dto.events == null)
                return;

            for (int i = 0; i < dto.events.Length; i++)
            {
                var evt = dto.events[i];
                var remoteEventId = evt.eventId ?? string.Empty;
                if (!string.IsNullOrEmpty(remoteEventId) && !_publishedTimelineEventIds.Add(remoteEventId))
                    continue;

                _eventBus.Publish(new AuthoritativeTimelineEventReceivedEventData
                {
                    EventId = _eventBus.GetNextEventId(),
                    FrameNumber = evt.frame,
                    Timestamp = Time.realtimeSinceStartup,
                    SessionId = dto.sessionId ?? string.Empty,
                    RemoteEventId = remoteEventId,
                    RemoteEventType = evt.eventType ?? string.Empty,
                    Category = evt.category ?? string.Empty,
                    RemoteFrame = evt.frame,
                    EntityId = evt.entityId ?? string.Empty,
                    Message = evt.message ?? string.Empty,
                    TimestampUtc = evt.timestampUtc ?? string.Empty,
                });
            }
        }

        private void HandleRequestFailed(string error)
        {
            _eventBus.Publish(new AuthoritativeRequestFailedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = unchecked((uint)Time.frameCount),
                Timestamp = Time.realtimeSinceStartup,
                SessionId = _activeSessionId,
                ErrorMessage = error ?? string.Empty,
            });
        }

        private void ResetTimelineCacheIfSessionChanged(string sessionId)
        {
            var normalizedSessionId = string.IsNullOrWhiteSpace(sessionId) ? string.Empty : sessionId.Trim();
            if (string.Equals(_activeSessionId, normalizedSessionId, StringComparison.Ordinal))
                return;

            _activeSessionId = normalizedSessionId;
            _publishedTimelineEventIds.Clear();
        }
    }
}