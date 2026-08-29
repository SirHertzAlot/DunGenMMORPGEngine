using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using DunGen.Events;

namespace DunGen.Networking
{
    /// <summary>
    /// High-performance compact binary codec for authoritative world snapshots and entity states.
    /// Replaces bulky JSON strings with binary payloads for reduced bandwidth and allocation.
    /// </summary>
    public static class BinaryWorldSnapshotCodec
    {
        public const uint WorldMagic = 0x53574744;  // 'DGWS' in little endian (0x44, 0x47, 0x57, 0x53)
        public const uint EntityMagic = 0x53454744; // 'DGES' in little endian (0x44, 0x47, 0x45, 0x53)
        public const ushort CurrentVersion = 1;

        public struct BinaryEntityState
        {
            public int EntityId;
            public byte EntityType; // 0=Player, 1=Enemy, 2=NPC, 3=Loot
            public int X;
            public int Y;
            public int Health;
            public int MaxHealth;
            public byte Flags; // bit 0: InCombat, bit 1: IsDead
        }

        public struct BinaryEntitySnapshotBatch
        {
            public uint FrameNumber;
            public float Timestamp;
            public BinaryEntityState[] Entities;
        }

        #region World Snapshot Serialization

        public static byte[] SerializeWorld(AuthoritativeWorldReceivedEventData world)
        {
            using (var ms = new MemoryStream(4096))
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                writer.Write(WorldMagic);
                writer.Write(CurrentVersion);
                writer.Write((ushort)0); // Flags

                writer.Write(world.SessionId ?? string.Empty);
                writer.Write(world.ExecutionId ?? string.Empty);
                writer.Write(world.Seed);
                writer.Write(world.Width);
                writer.Write(world.Height);
                writer.Write(world.DungeonLevel);

                // Rooms
                var rooms = world.Rooms ?? Array.Empty<AuthoritativeWorldRoomData>();
                writer.Write(rooms.Length);
                for (int i = 0; i < rooms.Length; i++)
                {
                    writer.Write(rooms[i].Id);
                    writer.Write(rooms[i].X);
                    writer.Write(rooms[i].Y);
                    writer.Write(rooms[i].Width);
                    writer.Write(rooms[i].Height);
                }

                // Enemies
                var enemies = world.Enemies ?? Array.Empty<AuthoritativeWorldEnemyData>();
                writer.Write(enemies.Length);
                for (int i = 0; i < enemies.Length; i++)
                {
                    writer.Write(enemies[i].Id);
                    writer.Write(enemies[i].Archetype ?? string.Empty);
                    writer.Write(enemies[i].X);
                    writer.Write(enemies[i].Y);
                    writer.Write(enemies[i].Level);
                }

                // Loot
                var loot = world.Loot ?? Array.Empty<AuthoritativeWorldLootData>();
                writer.Write(loot.Length);
                for (int i = 0; i < loot.Length; i++)
                {
                    writer.Write(loot[i].ItemId ?? string.Empty);
                    writer.Write(loot[i].ItemType ?? string.Empty);
                    writer.Write(loot[i].Tier ?? string.Empty);
                    writer.Write(loot[i].X);
                    writer.Write(loot[i].Y);
                }

                // Terrain Mesh
                bool hasTerrain = world.TerrainMesh.Vertices != null && world.TerrainMesh.Vertices.Length > 0;
                writer.Write(hasTerrain);
                if (hasTerrain)
                {
                    writer.Write(world.TerrainMesh.MeshId ?? string.Empty);
                    writer.Write(world.TerrainMesh.Width);
                    writer.Write(world.TerrainMesh.Height);
                    writer.Write(world.TerrainMesh.Seed);
                    writer.Write(world.TerrainMesh.Algorithm ?? string.Empty);
                    writer.Write(world.TerrainMesh.WaterLevel);
                    writer.Write(world.TerrainMesh.HeightScale);
                    writer.Write(world.TerrainMesh.MinHeight);
                    writer.Write(world.TerrainMesh.MaxHeight);

                    var verts = world.TerrainMesh.Vertices;
                    writer.Write(verts.Length);
                    for (int i = 0; i < verts.Length; i++)
                    {
                        writer.Write(verts[i].X);
                        writer.Write(verts[i].Y);
                        writer.Write(verts[i].Z);
                        writer.Write(verts[i].U);
                        writer.Write(verts[i].V);
                        writer.Write(verts[i].NormalX);
                        writer.Write(verts[i].NormalY);
                        writer.Write(verts[i].NormalZ);
                    }

                    var tris = world.TerrainMesh.Triangles ?? Array.Empty<int>();
                    writer.Write(tris.Length);
                    for (int i = 0; i < tris.Length; i++)
                    {
                        writer.Write(tris[i]);
                    }
                }

                return ms.ToArray();
            }
        }

        public static bool TryDeserializeWorld(byte[] data, out AuthoritativeWorldReceivedEventData world, out string error)
        {
            world = default;
            error = null;

            if (data == null || data.Length < 12)
            {
                error = "Data payload too short for binary world header.";
                return false;
            }

            try
            {
                using (var ms = new MemoryStream(data))
                using (var reader = new BinaryReader(ms, Encoding.UTF8))
                {
                    var magic = reader.ReadUInt32();
                    if (magic != WorldMagic)
                    {
                        error = $"Invalid magic signature: 0x{magic:X8}";
                        return false;
                    }

                    var version = reader.ReadUInt16();
                    if (version > CurrentVersion)
                    {
                        error = $"Unsupported protocol version: {version}";
                        return false;
                    }

                    var flags = reader.ReadUInt16();
                    var sessionId = reader.ReadString();
                    var executionId = reader.ReadString();
                    var seed = reader.ReadInt32();
                    var width = reader.ReadInt32();
                    var height = reader.ReadInt32();
                    var dungeonLevel = reader.ReadInt32();

                    // Rooms
                    var roomCount = reader.ReadInt32();
                    var rooms = new AuthoritativeWorldRoomData[roomCount];
                    for (int i = 0; i < roomCount; i++)
                    {
                        rooms[i] = new AuthoritativeWorldRoomData
                        {
                            Id = reader.ReadInt32(),
                            X = reader.ReadInt32(),
                            Y = reader.ReadInt32(),
                            Width = reader.ReadInt32(),
                            Height = reader.ReadInt32()
                        };
                    }

                    // Enemies
                    var enemyCount = reader.ReadInt32();
                    var enemies = new AuthoritativeWorldEnemyData[enemyCount];
                    for (int i = 0; i < enemyCount; i++)
                    {
                        enemies[i] = new AuthoritativeWorldEnemyData
                        {
                            Id = reader.ReadInt32(),
                            Archetype = reader.ReadString(),
                            X = reader.ReadInt32(),
                            Y = reader.ReadInt32(),
                            Level = reader.ReadInt32()
                        };
                    }

                    // Loot
                    var lootCount = reader.ReadInt32();
                    var loot = new AuthoritativeWorldLootData[lootCount];
                    for (int i = 0; i < lootCount; i++)
                    {
                        loot[i] = new AuthoritativeWorldLootData
                        {
                            ItemId = reader.ReadString(),
                            ItemType = reader.ReadString(),
                            Tier = reader.ReadString(),
                            X = reader.ReadInt32(),
                            Y = reader.ReadInt32()
                        };
                    }

                    // Terrain
                    var terrainMeshData = new AuthoritativeTerrainMeshData();
                    bool hasTerrain = reader.ReadBoolean();
                    if (hasTerrain)
                    {
                        terrainMeshData.MeshId = reader.ReadString();
                        terrainMeshData.Width = reader.ReadInt32();
                        terrainMeshData.Height = reader.ReadInt32();
                        terrainMeshData.Seed = reader.ReadInt32();
                        terrainMeshData.Algorithm = reader.ReadString();
                        terrainMeshData.WaterLevel = reader.ReadSingle();
                        terrainMeshData.HeightScale = reader.ReadSingle();
                        terrainMeshData.MinHeight = reader.ReadSingle();
                        terrainMeshData.MaxHeight = reader.ReadSingle();

                        var vertCount = reader.ReadInt32();
                        var verts = new AuthoritativeTerrainMeshVertexData[vertCount];
                        for (int i = 0; i < vertCount; i++)
                        {
                            verts[i] = new AuthoritativeTerrainMeshVertexData
                            {
                                X = reader.ReadSingle(),
                                Y = reader.ReadSingle(),
                                Z = reader.ReadSingle(),
                                U = reader.ReadSingle(),
                                V = reader.ReadSingle(),
                                NormalX = reader.ReadSingle(),
                                NormalY = reader.ReadSingle(),
                                NormalZ = reader.ReadSingle()
                            };
                        }
                        terrainMeshData.Vertices = verts;

                        var triCount = reader.ReadInt32();
                        var tris = new int[triCount];
                        for (int i = 0; i < triCount; i++)
                        {
                            tris[i] = reader.ReadInt32();
                        }
                        terrainMeshData.Triangles = tris;
                    }

                    world = new AuthoritativeWorldReceivedEventData
                    {
                        SessionId = sessionId,
                        ExecutionId = executionId,
                        Seed = seed,
                        Width = width,
                        Height = height,
                        DungeonLevel = dungeonLevel,
                        Rooms = rooms,
                        Enemies = enemies,
                        Loot = loot,
                        TerrainMesh = terrainMeshData
                    };

                    return true;
                }
            }
            catch (Exception ex)
            {
                error = $"Deserialization error: {ex.Message}";
                return false;
            }
        }

        #endregion

        #region Entity Snapshot Batch Serialization

        public static byte[] SerializeEntityBatch(uint frameNumber, float timestamp, IReadOnlyList<BinaryEntityState> entities)
        {
            using (var ms = new MemoryStream(256))
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                writer.Write(EntityMagic);
                writer.Write(CurrentVersion);
                writer.Write(frameNumber);
                writer.Write(timestamp);

                int count = entities?.Count ?? 0;
                writer.Write(count);
                for (int i = 0; i < count; i++)
                {
                    var e = entities[i];
                    writer.Write(e.EntityId);
                    writer.Write(e.EntityType);
                    writer.Write(e.X);
                    writer.Write(e.Y);
                    writer.Write(e.Health);
                    writer.Write(e.MaxHealth);
                    writer.Write(e.Flags);
                }

                return ms.ToArray();
            }
        }

        public static bool TryDeserializeEntityBatch(byte[] data, out BinaryEntitySnapshotBatch batch, out string error)
        {
            batch = default;
            error = null;

            if (data == null || data.Length < 14)
            {
                error = "Payload too short for entity batch header.";
                return false;
            }

            try
            {
                using (var ms = new MemoryStream(data))
                using (var reader = new BinaryReader(ms, Encoding.UTF8))
                {
                    var magic = reader.ReadUInt32();
                    if (magic != EntityMagic)
                    {
                        error = $"Invalid magic signature: 0x{magic:X8}";
                        return false;
                    }

                    var version = reader.ReadUInt16();
                    if (version > CurrentVersion)
                    {
                        error = $"Unsupported version: {version}";
                        return false;
                    }

                    var frame = reader.ReadUInt32();
                    var time = reader.ReadSingle();
                    var count = reader.ReadInt32();

                    var entities = new BinaryEntityState[count];
                    for (int i = 0; i < count; i++)
                    {
                        entities[i] = new BinaryEntityState
                        {
                            EntityId = reader.ReadInt32(),
                            EntityType = reader.ReadByte(),
                            X = reader.ReadInt32(),
                            Y = reader.ReadInt32(),
                            Health = reader.ReadInt32(),
                            MaxHealth = reader.ReadInt32(),
                            Flags = reader.ReadByte()
                        };
                    }

                    batch = new BinaryEntitySnapshotBatch
                    {
                        FrameNumber = frame,
                        Timestamp = time,
                        Entities = entities
                    };

                    return true;
                }
            }
            catch (Exception ex)
            {
                error = $"Entity batch deserialization error: {ex.Message}";
                return false;
            }
        }

        #endregion
    }
}
