#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Authoritative.Domain;
using Authoritative.Multiplayer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Authoritative.Services
{
    public sealed class WorldGenerationRequest
    {
        public PipelineDefinition Definition { get; set; } = new();
        public PipelineExecutionRequest Execution { get; set; } = new();
    }

    public interface IWorldGenerationAdapter
    {
        GeneratedWorldArtifact Generate(WorldGenerationRequest request);
    }

    public sealed class LocalWorldGenerationAdapter : IWorldGenerationAdapter
    {
        private static readonly string[] EnemyArchetypes = PersistenceTagCatalog.EnemyArchetypes;

        private readonly IItemGenerator _itemGenerator;
        private readonly IGeneratedItemStore _itemStore;
        private readonly HeightmapGenerator _terrainGenerator = new();

        public LocalWorldGenerationAdapter(IItemGenerator itemGenerator, IGeneratedItemStore itemStore)
        {
            _itemGenerator = itemGenerator;
            _itemStore = itemStore;
        }

        public GeneratedWorldArtifact Generate(WorldGenerationRequest request)
        {
            var definition = request.Definition;
            var execution = request.Execution;
            var ecs = definition.Ecs;
            var random = new DeterministicRng((ulong)(uint)ecs.Seed);
            var sessionId = string.IsNullOrWhiteSpace(execution.SessionId) ? string.Empty : execution.SessionId.Trim();
            var terrainMesh = _terrainGenerator.Generate(new HeightmapRequest
            {
                Width = ecs.Width,
                Height = ecs.Height,
                Seed = ecs.Seed,
                Algorithm = "diamond-square",
                WaterLevel = 0.32f,
                Roughness = 0.55f,
                Octaves = 4,
            });

            var roomCount = Math.Clamp(Math.Max(4, (ecs.Width * ecs.Height) / 180), 4, 64);
            var rooms = new List<WorldRoom>(roomCount);
            for (int i = 0; i < roomCount; i++)
            {
                int roomWidth = random.Next(4, Math.Max(5, Math.Min(12, ecs.Width)));
                int roomHeight = random.Next(4, Math.Max(5, Math.Min(10, ecs.Height)));
                int xBound = Math.Max(1, ecs.Width - roomWidth);
                int yBound = Math.Max(1, ecs.Height - roomHeight);

                rooms.Add(new WorldRoom
                {
                    Id = i + 1,
                    X = random.Next(0, xBound),
                    Y = random.Next(0, yBound),
                    Width = roomWidth,
                    Height = roomHeight
                });
            }

            var enemies = new List<WorldEnemy>(Math.Max(0, ecs.EnemyCount));
            for (int i = 0; i < ecs.EnemyCount; i++)
            {
                enemies.Add(new WorldEnemy
                {
                    Id = i + 1,
                    Archetype = EnemyArchetypes[random.Next(EnemyArchetypes.Length)],
                    X = random.Next(0, Math.Max(1, ecs.Width)),
                    Y = random.Next(0, Math.Max(1, ecs.Height)),
                    Level = Math.Max(1, ecs.DungeonLevel + random.Next(-1, 2))
                });
            }

            var loot = new List<WorldLoot>(Math.Max(0, ecs.LootCount));
            for (int i = 0; i < ecs.LootCount; i++)
            {
                var generatedItem = _itemGenerator.GenerateUniqueItem();
                _itemStore.SaveGeneratedItem(generatedItem, new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "pipeline-execution",
                    ["pipelineId"] = definition.PipelineId,
                    ["requestId"] = definition.RequestId,
                    ["sessionId"] = sessionId
                });

                loot.Add(new WorldLoot
                {
                    ItemId = generatedItem.Id,
                    ItemType = generatedItem.Type,
                    Tier = generatedItem.Tier,
                    X = random.Next(0, Math.Max(1, ecs.Width)),
                    Y = random.Next(0, Math.Max(1, ecs.Height))
                });
            }

            return new GeneratedWorldArtifact
            {
                Seed = ecs.Seed,
                Width = ecs.Width,
                Height = ecs.Height,
                DungeonLevel = ecs.DungeonLevel,
                Rooms = rooms,
                Enemies = enemies,
                Loot = loot,
                TerrainMesh = terrainMesh
            };
        }
    }

    public sealed class ExternalWorldGenerationAdapter : IWorldGenerationAdapter
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ExternalWorldGenerationAdapter> _logger;
        private readonly string _baseUrl;
        private readonly string? _apiKey;

        public ExternalWorldGenerationAdapter(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ExternalWorldGenerationAdapter> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _baseUrl = (configuration["EXTERNAL_GENERATOR_BASE_URL"] ?? string.Empty).TrimEnd('/');
            _apiKey = configuration["EXTERNAL_GENERATOR_API_KEY"];
        }

        public GeneratedWorldArtifact Generate(WorldGenerationRequest request)
        {
            if (string.IsNullOrWhiteSpace(_baseUrl))
                throw new InvalidOperationException("EXTERNAL_GENERATOR_BASE_URL is not configured.");

            return GenerateAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        }

        private async Task<GeneratedWorldArtifact> GenerateAsync(WorldGenerationRequest request, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient(nameof(ExternalWorldGenerationAdapter));
            var endpoint = _baseUrl + "/api/generators/world-pipeline";
            var payload = JsonConvert.SerializeObject(request);
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(_apiKey))
                message.Headers.Add("X-Generator-Key", _apiKey);

            var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("External generator call failed with status {StatusCode}: {Body}", response.StatusCode, responseBody);
                throw new InvalidOperationException($"External generator call failed: {(int)response.StatusCode}");
            }

            var artifact = JsonConvert.DeserializeObject<GeneratedWorldArtifact>(responseBody);
            if (artifact == null)
                throw new InvalidOperationException("External generator returned an empty payload.");

            return artifact;
        }
    }
}
#endif