using System;
using System.IO;
using Authoritative.Domain;
using Authoritative.Services;

#if UNITY_5_3_OR_NEWER
using Assert = NUnit.Framework.Assert;
using FactAttribute = NUnit.Framework.TestAttribute;
#else
using Assert = Xunit.Assert;
using FactAttribute = Xunit.FactAttribute;
#endif

#if !UNITY_5_3_OR_NEWER
namespace Authoritative.Tests
{
    public class HeadlessGeneratorServiceTests
    {
        [FactAttribute]
        public void CreateJob_ExecutesPipelineAndTracksSession()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"headless-generator-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var itemStore = new GeneratedItemStore(Path.Combine(tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(
                    adapter,
                    observability,
                    Path.Combine(tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = new PipelineRuntimeSnapshot
                {
                    IsLoaded = true,
                    ActiveDefinition = new PipelineDefinition
                    {
                        PipelineId = "pipeline_world",
                        RequestId = "request_world",
                        Ecs = new EcsGenerationConfig
                        {
                            Seed = 77,
                            Width = 48,
                            Height = 24,
                            DungeonLevel = 2,
                            EnemyCount = 4,
                            LootCount = 2
                        },
                        Steps =
                        {
                            new PipelineStepDefinition { Stage = "layout", EcsSystem = "DungeonGeneratorSystem", Enabled = true },
                            new PipelineStepDefinition { Stage = "encounters", EcsSystem = "EncounterSpawnSystem", Enabled = true },
                            new PipelineStepDefinition { Stage = "loot", EcsSystem = "LootPlacementSystem", Enabled = true }
                        }
                    }
                };

                var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "session-alpha",
                    RequestedBy = "test-runner",
                    ConstraintsYaml = "seed: 77"
                });

                Assert.Equal("completed", job.Status);
                Assert.NotNull(job.Execution);
                Assert.Equal("session-alpha", job.SessionId);
                Assert.Equal(job.JobId, generatorService.GetLatestJobForSession("session-alpha")!.JobId);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [FactAttribute]
        public void CreateJob_GeneratesCharactersThroughSharedJobFlow()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"headless-generator-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var itemStore = new GeneratedItemStore(Path.Combine(tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(
                    adapter,
                    observability,
                    Path.Combine(tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var job = generatorService.CreateJob(new PipelineRuntimeSnapshot(), new GeneratorJobRequest
                {
                    GeneratorId = "characters",
                    SessionId = "session-characters",
                    RequestedBy = "test-runner",
                    SeedOverride = 1337,
                    Parameters = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["count"] = "2",
                        ["level"] = "12",
                        ["class"] = "Mage",
                        ["race"] = "Elf"
                    }
                });

                Assert.Equal("completed", job.Status);
                Assert.Equal("character-artifact", job.OutputMode);
                Assert.Null(job.Execution);

                var characters = Assert.IsType<System.Collections.Generic.List<GeneratedCharacter>>(job.Result);
                Assert.Equal(2, characters.Count);
                Assert.All(characters, character =>
                {
                    Assert.Equal("Mage", character.Class);
                    Assert.Equal("Elf", character.Race);
                    Assert.Equal(12, character.Level);
                });
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [FactAttribute]
        public void CreateJob_GeneratesTerrainMeshThroughSharedJobFlow()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"headless-generator-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var itemStore = new GeneratedItemStore(Path.Combine(tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(
                    adapter,
                    observability,
                    Path.Combine(tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var job = generatorService.CreateJob(new PipelineRuntimeSnapshot(), new GeneratorJobRequest
                {
                    GeneratorId = "heightmap",
                    SessionId = "session-heightmap",
                    RequestedBy = "test-runner",
                    SeedOverride = 2026,
                    Parameters = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["width"] = "32",
                        ["height"] = "16",
                        ["algorithm"] = "perlin",
                        ["waterLevel"] = "0.4",
                        ["octaves"] = "5"
                    }
                });

                Assert.Equal("completed", job.Status);
                Assert.Equal("terrain-mesh-artifact", job.OutputMode);
                Assert.Null(job.Execution);

                var terrainMesh = Assert.IsType<GeneratedTerrainMesh>(job.Result);
                Assert.Equal(32, terrainMesh.Width);
                Assert.Equal(16, terrainMesh.Height);
                Assert.Equal("perlin", terrainMesh.Algorithm);
                Assert.Equal(2026, terrainMesh.Seed);
                Assert.Equal(32 * 16, terrainMesh.Vertices.Length);
                Assert.Equal((32 - 1) * (16 - 1) * 6, terrainMesh.Triangles.Length);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
#endif
