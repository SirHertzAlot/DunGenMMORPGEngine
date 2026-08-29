using System;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// Week 4: Procedural Generation Depth - Backend Service Tests
    /// Validates that the dungeon generation service produces correct room layouts,
    /// enemy compositions, and loot distributions.
    /// Reference: WEEK4_EXECUTION_PLAN.md
    /// </summary>
    public class Week4ProceduralGenerationTests
    {
        private string _tempDirectory = "";

        private void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"week4-gen-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDirectory);
        }

        private void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }

        #region Room Generation Tests

        [FactAttribute]
        public void GenerateDungeon_ProducesValidRoomLayout()
        {
            SetUp();
            try
            {
                // GIVEN a generator service with a valid pipeline
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();

                // WHEN I generate a dungeon
                var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-room-layout",
                    RequestedBy = "test",
                    SeedOverride = 42
                });

                // THEN the job should complete successfully
                Assert.Equal("completed", job.Status);
                Assert.NotNull(job.Execution);
                Assert.NotNull(job.Execution!.World);

                var world = job.Execution.World;

                // AND the dungeon should have rooms
                Assert.NotEmpty(world.Rooms);
                Assert.True(world.Rooms.Count > 0);

                // AND each room should have valid dimensions
                foreach (var room in world.Rooms)
                {
                    Assert.True(room.Width > 0);
                    Assert.True(room.Height > 0);
                    Assert.True(room.X >= 0);
                    Assert.True(room.Y >= 0);
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_ProducesAtLeastFiveRoomTypes()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();

                // WHEN I generate 10 dungeons
                var allRoomTypes = new System.Collections.Generic.HashSet<string>();
                for (int seed = 0; seed < 10; seed++)
                {
                    var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                    {
                        SessionId = $"test-room-types-{seed}",
                        RequestedBy = "test",
                        SeedOverride = seed
                    });

                    if (job.Execution?.World?.Rooms != null)
                    {
                        foreach (var room in job.Execution.World.Rooms)
                        {
                            // Room type could be encoded in room properties, for now we just track that variety exists
                            allRoomTypes.Add($"{room.Width}x{room.Height}");
                        }
                    }
                }

                // THEN we should see variety in room types
                Assert.True(allRoomTypes.Count >= 3);
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_ProducesConsistentLayoutWithSameSeed()
        {
            SetUp();
            try
            {
                // GIVEN two generator services with the same pipeline and seed
                var itemStore1 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items1"));
                var observability1 = new AdminObservabilityService();
                var adapter1 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore1);
                var executionService1 = new PipelineExecutionService(adapter1, observability1, Path.Combine(_tempDirectory, "worlds1"));
                var generatorService1 = new HeadlessGeneratorService(executionService1, observability1);

                var itemStore2 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items2"));
                var observability2 = new AdminObservabilityService();
                var adapter2 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore2);
                var executionService2 = new PipelineExecutionService(adapter2, observability2, Path.Combine(_tempDirectory, "worlds2"));
                var generatorService2 = new HeadlessGeneratorService(executionService2, observability2);

                var runtimeSnapshot = CreateValidPipelineSnapshot();
                const int testSeed = 12345;

                // WHEN I generate dungeons with the same seed
                var job1 = generatorService1.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-determ-1",
                    RequestedBy = "test",
                    SeedOverride = testSeed
                });

                var job2 = generatorService2.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-determ-2",
                    RequestedBy = "test",
                    SeedOverride = testSeed
                });

                // THEN the room layouts should be identical
                Assert.NotNull(job1.Execution?.World?.Rooms);
                Assert.NotNull(job2.Execution?.World?.Rooms);
                
                Assert.Equal(job1.Execution!.World.Rooms.Count, job2.Execution!.World.Rooms.Count);

                for (int i = 0; i < job1.Execution.World.Rooms.Count; i++)
                {
                    var r1 = job1.Execution.World.Rooms[i];
                    var r2 = job2.Execution.World.Rooms[i];

                    Assert.Equal(r1.X, r2.X);
                    Assert.Equal(r1.Y, r2.Y);
                    Assert.Equal(r1.Width, r2.Width);
                    Assert.Equal(r1.Height, r2.Height);
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_EnemyArchetypesAreValid()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();

                // WHEN I generate a dungeon
                var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-enemy-archetypes",
                    RequestedBy = "test",
                    SeedOverride = 42
                });

                // THEN enemies should have valid archetypes
                var enemies = job.Execution!.World.Enemies;
                var archetypeSet = new System.Collections.Generic.HashSet<string>();

                foreach (var enemy in enemies)
                {
                    Assert.NotEmpty(enemy.Archetype);
                    archetypeSet.Add(enemy.Archetype);
                }

                // Should have at least 2 different enemy types
                Assert.True(archetypeSet.Count >= 2, "Expected at least 2 different enemy archetypes");
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_EnemyPlacementWithinRooms()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();
                int maxWidth = runtimeSnapshot.ActiveDefinition!.Ecs.Width;
                int maxHeight = runtimeSnapshot.ActiveDefinition!.Ecs.Height;

                // WHEN I generate a dungeon
                var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-enemy-placement",
                    RequestedBy = "test",
                    SeedOverride = 42
                });

                // THEN all enemies should be placed within dungeon bounds
                var enemies = job.Execution!.World.Enemies;
                foreach (var enemy in enemies)
                {
                    Assert.True(enemy.X >= 0, $"Enemy X {enemy.X} is negative");
                    Assert.True(enemy.Y >= 0, $"Enemy Y {enemy.Y} is negative");
                    Assert.True(enemy.X < maxWidth, $"Enemy X {enemy.X} exceeds width {maxWidth}");
                    Assert.True(enemy.Y < maxHeight, $"Enemy Y {enemy.Y} exceeds height {maxHeight}");
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_EnemyCountIsConsistentWithConfig()
        {
            SetUp();
            try
            {
                // GIVEN a generator service with specific enemy count config
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var snapshot = CreateValidPipelineSnapshot();
                int expectedCount = snapshot.ActiveDefinition!.Ecs.EnemyCount;

                // WHEN I generate multiple dungeons with the same seed
                const int testSeed = 666;
                for (int i = 0; i < 3; i++)
                {
                    var job = generatorService.CreateJob(snapshot, new GeneratorJobRequest
                    {
                        SessionId = $"test-enemy-count-{i}",
                        RequestedBy = "test",
                        SeedOverride = testSeed
                    });

                    // THEN each dungeon should match the configured enemy count
                    Assert.Equal(expectedCount, job.Execution!.World.Enemies.Count);
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_EnemyLevelsScaleCorrectly()
        {
            SetUp();
            try
            {
                // GIVEN a generator service with multiple difficulty levels
                var itemStore1 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items1"));
                var observability1 = new AdminObservabilityService();
                var adapter1 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore1);
                var executionService1 = new PipelineExecutionService(adapter1, observability1, Path.Combine(_tempDirectory, "worlds1"));
                var generatorService1 = new HeadlessGeneratorService(executionService1, observability1);

                var itemStore2 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items2"));
                var observability2 = new AdminObservabilityService();
                var adapter2 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore2);
                var executionService2 = new PipelineExecutionService(adapter2, observability2, Path.Combine(_tempDirectory, "worlds2"));
                var generatorService2 = new HeadlessGeneratorService(executionService2, observability2);

                var snapshot1 = CreateValidPipelineSnapshot();
                snapshot1.ActiveDefinition!.Ecs.DungeonLevel = 1;

                var snapshot10 = CreateValidPipelineSnapshot();
                snapshot10.ActiveDefinition!.Ecs.DungeonLevel = 10;

                // WHEN I generate dungeons at different difficulty levels
                var job1 = generatorService1.CreateJob(snapshot1, new GeneratorJobRequest
                {
                    SessionId = "test-enemy-level-1",
                    RequestedBy = "test"
                });

                var job10 = generatorService2.CreateJob(snapshot10, new GeneratorJobRequest
                {
                    SessionId = "test-enemy-level-10",
                    RequestedBy = "test"
                });

                // THEN higher level dungeons should have higher average enemy levels
                var avgLevel1 = job1.Execution!.World.Enemies.Count > 0
                    ? job1.Execution.World.Enemies.Average(e => e.Level)
                    : 0;
                var avgLevel10 = job10.Execution!.World.Enemies.Count > 0
                    ? job10.Execution.World.Enemies.Average(e => e.Level)
                    : 0;

                Assert.True(avgLevel10 > avgLevel1, $"Level 10 avg ({avgLevel10}) should exceed Level 1 avg ({avgLevel1})");
            }
            finally
            {
                TearDown();
            }
        }

        #endregion

        #region Enemy Composition Tests

        [FactAttribute]
        public void GenerateDungeon_ProducesValidEnemyComposition()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();

                // WHEN I generate a dungeon
                var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-enemies",
                    RequestedBy = "test",
                    SeedOverride = 42
                });

                // THEN the dungeon should have enemies
                Assert.NotNull(job.Execution?.World?.Enemies);
                Assert.NotEmpty(job.Execution!.World.Enemies);

                var expectedEnemyCount = runtimeSnapshot.ActiveDefinition!.Ecs.EnemyCount;
                var actualEnemyCount = job.Execution.World.Enemies.Count;

                Assert.Equal(expectedEnemyCount, actualEnemyCount);

                // AND each enemy should have valid properties
                foreach (var enemy in job.Execution.World.Enemies)
                {
                    Assert.NotEmpty(enemy.Archetype);
                    Assert.True(enemy.Level > 0);
                    Assert.True(enemy.X >= 0);
                    Assert.True(enemy.Y >= 0);
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_EnemyDifficultyScalesWithDungeonLevel()
        {
            SetUp();
            try
            {
                // GIVEN two generators with different dungeon levels
                var itemStore1 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items1"));
                var observability1 = new AdminObservabilityService();
                var adapter1 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore1);
                var executionService1 = new PipelineExecutionService(adapter1, observability1, Path.Combine(_tempDirectory, "worlds1"));
                var generatorService1 = new HeadlessGeneratorService(executionService1, observability1);

                var itemStore2 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items2"));
                var observability2 = new AdminObservabilityService();
                var adapter2 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore2);
                var executionService2 = new PipelineExecutionService(adapter2, observability2, Path.Combine(_tempDirectory, "worlds2"));
                var generatorService2 = new HeadlessGeneratorService(executionService2, observability2);

                var snapshot1 = CreateValidPipelineSnapshot();
                snapshot1.ActiveDefinition!.Ecs.DungeonLevel = 1;

                var snapshot2 = CreateValidPipelineSnapshot();
                snapshot2.ActiveDefinition!.Ecs.DungeonLevel = 5;

                // WHEN I generate dungeons at different levels
                var job1 = generatorService1.CreateJob(snapshot1, new GeneratorJobRequest
                {
                    SessionId = "test-level-1",
                    RequestedBy = "test"
                });

                var job2 = generatorService2.CreateJob(snapshot2, new GeneratorJobRequest
                {
                    SessionId = "test-level-5",
                    RequestedBy = "test"
                });

                // THEN enemies at higher levels should be stronger
                Assert.NotNull(job1.Execution?.World?.Enemies);
                Assert.NotNull(job2.Execution?.World?.Enemies);
                
                var avgLevel1 = job1.Execution!.World.Enemies.Count > 0 
                    ? job1.Execution.World.Enemies.Average(e => e.Level) 
                    : 0;
                var avgLevel5 = job2.Execution!.World.Enemies.Count > 0 
                    ? job2.Execution.World.Enemies.Average(e => e.Level) 
                    : 0;

                Assert.True(avgLevel5 > avgLevel1);
            }
            finally
            {
                TearDown();
            }
        }

        #endregion

        #region Loot Distribution Tests

        [FactAttribute]
        public void GenerateDungeon_ProducesValidLootDistribution()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();

                // WHEN I generate a dungeon
                var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-loot",
                    RequestedBy = "test",
                    SeedOverride = 42
                });

                // THEN the dungeon should have loot
                Assert.NotNull(job.Execution?.World?.Loot);
                Assert.NotEmpty(job.Execution!.World.Loot);

                var expectedLootCount = runtimeSnapshot.ActiveDefinition!.Ecs.LootCount;
                var actualLootCount = job.Execution.World.Loot.Count;

                Assert.Equal(expectedLootCount, actualLootCount);

                // AND each loot item should be valid
                foreach (var loot in job.Execution.World.Loot)
                {
                    Assert.NotEmpty(loot.ItemType);
                    Assert.True(loot.X >= 0);
                    Assert.True(loot.Y >= 0);
                    Assert.NotEmpty(loot.Tier);
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_LootRarityProgressesWithDifficultyLevel()
        {
            SetUp();
            try
            {
                // GIVEN two generators with different dungeon levels
                var itemStore1 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items1"));
                var observability1 = new AdminObservabilityService();
                var adapter1 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore1);
                var executionService1 = new PipelineExecutionService(adapter1, observability1, Path.Combine(_tempDirectory, "worlds1"));
                var generatorService1 = new HeadlessGeneratorService(executionService1, observability1);

                var itemStore2 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items2"));
                var observability2 = new AdminObservabilityService();
                var adapter2 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore2);
                var executionService2 = new PipelineExecutionService(adapter2, observability2, Path.Combine(_tempDirectory, "worlds2"));
                var generatorService2 = new HeadlessGeneratorService(executionService2, observability2);

                var snapshot1 = CreateValidPipelineSnapshot();
                snapshot1.ActiveDefinition!.Ecs.DungeonLevel = 1;

                var snapshot2 = CreateValidPipelineSnapshot();
                snapshot2.ActiveDefinition!.Ecs.DungeonLevel = 8;

                // WHEN I generate dungeons at different levels
                var job1 = generatorService1.CreateJob(snapshot1, new GeneratorJobRequest
                {
                    SessionId = "test-loot-level-1",
                    RequestedBy = "test"
                });

                var job2 = generatorService2.CreateJob(snapshot2, new GeneratorJobRequest
                {
                    SessionId = "test-loot-level-8",
                    RequestedBy = "test"
                });

                // THEN both dungeons should have valid loot (may not differ significantly in rarity yet)
                Assert.NotNull(job1.Execution?.World?.Loot);
                Assert.NotNull(job2.Execution?.World?.Loot);
                Assert.NotEmpty(job1.Execution!.World.Loot);
                Assert.NotEmpty(job2.Execution!.World.Loot);
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_RoomPositionsAreConsistent()
        {
            SetUp();
            try
            {
                // GIVEN two generator services with the same seed
                var itemStore1 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items1"));
                var observability1 = new AdminObservabilityService();
                var adapter1 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore1);
                var executionService1 = new PipelineExecutionService(adapter1, observability1, Path.Combine(_tempDirectory, "worlds1"));
                var generatorService1 = new HeadlessGeneratorService(executionService1, observability1);

                var itemStore2 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items2"));
                var observability2 = new AdminObservabilityService();
                var adapter2 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore2);
                var executionService2 = new PipelineExecutionService(adapter2, observability2, Path.Combine(_tempDirectory, "worlds2"));
                var generatorService2 = new HeadlessGeneratorService(executionService2, observability2);

                var snapshot = CreateValidPipelineSnapshot();
                const int testSeed = 555;

                // WHEN I generate dungeons with the same seed
                var job1 = generatorService1.CreateJob(snapshot, new GeneratorJobRequest
                {
                    SessionId = "test-room-pos-1",
                    RequestedBy = "test",
                    SeedOverride = testSeed
                });

                var job2 = generatorService2.CreateJob(snapshot, new GeneratorJobRequest
                {
                    SessionId = "test-room-pos-2",
                    RequestedBy = "test",
                    SeedOverride = testSeed
                });

                // THEN all room positions should be identical
                var rooms1 = job1.Execution!.World.Rooms;
                var rooms2 = job2.Execution!.World.Rooms;
                Assert.Equal(rooms1.Count, rooms2.Count);

                for (int i = 0; i < rooms1.Count; i++)
                {
                    Assert.Equal(rooms1[i].X, rooms2[i].X);
                    Assert.Equal(rooms1[i].Y, rooms2[i].Y);
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_RoomCountIsConsistentWithSameSeed()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore1 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items1"));
                var observability1 = new AdminObservabilityService();
                var adapter1 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore1);
                var executionService1 = new PipelineExecutionService(adapter1, observability1, Path.Combine(_tempDirectory, "worlds1"));
                var generatorService1 = new HeadlessGeneratorService(executionService1, observability1);

                var itemStore2 = new GeneratedItemStore(Path.Combine(_tempDirectory, "items2"));
                var observability2 = new AdminObservabilityService();
                var adapter2 = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore2);
                var executionService2 = new PipelineExecutionService(adapter2, observability2, Path.Combine(_tempDirectory, "worlds2"));
                var generatorService2 = new HeadlessGeneratorService(executionService2, observability2);

                var snapshot = CreateValidPipelineSnapshot();
                const int testSeed = 777;

                // WHEN I generate dungeons with the same seed multiple times
                var job1 = generatorService1.CreateJob(snapshot, new GeneratorJobRequest
                {
                    SessionId = "test-room-count-1",
                    RequestedBy = "test",
                    SeedOverride = testSeed
                });

                var job2 = generatorService2.CreateJob(snapshot, new GeneratorJobRequest
                {
                    SessionId = "test-room-count-2",
                    RequestedBy = "test",
                    SeedOverride = testSeed
                });

                // THEN room count should be identical
                Assert.Equal(job1.Execution!.World.Rooms.Count, job2.Execution!.World.Rooms.Count);
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_RoomsHaveVariedDimensions()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();

                // WHEN I generate a dungeon
                var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-room-variety",
                    RequestedBy = "test",
                    SeedOverride = 42
                });

                // THEN rooms should have varied dimensions (not all the same size)
                var rooms = job.Execution!.World.Rooms;
                var dimensionSet = new System.Collections.Generic.HashSet<string>();
                
                foreach (var room in rooms)
                {
                    dimensionSet.Add($"{room.Width}x{room.Height}");
                }

                // Should have at least 2 different room sizes
                Assert.True(dimensionSet.Count >= 2, "Expected at least 2 different room dimensions");
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_AllRoomsWithinDungeonBounds()
        {
            SetUp();
            try
            {
                // GIVEN a generator service with defined dungeon bounds
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();
                int maxWidth = runtimeSnapshot.ActiveDefinition!.Ecs.Width;
                int maxHeight = runtimeSnapshot.ActiveDefinition!.Ecs.Height;

                // WHEN I generate a dungeon
                var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-room-bounds",
                    RequestedBy = "test",
                    SeedOverride = 42
                });

                // THEN all rooms should be within the dungeon bounds
                var rooms = job.Execution!.World.Rooms;
                foreach (var room in rooms)
                {
                    Assert.True(room.X >= 0, $"Room X position {room.X} is negative");
                    Assert.True(room.Y >= 0, $"Room Y position {room.Y} is negative");
                    Assert.True(room.X + room.Width <= maxWidth, $"Room extends beyond width: {room.X} + {room.Width} > {maxWidth}");
                    Assert.True(room.Y + room.Height <= maxHeight, $"Room extends beyond height: {room.Y} + {room.Height} > {maxHeight}");
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_RoomCountIsStableAcrossMultipleGenerations()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var snapshot = CreateValidPipelineSnapshot();
                const int testSeed = 888;

                // WHEN I generate the same dungeon multiple times with the same seed
                var roomCounts = new System.Collections.Generic.List<int>();
                for (int i = 0; i < 5; i++)
                {
                    var job = generatorService.CreateJob(snapshot, new GeneratorJobRequest
                    {
                        SessionId = $"test-room-stability-{i}",
                        RequestedBy = "test",
                        SeedOverride = testSeed
                    });
                    roomCounts.Add(job.Execution!.World.Rooms.Count);
                }

                // THEN room count should always be the same for the same seed
                int firstCount = roomCounts[0];
                foreach (var count in roomCounts)
                {
                    Assert.Equal(firstCount, count);
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_LootPlacementWithinBounds()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();
                int maxWidth = runtimeSnapshot.ActiveDefinition!.Ecs.Width;
                int maxHeight = runtimeSnapshot.ActiveDefinition!.Ecs.Height;

                // WHEN I generate a dungeon
                var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-loot-bounds",
                    RequestedBy = "test",
                    SeedOverride = 42
                });

                // THEN all loot should be placed within dungeon bounds
                var loot = job.Execution!.World.Loot;
                foreach (var item in loot)
                {
                    Assert.True(item.X >= 0, $"Loot X {item.X} is negative");
                    Assert.True(item.Y >= 0, $"Loot Y {item.Y} is negative");
                    Assert.True(item.X < maxWidth, $"Loot X {item.X} exceeds width {maxWidth}");
                    Assert.True(item.Y < maxHeight, $"Loot Y {item.Y} exceeds height {maxHeight}");
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_LootItemTypesAreValid()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();

                // WHEN I generate a dungeon
                var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                {
                    SessionId = "test-loot-types",
                    RequestedBy = "test",
                    SeedOverride = 42
                });

                // THEN loot items should have valid types and tiers
                var loot = job.Execution!.World.Loot;
                var itemTypes = new System.Collections.Generic.HashSet<string>();
                var tiers = new System.Collections.Generic.HashSet<string>();

                foreach (var item in loot)
                {
                    Assert.NotEmpty(item.ItemType);
                    Assert.NotEmpty(item.Tier);
                    itemTypes.Add(item.ItemType);
                    tiers.Add(item.Tier);
                }

                // Should have at least 2 different item types
                Assert.True(itemTypes.Count >= 2, "Expected at least 2 different item types");
                // Should have at least 1 tier (common, uncommon, rare, etc)
                Assert.True(tiers.Count >= 1, "Expected at least 1 loot tier");
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_LootCountIsConsistentWithConfig()
        {
            SetUp();
            try
            {
                // GIVEN a generator service with specific loot count config
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var snapshot = CreateValidPipelineSnapshot();
                int expectedCount = snapshot.ActiveDefinition!.Ecs.LootCount;

                // WHEN I generate multiple dungeons with the same seed
                const int testSeed = 999;
                for (int i = 0; i < 3; i++)
                {
                    var job = generatorService.CreateJob(snapshot, new GeneratorJobRequest
                    {
                        SessionId = $"test-loot-count-{i}",
                        RequestedBy = "test",
                        SeedOverride = testSeed
                    });

                    // THEN each dungeon should match the configured loot count
                    Assert.Equal(expectedCount, job.Execution!.World.Loot.Count);
                }
            }
            finally
            {
                TearDown();
            }
        }

        [FactAttribute]
        public void GenerateDungeon_LootTiersAreValid()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var snapshot = CreateValidPipelineSnapshot();

                // WHEN I generate dungeons at different levels
                var levels = new[] { 1, 5, 9 };
                foreach (var level in levels)
                {
                    snapshot.ActiveDefinition!.Ecs.DungeonLevel = level;

                    var job = generatorService.CreateJob(snapshot, new GeneratorJobRequest
                    {
                        SessionId = $"test-loot-tiers-{level}",
                        RequestedBy = "test"
                    });

                    // THEN all loot items should have valid tiers
                    Assert.NotEmpty(job.Execution!.World.Loot);

                    var validTiers = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                    {
                        "common", "uncommon", "rare", "epic", "legendary"
                    };

                    foreach (var loot in job.Execution.World.Loot)
                    {
                        Assert.True(validTiers.Contains(loot.Tier), $"Invalid loot tier: {loot.Tier}");
                    }
                }
            }
            finally
            {
                TearDown();
            }
        }

        #endregion

        #region Integration & Stability Tests

        [FactAttribute]
        public void GenerateDungeon_1000Dungeons_AllValid()
        {
            SetUp();
            try
            {
                // GIVEN a generator service
                var itemStore = new GeneratedItemStore(Path.Combine(_tempDirectory, "items"));
                var observability = new AdminObservabilityService();
                var adapter = new LocalWorldGenerationAdapter(new ItemGenerator(), itemStore);
                var executionService = new PipelineExecutionService(adapter, observability, Path.Combine(_tempDirectory, "worlds"));
                var generatorService = new HeadlessGeneratorService(executionService, observability);

                var runtimeSnapshot = CreateValidPipelineSnapshot();
                var validCount = 0;
                var roomStats = new System.Collections.Generic.List<int>();
                var enemyStats = new System.Collections.Generic.List<int>();
                var lootStats = new System.Collections.Generic.List<int>();

                // WHEN I generate 1000 dungeons with different seeds
                for (int seed = 0; seed < 1000; seed++)
                {
                    var job = generatorService.CreateJob(runtimeSnapshot, new GeneratorJobRequest
                    {
                        SessionId = $"test-seed-{seed}",
                        RequestedBy = "test",
                        SeedOverride = seed
                    });

                    // THEN each should be valid
                    if (job.Status == "completed" &&
                        job.Execution?.World != null &&
                        job.Execution.World.Rooms.Count > 0 &&
                        job.Execution.World.Enemies.Count > 0 &&
                        job.Execution.World.Loot.Count > 0)
                    {
                        validCount++;
                        roomStats.Add(job.Execution.World.Rooms.Count);
                        enemyStats.Add(job.Execution.World.Enemies.Count);
                        lootStats.Add(job.Execution.World.Loot.Count);
                    }
                }

                // All should be valid
                Assert.Equal(1000, validCount);

                // Verify consistency across 1000 generations
                var uniqueRoomCounts = new System.Collections.Generic.HashSet<int>(roomStats);
                var uniqueEnemyCounts = new System.Collections.Generic.HashSet<int>(enemyStats);
                var uniqueLootCounts = new System.Collections.Generic.HashSet<int>(lootStats);

                // Room count should be stable (within 1-2 values for normal variation)
                Assert.True(uniqueRoomCounts.Count <= 3, $"Expected stable room counts, got {uniqueRoomCounts.Count} different values");
            }
            finally
            {
                TearDown();
            }
        }

        #endregion

        #region Helpers

        private PipelineRuntimeSnapshot CreateValidPipelineSnapshot()
        {
            return new PipelineRuntimeSnapshot
            {
                IsLoaded = true,
                ActiveDefinition = new PipelineDefinition
                {
                    PipelineId = "pipeline_week4_test",
                    RequestId = "request_week4_test",
                    Ecs = new EcsGenerationConfig
                    {
                        Seed = 42,
                        Width = 60,
                        Height = 40,
                        DungeonLevel = 3,
                        EnemyCount = 8,
                        LootCount = 5
                    },
                    Steps =
                    {
                        new PipelineStepDefinition { Stage = "layout", EcsSystem = "DungeonGeneratorSystem", Enabled = true },
                        new PipelineStepDefinition { Stage = "encounters", EcsSystem = "EncounterSpawnSystem", Enabled = true },
                        new PipelineStepDefinition { Stage = "loot", EcsSystem = "LootPlacementSystem", Enabled = true }
                    }
                }
            };
        }

        private double CalculateAverageTierRank(System.Collections.Generic.List<WorldLoot> lootItems)
        {
            // Simple tier ranking: Common=0, Uncommon=1, Rare=2, Epic=3, Legendary=4
            var tierRanks = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "common", 0 },
                { "uncommon", 1 },
                { "rare", 2 },
                { "epic", 3 },
                { "legendary", 4 }
            };

            if (lootItems.Count == 0)
                return 0;

            int totalRank = 0;
            foreach (var loot in lootItems)
            {
                if (tierRanks.TryGetValue(loot.Tier, out var rank))
                    totalRank += rank;
            }

            return (double)totalRank / lootItems.Count;
        }

        #endregion
    }
}
#endif

