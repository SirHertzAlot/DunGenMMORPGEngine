#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Authoritative.Services
{
    public interface IPipelineDefinitionWriter
    {
        GeneratedPipelineDefinition WriteApprovedDefinition(PipelineRequestRecord approvedRequest, string approvedBy, int? overrideSeed);
    }

    public sealed class GeneratedPipelineDefinition
    {
        public string DefinitionPath { get; set; } = string.Empty;
        public string DefinitionHash { get; set; } = string.Empty;
        public PipelineDefinition LoadedDefinition { get; set; } = new();
    }

    public sealed class PipelineDefinitionWriter : IPipelineDefinitionWriter
    {
        private readonly object _lock = new();
        private readonly string _definitionsDirectory;
        private readonly ISerializer _serializer;

        public PipelineDefinitionWriter()
            : this(Path.Combine(AppContext.BaseDirectory, "data", "pipeline"))
        {
        }

        public PipelineDefinitionWriter(string definitionsDirectory)
        {
            _definitionsDirectory = definitionsDirectory;
            Directory.CreateDirectory(_definitionsDirectory);

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        public GeneratedPipelineDefinition WriteApprovedDefinition(PipelineRequestRecord approvedRequest, string approvedBy, int? overrideSeed)
        {
            var request = approvedRequest.RequestedConfig;
            var seed = overrideSeed ?? request.Seed ?? Random.Shared.Next(1, int.MaxValue);

            var definition = new PipelineDefinition
            {
                PipelineId = "pipeline_" + Guid.NewGuid().ToString("N"),
                RequestId = approvedRequest.RequestId,
                Name = request.PipelineName,
                Purpose = request.Purpose,
                ApprovedBy = approvedBy,
                ApprovedAtUtc = DateTime.UtcNow,
                Ecs = new EcsGenerationConfig
                {
                    DungeonLevel = request.DungeonLevel,
                    Width = request.Width,
                    Height = request.Height,
                    EnemyCount = request.EnemyCount,
                    LootCount = request.LootCount,
                    Seed = seed
                },
                Steps = BuildDefaultSteps(request)
            };

            var yaml = _serializer.Serialize(definition);
            var hash = ComputeSha256(yaml);

            var stampedName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{definition.PipelineId}.yaml";
            var stampedPath = Path.Combine(_definitionsDirectory, stampedName);
            var activePath = Path.Combine(_definitionsDirectory, "active-pipeline.yaml");

            lock (_lock)
            {
                File.WriteAllText(stampedPath, yaml, Encoding.UTF8);
                File.WriteAllText(activePath, yaml, Encoding.UTF8);
            }

            return new GeneratedPipelineDefinition
            {
                DefinitionPath = activePath,
                DefinitionHash = hash,
                LoadedDefinition = definition
            };
        }

        private static List<PipelineStepDefinition> BuildDefaultSteps(PipelineCreateRequest request)
        {
            return new List<PipelineStepDefinition>
            {
                new()
                {
                    Stage = "layout",
                    EcsSystem = "DungeonGeneratorSystem",
                    Enabled = true,
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["width"] = request.Width.ToString(),
                        ["height"] = request.Height.ToString(),
                        ["seed"] = (request.Seed ?? 0).ToString()
                    }
                },
                new()
                {
                    Stage = "encounters",
                    EcsSystem = "EncounterSpawnSystem",
                    Enabled = true,
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["enemyCount"] = request.EnemyCount.ToString(),
                        ["dungeonLevel"] = request.DungeonLevel.ToString()
                    }
                },
                new()
                {
                    Stage = "loot",
                    EcsSystem = "LootPlacementSystem",
                    Enabled = true,
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["lootCount"] = request.LootCount.ToString()
                    }
                }
            };
        }

        private static string ComputeSha256(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
#endif
