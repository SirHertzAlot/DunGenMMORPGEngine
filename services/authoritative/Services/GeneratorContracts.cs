#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;

namespace Authoritative.Services
{
    public sealed class GeneratorCapabilityDescriptor
    {
        public string GeneratorId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string InputMode { get; set; } = "yaml+parameters";
        public string OutputMode { get; set; } = "world-artifact";
        public bool RequiresActivePipeline { get; set; } = true;
    }

    public sealed class GeneratorJobRequest
    {
        public string GeneratorId { get; set; } = "world-pipeline";
        public string RequestedBy { get; set; } = "admin";
        public string? SessionId { get; set; }
        public string ConstraintsYaml { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int? SeedOverride { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class GeneratorJobRecord
    {
        public string JobId { get; set; } = string.Empty;
        public string GeneratorId { get; set; } = string.Empty;
        public string OutputMode { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string ConstraintsYaml { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int? SeedOverride { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public string Status { get; set; } = "pending";
        public string? Error { get; set; }
        public PipelineExecutionRecord? Execution { get; set; }
        public object? Result { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class UnitySessionBootstrapResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public bool HasWorld { get; set; }
        public string? ExecutionId { get; set; }
        public int RoomCount { get; set; }
        public int EnemyCount { get; set; }
        public int LootCount { get; set; }
        public string SnapshotUrl { get; set; } = string.Empty;
        public string StreamUrl { get; set; } = string.Empty;
        public string WebSocketUrl { get; set; } = string.Empty;
        public string TimelineUrl { get; set; } = string.Empty;
    }

    public sealed class UnitySessionTimelineEnvelope
    {
        public string SessionId { get; set; } = string.Empty;
        public IReadOnlyCollection<WorldSessionEvent> Events { get; set; } = Array.Empty<WorldSessionEvent>();
    }

    public sealed class UnitySessionWorldEnvelope
    {
        public string SessionId { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public GeneratedWorldArtifact World { get; set; } = new();
    }
}
#endif