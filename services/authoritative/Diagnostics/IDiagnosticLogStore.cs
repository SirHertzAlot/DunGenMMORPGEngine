using System.Runtime.CompilerServices;

namespace Authoritative.Diagnostics;

public interface IDiagnosticLogStore
{
    DiagnosticLogEntry Record(
        DiagnosticLogWriteRequest request,
        Exception? exception = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string sourceMember = "",
        [CallerLineNumber] int sourceLine = 0);

    DiagnosticLogEntry? Get(string id);
    DiagnosticLogQueryResult Query(DiagnosticLogQuery query);
    bool TryUpdate(string id, DiagnosticLogUpdateRequest request, out DiagnosticLogEntry? updatedEntry);
    bool TryDelete(string id);
}
