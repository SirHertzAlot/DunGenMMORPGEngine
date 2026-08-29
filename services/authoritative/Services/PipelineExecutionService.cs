#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Authoritative.Services
{
    public interface IPipelineExecutionService
    {
        PipelineExecutionRecord Execute(PipelineRuntimeSnapshot runtimeSnapshot, PipelineExecutionRequest request);
        PipelineExecutionRecord? GetExecution(string executionId);
        IReadOnlyCollection<PipelineExecutionRecord> GetExecutions(int take = 25);
        PipelineExecutionRecord? GetLatestExecution();
    }

    public sealed class PipelineExecutionService : IPipelineExecutionService
    {
        private readonly IWorldGenerationAdapter _worldGenerationAdapter;
        private readonly IAdminObservabilityService? _observability;
        private readonly IScyllaWorldPersistenceService? _scylla;
        private readonly ConcurrentDictionary<string, PipelineExecutionRecord> _executions = new(StringComparer.Ordinal);
        private readonly object _fileLock = new();
        private readonly string _outputDirectory;

        public PipelineExecutionService(IWorldGenerationAdapter worldGenerationAdapter)
            : this(worldGenerationAdapter, null, null, Path.Combine(AppContext.BaseDirectory, "data", "world-builds"))
        {
        }

        public PipelineExecutionService(IWorldGenerationAdapter worldGenerationAdapter, string outputDirectory)
            : this(worldGenerationAdapter, null, null, outputDirectory)
        {
        }

        public PipelineExecutionService(
            IWorldGenerationAdapter worldGenerationAdapter,
            IAdminObservabilityService? observability,
            string outputDirectory)
            : this(worldGenerationAdapter, observability, null, outputDirectory)
        {
        }

        public PipelineExecutionService(
            IWorldGenerationAdapter worldGenerationAdapter,
            IAdminObservabilityService? observability,
            IScyllaWorldPersistenceService? scylla,
            string outputDirectory)
        {
            _worldGenerationAdapter = worldGenerationAdapter;
            _observability = observability;
            _scylla = scylla;
            _outputDirectory = outputDirectory;
            Directory.CreateDirectory(_outputDirectory);
        }

        public PipelineExecutionRecord Execute(PipelineRuntimeSnapshot runtimeSnapshot, PipelineExecutionRequest request)
        {
            if (!runtimeSnapshot.IsLoaded || runtimeSnapshot.ActiveDefinition == null)
                throw new InvalidOperationException("No active pipeline definition is loaded.");

            var definition = runtimeSnapshot.ActiveDefinition;
            var startedAt = DateTime.UtcNow;

            var world = _worldGenerationAdapter.Generate(new WorldGenerationRequest
            {
                Definition = definition,
                Execution = request
            });
            var executionId = "exec_" + Guid.NewGuid().ToString("N");
            var artifactPath = Path.Combine(_outputDirectory, executionId + ".json");

            var record = new PipelineExecutionRecord
            {
                ExecutionId = executionId,
                PipelineId = definition.PipelineId,
                RequestId = definition.RequestId,
                SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? null : request.SessionId.Trim(),
                RequestedBy = string.IsNullOrWhiteSpace(request.RequestedBy) ? "admin" : request.RequestedBy.Trim(),
                Notes = request.Notes?.Trim() ?? string.Empty,
                StartedAtUtc = startedAt,
                CompletedAtUtc = DateTime.UtcNow,
                ArtifactPath = artifactPath,
                Status = "completed",
                StepResults = BuildStepResults(definition, world),
                World = world
            };

            PersistExecution(record);
            _executions[executionId] = record;
            _observability?.RecordExecution(record);
            _scylla?.EnqueueWorld(record);

            return record;
        }

        public PipelineExecutionRecord? GetExecution(string executionId)
        {
            return _executions.TryGetValue(executionId, out var record) ? record : null;
        }

        public IReadOnlyCollection<PipelineExecutionRecord> GetExecutions(int take = 25)
        {
            return _executions.Values
                .OrderByDescending(x => x.CompletedAtUtc)
                .Take(Math.Max(1, take))
                .ToArray();
        }

        public PipelineExecutionRecord? GetLatestExecution()
        {
            return _executions.Values
                .OrderByDescending(x => x.CompletedAtUtc)
                .FirstOrDefault();
        }

        private static List<PipelineStepExecutionResult> BuildStepResults(PipelineDefinition definition, GeneratedWorldArtifact world)
        {
            var results = new List<PipelineStepExecutionResult>(definition.Steps.Count);
            foreach (var step in definition.Steps)
            {
                var summary = step.Stage switch
                {
                    "layout" => $"Generated {world.Rooms.Count} rooms for {world.Width}x{world.Height} map.",
                    "encounters" => $"Spawned {world.Enemies.Count} enemies at dungeon level {world.DungeonLevel}.",
                    "loot" => $"Placed {world.Loot.Count} loot entries.",
                    "terrain" => $"Built terrain mesh with {world.TerrainMesh.Vertices.Length} vertices and {world.TerrainMesh.Triangles.Length / 3} triangles.",
                    _ => "Step completed."
                };

                results.Add(new PipelineStepExecutionResult
                {
                    Stage = step.Stage,
                    EcsSystem = step.EcsSystem,
                    Status = step.Enabled ? "completed" : "skipped",
                    Summary = step.Enabled ? summary : "Step disabled in active pipeline definition."
                });
            }

            return results;
        }

        private void PersistExecution(PipelineExecutionRecord record)
        {
            var json = JsonConvert.SerializeObject(record, Formatting.Indented);
            var latestPath = Path.Combine(_outputDirectory, "latest-world.json");

            lock (_fileLock)
            {
                File.WriteAllText(record.ArtifactPath, json);
                File.WriteAllText(latestPath, json);
            }
        }
    }
}
#endif
