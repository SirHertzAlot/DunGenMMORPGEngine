#if !UNITY_5_3_OR_NEWER
using System;
using Authoritative.Multiplayer;

namespace Authoritative.Domain
{
    public sealed class HeightmapRequest
    {
        public int Width { get; set; } = 64;
        public int Height { get; set; } = 64;
        public int? Seed { get; set; }
        public float WaterLevel { get; set; } = 0.35f;
        public string Algorithm { get; set; } = "diamond-square";
        public float Roughness { get; set; } = 0.55f;
        public int Octaves { get; set; } = 4;
    }

    public sealed class HeightmapBiomes
    {
        public int WaterTiles { get; set; }
        public int LandTiles { get; set; }
        public int MountainTiles { get; set; }
        public float WaterPercent { get; set; }
        public float LandPercent { get; set; }
        public float MountainPercent { get; set; }
    }

    public sealed class TerrainMeshVertex
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float U { get; set; }
        public float V { get; set; }
        public float NormalX { get; set; }
        public float NormalY { get; set; }
        public float NormalZ { get; set; }
    }

    public sealed class GeneratedTerrainMesh
    {
        public string MeshId { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int Seed { get; set; }
        public string Algorithm { get; set; } = string.Empty;
        public float WaterLevel { get; set; }
        public float HeightScale { get; set; }
        public float MinHeight { get; set; }
        public float MaxHeight { get; set; }
        public TerrainMeshVertex[] Vertices { get; set; } = Array.Empty<TerrainMeshVertex>();
        public int[] Triangles { get; set; } = Array.Empty<int>();
        public HeightmapBiomes Biomes { get; set; } = new();
    }

    public sealed class HeightmapGenerator
    {
        private const int DefaultBaseSeed = 1337;

        public GeneratedTerrainMesh Generate(HeightmapRequest request)
        {
            int w     = Math.Clamp(request.Width,  8, 512);
            int h     = Math.Clamp(request.Height, 8, 512);
            int seed  = request.Seed ?? DefaultBaseSeed;
            float wl  = Math.Clamp(request.WaterLevel, 0f, 1f);
            float rough = Math.Clamp(request.Roughness, 0.1f, 1f);
            var algo  = (request.Algorithm ?? "diamond-square").Trim().ToLowerInvariant();
            const float heightScale = 24f;

            float[,] raw = algo == "perlin"
                ? GeneratePerlinLike(w, h, seed, Math.Clamp(request.Octaves, 1, 8), rough)
                : GenerateDiamondSquare(w, h, seed, rough);

            Normalize(raw, w, h, out float minV, out float maxV);

            int water = 0, land = 0, mountain = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float v = raw[x, y];
                    if      (v < wl)    water++;
                    else if (v > 0.75f) mountain++;
                    else                land++;
                }
            }

            var vertices = BuildVertices(raw, w, h, heightScale);
            var triangles = BuildTriangles(w, h);
            ApplyNormals(vertices, triangles);

            int total = w * h;
            return new GeneratedTerrainMesh
            {
                MeshId      = $"terrain_{seed}",
                Width       = w,
                Height      = h,
                Seed        = seed,
                Algorithm   = algo,
                WaterLevel  = wl,
                HeightScale = heightScale,
                MinHeight   = MathF.Round(minV * heightScale, 4),
                MaxHeight   = MathF.Round(maxV * heightScale, 4),
                Vertices    = vertices,
                Triangles   = triangles,
                Biomes      = new HeightmapBiomes
                {
                    WaterTiles      = water,
                    LandTiles       = land,
                    MountainTiles   = mountain,
                    WaterPercent    = MathF.Round((float)water    / total * 100, 1),
                    LandPercent     = MathF.Round((float)land     / total * 100, 1),
                    MountainPercent = MathF.Round((float)mountain / total * 100, 1)
                }
            };
        }

        private static TerrainMeshVertex[] BuildVertices(float[,] raw, int w, int h, float heightScale)
        {
            var vertices = new TerrainMeshVertex[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var index = y * w + x;
                    vertices[index] = new TerrainMeshVertex
                    {
                        X = x,
                        Y = MathF.Round(raw[x, y] * heightScale, 4),
                        Z = y,
                        U = w == 1 ? 0f : MathF.Round((float)x / (w - 1), 4),
                        V = h == 1 ? 0f : MathF.Round((float)y / (h - 1), 4),
                        NormalX = 0f,
                        NormalY = 1f,
                        NormalZ = 0f,
                    };
                }
            }

            return vertices;
        }

        private static int[] BuildTriangles(int w, int h)
        {
            var triangles = new int[(w - 1) * (h - 1) * 6];
            var offset = 0;
            for (int y = 0; y < h - 1; y++)
            {
                for (int x = 0; x < w - 1; x++)
                {
                    var topLeft = y * w + x;
                    var topRight = topLeft + 1;
                    var bottomLeft = topLeft + w;
                    var bottomRight = bottomLeft + 1;

                    triangles[offset++] = topLeft;
                    triangles[offset++] = bottomLeft;
                    triangles[offset++] = topRight;
                    triangles[offset++] = topRight;
                    triangles[offset++] = bottomLeft;
                    triangles[offset++] = bottomRight;
                }
            }

            return triangles;
        }

        private static void ApplyNormals(TerrainMeshVertex[] vertices, int[] triangles)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                ref var a = ref vertices[triangles[i]];
                ref var b = ref vertices[triangles[i + 1]];
                ref var c = ref vertices[triangles[i + 2]];

                var abX = b.X - a.X;
                var abY = b.Y - a.Y;
                var abZ = b.Z - a.Z;
                var acX = c.X - a.X;
                var acY = c.Y - a.Y;
                var acZ = c.Z - a.Z;

                var normalX = abY * acZ - abZ * acY;
                var normalY = abZ * acX - abX * acZ;
                var normalZ = abX * acY - abY * acX;

                a.NormalX += normalX; a.NormalY += normalY; a.NormalZ += normalZ;
                b.NormalX += normalX; b.NormalY += normalY; b.NormalZ += normalZ;
                c.NormalX += normalX; c.NormalY += normalY; c.NormalZ += normalZ;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                var normalX = vertices[i].NormalX;
                var normalY = vertices[i].NormalY;
                var normalZ = vertices[i].NormalZ;
                var magnitude = MathF.Sqrt((normalX * normalX) + (normalY * normalY) + (normalZ * normalZ));
                if (magnitude < float.Epsilon)
                {
                    vertices[i].NormalX = 0f;
                    vertices[i].NormalY = 1f;
                    vertices[i].NormalZ = 0f;
                    continue;
                }

                vertices[i].NormalX = MathF.Round(normalX / magnitude, 4);
                vertices[i].NormalY = MathF.Round(normalY / magnitude, 4);
                vertices[i].NormalZ = MathF.Round(normalZ / magnitude, 4);
            }
        }

        // Diamond-Square fractal terrain
        private static float[,] GenerateDiamondSquare(int w, int h, int seed, float roughness)
        {
            int size = 1;
            while (size < Math.Max(w, h)) size <<= 1;
            size++;

            var grid = new float[size, size];
            var rng  = new DeterministicRng((ulong)(uint)seed);
            float scale = 1f;

            grid[0, 0]           = (float)rng.NextDouble();
            grid[size - 1, 0]    = (float)rng.NextDouble();
            grid[0, size - 1]    = (float)rng.NextDouble();
            grid[size - 1, size - 1] = (float)rng.NextDouble();

            int step = size - 1;
            while (step > 1)
            {
                int half = step / 2;

                // Diamond step
                for (int y = 0; y < size - 1; y += step)
                    for (int x = 0; x < size - 1; x += step)
                    {
                        float avg = (grid[x, y] + grid[x + step, y] + grid[x, y + step] + grid[x + step, y + step]) * 0.25f;
                        grid[x + half, y + half] = avg + ((float)rng.NextDouble() * 2 - 1) * scale;
                    }

                // Square step
                for (int y = 0; y < size; y += half)
                    for (int x = (y + half) % step; x < size; x += step)
                    {
                        float sum = 0; int cnt = 0;
                        if (x - half >= 0)    { sum += grid[x - half, y]; cnt++; }
                        if (x + half < size)  { sum += grid[x + half, y]; cnt++; }
                        if (y - half >= 0)    { sum += grid[x, y - half]; cnt++; }
                        if (y + half < size)  { sum += grid[x, y + half]; cnt++; }
                        grid[x, y] = sum / cnt + ((float)rng.NextDouble() * 2 - 1) * scale;
                    }

                scale *= MathF.Pow(2f, -roughness);
                step = half;
            }

            var result = new float[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    result[x, y] = grid[x, y];
            return result;
        }

        // Layered value-noise approximation
        private static float[,] GeneratePerlinLike(int w, int h, int seed, int octaves, float roughness)
        {
            var result   = new float[w, h];
            float amp    = 1f;
            float freq   = 4f / Math.Max(w, h);
            float maxAmp = 0f;
            var rng      = new DeterministicRng((ulong)(uint)seed);

            for (int o = 0; o < octaves; o++)
            {
                int gw = Math.Max(2, (int)(w * freq) + 2);
                int gh = Math.Max(2, (int)(h * freq) + 2);
                var noise = new float[gw, gh];
                for (int gy = 0; gy < gh; gy++)
                    for (int gx = 0; gx < gw; gx++)
                        noise[gx, gy] = (float)rng.NextDouble();

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float fx = Math.Clamp(x * freq * (gw - 1), 0, gw - 2);
                        float fy = Math.Clamp(y * freq * (gh - 1), 0, gh - 2);
                        int ix = (int)fx, iy = (int)fy;
                        float tx = fx - ix, ty = fy - iy;
                        float top    = noise[ix, iy]     + (noise[ix + 1, iy]     - noise[ix, iy])     * tx;
                        float bottom = noise[ix, iy + 1] + (noise[ix + 1, iy + 1] - noise[ix, iy + 1]) * tx;
                        result[x, y] += (top + (bottom - top) * ty) * amp;
                    }

                maxAmp += amp;
                amp    *= roughness;
                freq   *= 2f;
            }

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    result[x, y] /= maxAmp;

            return result;
        }

        private static void Normalize(float[,] grid, int w, int h, out float minV, out float maxV)
        {
            minV = float.MaxValue; maxV = float.MinValue;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (grid[x, y] < minV) minV = grid[x, y];
                    if (grid[x, y] > maxV) maxV = grid[x, y];
                }
            float range = maxV - minV;
            if (range < float.Epsilon) { minV = 0f; maxV = 1f; return; }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    grid[x, y] = (grid[x, y] - minV) / range;
            minV = 0f; maxV = 1f;
        }
    }
}
#endif
