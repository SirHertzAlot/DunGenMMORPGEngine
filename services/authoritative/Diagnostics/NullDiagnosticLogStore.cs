using System.Runtime.CompilerServices;

namespace Authoritative.Diagnostics;

public sealed class NullDiagnosticLogStore : IDiagnosticLogStore
{
    public static NullDiagnosticLogStore Instance { get; } = new();

    NullDiagnosticLogStore()
    {
    }

    public DiagnosticLogEntry Record(
        DiagnosticLogWriteRequest request,
        Exception? exception = null,
        [CallerFilePath]
        string sourceFile = "",
        [CallerMemberName]
        string sourceMember = "",
        [CallerLineNumber]
        int sourceLine = 0)
    {
        var now = DateTimeOffset.UtcNow;
        return new DiagnosticLogEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = now,
            ObservedAtUnixMs = now.ToUnixTimeMilliseconds(),
            Level = request.Level,
            Category = request.Category,
            EventName = request.EventName,
            Message = request.Message,
            SourceFile = sourceFile,
            SourceMember = sourceMember,
            SourceLine = sourceLine,
            ExceptionType = exception?.GetType().FullName,
            ExceptionMessage = exception?.Message
        };
    }

    public DiagnosticLogEntry? Get(string id)
    {
        return null;
    }

    public DiagnosticLogQueryResult Query(DiagnosticLogQuery query)
    {
        return new DiagnosticLogQueryResult();
    }

    public bool TryUpdate(string id, DiagnosticLogUpdateRequest request, out DiagnosticLogEntry? updatedEntry)
    {
        updatedEntry = null;
        return false;
    }

    public bool TryDelete(string id)
    {
        return false;
    }
}
