using System.Text.Json.Serialization;

namespace Authoritative.Diagnostics;

public sealed class DiagnosticLogEntry
{
    public string Id { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; }
    public long ObservedAtUnixMs { get; set; }
    public string Level { get; set; } = "Information";
    public string Category { get; set; } = "general";
    public string EventName { get; set; } = "diagnostic.event";
    public string Message { get; set; } = "";
    public string Service { get; set; } = "authoritative";
    public string Environment { get; set; } = "unknown";
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? SessionId { get; set; }
    public string? ActorId { get; set; }
    public string? EntityId { get; set; }
    public string? CommandId { get; set; }
    public string? SourceFile { get; set; }
    public string? SourceMember { get; set; }
    public int SourceLine { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? ExceptionStackTrace { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? PayloadHash { get; set; }
    public bool IsRedacted { get; set; }
    public string RetentionClass { get; set; } = "debug";

    [JsonIgnore]
    public string SourceLocation =>
        string.IsNullOrWhiteSpace(SourceFile)
            ? SourceMember ?? ""
            : $"{SourceFile}:{SourceLine}";
}
