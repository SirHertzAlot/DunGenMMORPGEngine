namespace Authoritative.Diagnostics;

public sealed class DiagnosticLogQuery
{
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public string[]? Levels { get; set; }
    public string? Category { get; set; }
    public string? EventName { get; set; }
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? SessionId { get; set; }
    public string? ActorId { get; set; }
    public string? EntityId { get; set; }
    public string? CommandId { get; set; }
    public string? TextContains { get; set; }
    public string? SourceFile { get; set; }
    public string? SourceMember { get; set; }
    public int? SourceLine { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public Dictionary<string, string>? Properties { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
    public bool Descending { get; set; } = true;
}
