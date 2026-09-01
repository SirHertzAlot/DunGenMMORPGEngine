#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using Authoritative.Domain;

namespace Authoritative.Services
{
    public enum PipelineRequestStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public sealed class PipelineCreateRequest
    {
        public string PipelineName { get; set; } = string.Empty;
        public int DungeonLevel { get; set; } = 1;
        public int Width { get; set; } = 80;
        public int Height { get; set; } = 24;
        public int EnemyCount { get; set; } = 5;
        public int LootCount { get; set; } = 3;
        public int? Seed { get; set; }
        public string Purpose { get; set; } = "ecs-generation";
        public string SubmittedBy { get; set; } = "";
    }

    public sealed class PipelineApprovalRequest
    {
        public string ApprovedBy { get; set; } = "";
        public int? OverrideSeed { get; set; }
    }

    public sealed class PipelineRejectionRequest
    {
        public string RejectedBy { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    public sealed class PipelineRequestRecord
    {
        public string RequestId { get; set; } = string.Empty;
        public PipelineRequestStatus Status { get; set; }
        public PipelineCreateRequest RequestedConfig { get; set; } = new();
        public string SubmittedBy { get; set; } = string.Empty;
        public string SubmittedFrom { get; set; } = string.Empty;
        public DateTime SubmittedAtUtc { get; set; }
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewReason { get; set; }
        public string? GeneratedDefinitionPath { get; set; }
        public string? GeneratedDefinitionHash { get; set; }
    }

    public sealed class PipelineStepDefinition
    {
        public string Stage { get; set; } = string.Empty;
        public string EcsSystem { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class EcsGenerationConfig
    {
        public int DungeonLevel { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int EnemyCount { get; set; }
        public int LootCount { get; set; }
        public int Seed { get; set; }
    }

    public sealed class PipelineDefinition
    {
        public string PipelineId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
        public DateTime ApprovedAtUtc { get; set; }
        public EcsGenerationConfig Ecs { get; set; } = new();
        public List<PipelineStepDefinition> Steps { get; set; } = new();
    }

    public sealed class PipelineRuntimeSnapshot
    {
        public bool IsLoaded { get; set; }
        public string? ActiveDefinitionPath { get; set; }
        public DateTime? LastLoadedAtUtc { get; set; }
        public string? DefinitionHash { get; set; }
        public PipelineDefinition? ActiveDefinition { get; set; }
    }

    public sealed class PipelineExecutionRequest
    {
        public string RequestedBy { get; set; } = "admin";
        public string Notes { get; set; } = "";
        public string? SessionId { get; set; }
        public string ConstraintsYaml { get; set; } = string.Empty;
    }

    public sealed class PipelineStepExecutionResult
    {
        public string Stage { get; set; } = string.Empty;
        public string EcsSystem { get; set; } = string.Empty;
        public string Status { get; set; } = "completed";
        public string Summary { get; set; } = string.Empty;
    }

    public sealed class WorldRoom
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class WorldEnemy
    {
        public int Id { get; set; }
        public string Archetype { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Level { get; set; }
    }

    public sealed class WorldLoot
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
    }

    public sealed class GeneratedWorldArtifact
    {
        public int Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int DungeonLevel { get; set; }
        public List<WorldRoom> Rooms { get; set; } = new();
        public List<WorldEnemy> Enemies { get; set; } = new();
        public List<WorldLoot> Loot { get; set; } = new();
        public GeneratedTerrainMesh TerrainMesh { get; set; } = new();
    }

    public sealed class PipelineExecutionRecord
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string PipelineId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public string ArtifactPath { get; set; } = string.Empty;
        public string Status { get; set; } = "completed";
        public List<PipelineStepExecutionResult> StepResults { get; set; } = new();
        public GeneratedWorldArtifact World { get; set; } = new();
    }

    // Ingest payload for POST /admin/world/sessions/{sessionId}/ingest
    public sealed class WorldIngestRequest
    {
        public string? ExecutionId { get; set; }
        public string? PipelineId { get; set; }
        public string? Notes { get; set; }
        public GeneratedWorldArtifact World { get; set; } = new();
    }

    public sealed class WorldSessionEventIngestRequest
    {
        public string EventType { get; set; } = "custom";
        public string Category { get; set; } = "simulation";
        public uint Frame { get; set; }
        public string EntityId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, string> Data { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class WorldSessionEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public uint Frame { get; set; }
        public string EntityId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
        public Dictionary<string, string> Data { get; set; } = new(StringComparer.Ordinal);
    }
}
#endif
