namespace Authoritative.Diagnostics;

public sealed class DiagnosticLogWriteRequest
{
    public string Level { get; set; } = "Information";
    public string Category { get; set; } = "general";
    public string EventName { get; set; } = "diagnostic.event";
    public string Message { get; set; } = "";
    public string? Service { get; set; }
    public string? Environment { get; set; }
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
    public Dictionary<string, string>? Tags { get; set; }
    public Dictionary<string, string>? Properties { get; set; }
    public string? Payload { get; set; }
    public string? PayloadHash { get; set; }
    public bool IsRedacted { get; set; }
    public string RetentionClass { get; set; } = "debug";
}

public sealed class DiagnosticLogUpdateRequest
{
    public string? Level { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public Dictionary<string, string>? Properties { get; set; }
    public bool? IsRedacted { get; set; }
    public string? RetentionClass { get; set; }
}
