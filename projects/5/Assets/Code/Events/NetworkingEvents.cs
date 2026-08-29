namespace DunGen.Events
{
    /// <summary>Event: authoritative session bootstrap snapshot received.</summary>
    public struct AuthoritativeBootstrapReceivedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public string SessionId;
        public bool HasWorld;
        public string ExecutionId;
        public int RoomCount;
        public int EnemyCount;
        public int LootCount;
        public string SnapshotUrl;
        public string StreamUrl;
        public string WebSocketUrl;
        public string TimelineUrl;
    }

    /// <summary>Event: authoritative timeline entry received.</summary>
    public struct AuthoritativeTimelineEventReceivedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public string SessionId;
        public string RemoteEventId;
        public string RemoteEventType;
        public string Category;
        public uint RemoteFrame;
        public string EntityId;
        public string Message;
        public string TimestampUtc;
    }

    /// <summary>Event: authoritative world payload received.</summary>
    public struct AuthoritativeWorldReceivedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public string SessionId;
        public string ExecutionId;
        public int Seed;
        public int Width;
        public int Height;
        public int DungeonLevel;
        public AuthoritativeWorldRoomData[] Rooms;
        public AuthoritativeWorldEnemyData[] Enemies;
        public AuthoritativeWorldLootData[] Loot;
        public AuthoritativeTerrainMeshData TerrainMesh;
    }

    /// <summary>Event: authoritative session request failed.</summary>
    public struct AuthoritativeRequestFailedEventData
    {
        public ulong EventId;
        public uint FrameNumber;
        public float Timestamp;
        public string SessionId;
        public string ErrorMessage;
    }

    public struct AuthoritativeWorldRoomData
    {
        public int Id;
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    public struct AuthoritativeWorldEnemyData
    {
        public int Id;
        public string Archetype;
        public int X;
        public int Y;
        public int Level;
    }

    public struct AuthoritativeWorldLootData
    {
        public string ItemId;
        public string ItemType;
        public string Tier;
        public int X;
        public int Y;
    }

    public struct AuthoritativeTerrainMeshVertexData
    {
        public float X;
        public float Y;
        public float Z;
        public float U;
        public float V;
        public float NormalX;
        public float NormalY;
        public float NormalZ;
    }

    public struct AuthoritativeTerrainMeshData
    {
        public string MeshId;
        public int Width;
        public int Height;
        public int Seed;
        public string Algorithm;
        public float WaterLevel;
        public float HeightScale;
        public float MinHeight;
        public float MaxHeight;
        public AuthoritativeTerrainMeshVertexData[] Vertices;
        public int[] Triangles;
    }
}