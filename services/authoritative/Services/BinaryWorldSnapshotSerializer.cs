#if !UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Authoritative.Services
{
    /// <summary>
    /// Binary serializer on the authoritative ASP.NET Core backend.
    /// Emits byte streams matching the Unity client's BinaryWorldSnapshotCodec ('DGWS' format).
    /// </summary>
    public static class BinaryWorldSnapshotSerializer
    {
        public const uint WorldMagic = 0x53574744;  // 'DGWS'
        public const ushort CurrentVersion = 1;

        public static byte[] SerializeWorldArtifact(string sessionId, string executionId, GeneratedWorldArtifact world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            using (var ms = new MemoryStream(4096))
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                writer.Write(WorldMagic);
                writer.Write(CurrentVersion);
                writer.Write((ushort)0); // Flags

                writer.Write(sessionId ?? string.Empty);
                writer.Write(executionId ?? string.Empty);
                writer.Write(world.Seed);
                writer.Write(world.Width);
                writer.Write(world.Height);
                writer.Write(world.DungeonLevel);

                // Rooms
                var rooms = world.Rooms ?? new List<WorldRoom>();
                writer.Write(rooms.Count);
                for (int i = 0; i < rooms.Count; i++)
                {
                    writer.Write(rooms[i].Id);
                    writer.Write(rooms[i].X);
                    writer.Write(rooms[i].Y);
                    writer.Write(rooms[i].Width);
                    writer.Write(rooms[i].Height);
                }

                // Enemies
                var enemies = world.Enemies ?? new List<WorldEnemy>();
                writer.Write(enemies.Count);
                for (int i = 0; i < enemies.Count; i++)
                {
                    writer.Write(enemies[i].Id);
                    writer.Write(enemies[i].Archetype ?? string.Empty);
                    writer.Write(enemies[i].X);
                    writer.Write(enemies[i].Y);
                    writer.Write(enemies[i].Level);
                }

                // Loot
                var loot = world.Loot ?? new List<WorldLoot>();
                writer.Write(loot.Count);
                for (int i = 0; i < loot.Count; i++)
                {
                    writer.Write(loot[i].ItemId ?? string.Empty);
                    writer.Write(loot[i].ItemType ?? string.Empty);
                    writer.Write(loot[i].Tier ?? string.Empty);
                    writer.Write(loot[i].X);
                    writer.Write(loot[i].Y);
                }

                // Terrain Mesh
                bool hasTerrain = world.TerrainMesh?.Vertices != null && world.TerrainMesh.Vertices.Length > 0;
                writer.Write(hasTerrain);
                if (hasTerrain)
                {
                    writer.Write(world.TerrainMesh!.MeshId ?? string.Empty);
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
    }
}
#endif
