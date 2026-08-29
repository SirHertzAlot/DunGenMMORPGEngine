namespace Authoritative.Diagnostics;

public sealed class DiagnosticLogQueryResult
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public IReadOnlyList<DiagnosticLogEntry> Entries { get; set; } = Array.Empty<DiagnosticLogEntry>();
}
