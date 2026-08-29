using System;
using System.IO;
using Authoritative.Domain;
using Authoritative.Services;

#if !UNITY_5_3_OR_NEWER
#if UNITY_5_3_OR_NEWER
using Assert = NUnit.Framework.Assert;
using FactAttribute = NUnit.Framework.TestAttribute;
#else
using Assert = Xunit.Assert;
using FactAttribute = Xunit.FactAttribute;
#endif

namespace Authoritative.Tests
{
    public class PipelineExecutionServiceTests
    {
        [FactAttribute]
        public void Execute_WritesArtifactAndReturnsWorldData()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"pipeline-exec-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var store = new GeneratedItemStore(Path.Combine(tempDirectory, "items"));
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), store);
                var service = new PipelineExecutionService(
                    adapter,
                    Path.Combine(tempDirectory, "world-builds"));

                var runtimeSnapshot = new PipelineRuntimeSnapshot
                {
                    IsLoaded = true,
                    ActiveDefinition = new PipelineDefinition
                    {
                        PipelineId = "pipeline_abc",
                        RequestId = "req_123",
                        Ecs = new EcsGenerationConfig
                        {
                            Seed = 1001,
                            Width = 80,
                            Height = 24,
                            DungeonLevel = 3,
                            EnemyCount = 5,
                            LootCount = 3
                        },
                        Steps =
                        {
                            new PipelineStepDefinition { Stage = "layout", EcsSystem = "DungeonGeneratorSystem", Enabled = true },
                            new PipelineStepDefinition { Stage = "encounters", EcsSystem = "EncounterSpawnSystem", Enabled = true },
                            new PipelineStepDefinition { Stage = "loot", EcsSystem = "LootPlacementSystem", Enabled = true }
                        }
                    }
                };

                var record = service.Execute(runtimeSnapshot, new PipelineExecutionRequest { RequestedBy = "integration-test" });

#if UNITY_5_3_OR_NEWER
                Assert.IsTrue(File.Exists(record.ArtifactPath));
                Assert.AreEqual(5, record.World.Enemies.Count);
                Assert.AreEqual(3, record.World.Loot.Count);
                Assert.IsNotNull(record.World.TerrainMesh);
                Assert.Greater(record.World.TerrainMesh.Vertices.Length, 0);
                Assert.Greater(record.World.TerrainMesh.Triangles.Length, 0);
                Assert.AreEqual(3, record.StepResults.Count);
                Assert.AreEqual("completed", record.Status);
                Assert.AreEqual(record.ExecutionId, service.GetLatestExecution()!.ExecutionId);
#else
                Assert.True(File.Exists(record.ArtifactPath));
                Assert.Equal(5, record.World.Enemies.Count);
                Assert.Equal(3, record.World.Loot.Count);
                Assert.NotNull(record.World.TerrainMesh);
                Assert.NotEmpty(record.World.TerrainMesh.Vertices);
                Assert.NotEmpty(record.World.TerrainMesh.Triangles);
                Assert.Equal(3, record.StepResults.Count);
                Assert.Equal("completed", record.Status);
                Assert.Equal(record.ExecutionId, service.GetLatestExecution()!.ExecutionId);
#endif
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
#endif
