#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Authoritative.Domain;

namespace Authoritative.Services
{
    public interface IHeadlessGeneratorService
    {
        IReadOnlyCollection<GeneratorCapabilityDescriptor> GetCapabilities();
        GeneratorJobRecord CreateJob(PipelineRuntimeSnapshot runtimeSnapshot, GeneratorJobRequest request);
        GeneratorJobRecord? GetJob(string jobId);
        IReadOnlyCollection<GeneratorJobRecord> GetJobs(int take = 25);
        GeneratorJobRecord? GetLatestJobForSession(string sessionId);
    }

    public sealed class HeadlessGeneratorService : IHeadlessGeneratorService
    {
        private readonly ConcurrentDictionary<string, GeneratorJobRecord> _jobs = new(StringComparer.Ordinal);
        private readonly IPipelineExecutionService _pipelineExecutionService;
        private readonly IAdminObservabilityService _observability;
        private readonly CharacterGenerator _characterGenerator = new();
        private readonly HeightmapGenerator _heightmapGenerator = new();

        public HeadlessGeneratorService(
            IPipelineExecutionService pipelineExecutionService,
            IAdminObservabilityService observability)
        {
            _pipelineExecutionService = pipelineExecutionService;
            _observability = observability;
        }

        public IReadOnlyCollection<GeneratorCapabilityDescriptor> GetCapabilities()
        {
            return new[]
            {
                new GeneratorCapabilityDescriptor
                {
                    GeneratorId = "world-pipeline",
                    Name = "World Pipeline Executor",
                    Description = "Consumes YAML constraints plus pipeline parameters and produces a world artifact for React and Unity consumers.",
                    InputMode = "yaml+parameters",
                    OutputMode = "world-artifact",
                    RequiresActivePipeline = true
                },
                new GeneratorCapabilityDescriptor
                {
                    GeneratorId = "characters",
                    Name = "Character Generator",
                    Description = "Builds generated character payloads from shared generator job parameters.",
                    InputMode = "parameters",
                    OutputMode = "character-artifact",
                    RequiresActivePipeline = false
                },
                new GeneratorCapabilityDescriptor
                {
                    GeneratorId = "heightmap",
                    Name = "Terrain Mesh Generator",
                    Description = "Builds final terrain mesh artifacts from shared generator job parameters.",
                    InputMode = "parameters",
                    OutputMode = "terrain-mesh-artifact",
                    RequiresActivePipeline = false
                }
            };
        }

        public GeneratorJobRecord CreateJob(PipelineRuntimeSnapshot runtimeSnapshot, GeneratorJobRequest request)
        {
            var normalizedGeneratorId = string.IsNullOrWhiteSpace(request.GeneratorId)
                ? "world-pipeline"
                : request.GeneratorId.Trim();

            var capability = GetCapabilities()
                .FirstOrDefault(x => string.Equals(x.GeneratorId, normalizedGeneratorId, StringComparison.Ordinal));
            if (capability == null)
                throw new InvalidOperationException($"Unsupported generatorId '{normalizedGeneratorId}'.");

            if (capability.RequiresActivePipeline && (!runtimeSnapshot.IsLoaded || runtimeSnapshot.ActiveDefinition == null))
                throw new InvalidOperationException("No active pipeline definition is loaded.");

            var job = new GeneratorJobRecord
            {
                JobId = "job_" + Guid.NewGuid().ToString("N"),
                GeneratorId = normalizedGeneratorId,
                OutputMode = capability.OutputMode,
                RequestedBy = string.IsNullOrWhiteSpace(request.RequestedBy) ? "admin" : request.RequestedBy.Trim(),
                SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? null : request.SessionId.Trim(),
                ConstraintsYaml = request.ConstraintsYaml ?? string.Empty,
                Notes = request.Notes ?? string.Empty,
                SeedOverride = request.SeedOverride,
                SubmittedAtUtc = DateTime.UtcNow,
                Status = "processing",
                Parameters = new Dictionary<string, string>(request.Parameters ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            };

            _jobs[job.JobId] = job;

            try
            {
                switch (normalizedGeneratorId)
                {
                    case "world-pipeline":
                    {
                        var execution = _pipelineExecutionService.Execute(runtimeSnapshot, new PipelineExecutionRequest
                        {
                            RequestedBy = job.RequestedBy,
                            Notes = job.Notes,
                            SessionId = job.SessionId,
                            ConstraintsYaml = job.ConstraintsYaml
                        });

                        job.Execution = execution;
                        break;
                    }
                    case "characters":
                        job.Result = _characterGenerator.Generate(BuildCharacterRequest(job));
                        break;
                    case "heightmap":
                        job.Result = _heightmapGenerator.Generate(BuildHeightmapRequest(job));
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported generatorId '{normalizedGeneratorId}'.");
                }

                job.CompletedAtUtc = DateTime.UtcNow;
                job.Status = "completed";

                _observability.RecordWorldEvent(new WorldSessionEvent
                {
                    SessionId = job.SessionId ?? "global",
                    EventType = "generator.job.completed",
                    Category = "generator",
                    Frame = 0,
                    EntityId = job.Execution?.ExecutionId ?? job.JobId,
                    Message = $"Generator job {job.JobId} completed via {job.GeneratorId}.",
                    TimestampUtc = DateTime.UtcNow,
                    Data = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["jobId"] = job.JobId,
                        ["generatorId"] = job.GeneratorId,
                        ["outputMode"] = job.OutputMode
                    }
                });

                if (job.Execution != null)
                {
                    _observability.RecordAction(job.GeneratorId, "completed", new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["jobId"] = job.JobId,
                        ["executionId"] = job.Execution.ExecutionId
                    });
                }
                else
                {
                    _observability.RecordAction(job.GeneratorId, "completed", new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["jobId"] = job.JobId,
                        ["outputMode"] = job.OutputMode
                    });
                }
            }
            catch (Exception ex)
            {
                job.CompletedAtUtc = DateTime.UtcNow;
                job.Status = "failed";
                job.Error = ex.Message;
                throw;
            }

            return job;
        }

        private static CharacterGenerationRequest BuildCharacterRequest(GeneratorJobRecord job)
        {
            return new CharacterGenerationRequest
            {
                Level = GetIntParameter(job.Parameters, "level", 1, 1, 60),
                Class = GetStringParameter(job.Parameters, "class"),
                Race = GetStringParameter(job.Parameters, "race"),
                Seed = job.SeedOverride,
                Count = GetIntParameter(job.Parameters, "count", 1, 1, 20)
            };
        }

        private static HeightmapRequest BuildHeightmapRequest(GeneratorJobRecord job)
        {
            return new HeightmapRequest
            {
                Width = GetIntParameter(job.Parameters, "width", 64, 8, 512),
                Height = GetIntParameter(job.Parameters, "height", 64, 8, 512),
                Seed = job.SeedOverride,
                WaterLevel = GetFloatParameter(job.Parameters, "waterLevel", 0.35f, 0f, 1f),
                Algorithm = GetStringParameter(job.Parameters, "algorithm") ?? "diamond-square",
                Roughness = GetFloatParameter(job.Parameters, "roughness", 0.55f, 0.1f, 1f),
                Octaves = GetIntParameter(job.Parameters, "octaves", 4, 1, 8)
            };
        }

        private static string? GetStringParameter(IReadOnlyDictionary<string, string> parameters, string key)
        {
            if (TryGetParameter(parameters, key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();

            return null;
        }

        private static int GetIntParameter(IReadOnlyDictionary<string, string> parameters, string key, int defaultValue, int min, int max)
        {
            if (TryGetParameter(parameters, key, out var value) && int.TryParse(value, out var parsed))
                return Math.Clamp(parsed, min, max);

            return defaultValue;
        }

        private static float GetFloatParameter(IReadOnlyDictionary<string, string> parameters, string key, float defaultValue, float min, float max)
        {
            if (TryGetParameter(parameters, key, out var value) && float.TryParse(value, out var parsed))
                return Math.Clamp(parsed, min, max);

            return defaultValue;
        }

        private static bool TryGetParameter(IReadOnlyDictionary<string, string> parameters, string key, out string value)
        {
            foreach (var entry in parameters)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        public GeneratorJobRecord? GetJob(string jobId)
        {
            return _jobs.TryGetValue(jobId, out var job) ? job : null;
        }

        public IReadOnlyCollection<GeneratorJobRecord> GetJobs(int take = 25)
        {
            return _jobs.Values
                .OrderByDescending(x => x.SubmittedAtUtc)
                .Take(Math.Max(1, take))
                .ToArray();
        }

        public GeneratorJobRecord? GetLatestJobForSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return null;

            return _jobs.Values
                .Where(x => string.Equals(x.SessionId, sessionId.Trim(), StringComparison.Ordinal))
                .OrderByDescending(x => x.SubmittedAtUtc)
                .FirstOrDefault();
        }
    }
}
#endif