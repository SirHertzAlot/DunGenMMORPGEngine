using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Authoritative.Diagnostics;

public sealed class DiagnosticLogStore : IDiagnosticLogStore
{
    const int MaxQueryTake = 1000;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    readonly object _gate = new();
    readonly Dictionary<string, DiagnosticLogEntry> _entries = new(StringComparer.Ordinal);
    readonly string _jsonlPath;
    readonly string _serviceName;
    readonly string _environmentName;

    public DiagnosticLogStore(string? dataDirectory = null, string serviceName = "authoritative")
    {
        var root = string.IsNullOrWhiteSpace(dataDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : dataDirectory;

        Directory.CreateDirectory(root);
        _jsonlPath = Path.Combine(root, "diagnostic-events.jsonl");
        _serviceName = serviceName;
        _environmentName =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "unknown";

        LoadExistingEntries();
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
        var activity = Activity.Current;
        var entry = new DiagnosticLogEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = now,
            ObservedAtUnixMs = now.ToUnixTimeMilliseconds(),
            Level = NormalizeLevel(request.Level),
            Category = ValueOrDefault(request.Category, "general"),
            EventName = ValueOrDefault(request.EventName, "diagnostic.event"),
            Message = request.Message ?? "",
            Service = ValueOrDefault(request.Service, _serviceName),
            Environment = ValueOrDefault(request.Environment, _environmentName),
            CorrelationId = EmptyToNull(request.CorrelationId),
            TraceId = EmptyToNull(request.TraceId) ?? activity?.TraceId.ToString(),
            SpanId = EmptyToNull(request.SpanId) ?? activity?.SpanId.ToString(),
            SessionId = EmptyToNull(request.SessionId),
            ActorId = EmptyToNull(request.ActorId),
            EntityId = EmptyToNull(request.EntityId),
            CommandId = EmptyToNull(request.CommandId),
            SourceFile = EmptyToNull(request.SourceFile) ?? EmptyToNull(sourceFile),
            SourceMember = EmptyToNull(request.SourceMember) ?? EmptyToNull(sourceMember),
            SourceLine = request.SourceLine > 0 ? request.SourceLine : Math.Max(0, sourceLine),
            ExceptionType = exception?.GetType().FullName,
            ExceptionMessage = exception?.Message,
            ExceptionStackTrace = exception?.ToString(),
            Tags = CloneDictionary(request.Tags),
            Properties = CloneDictionary(request.Properties),
            PayloadHash = EmptyToNull(request.PayloadHash) ?? HashPayload(request.Payload),
            IsRedacted = request.IsRedacted,
            RetentionClass = ValueOrDefault(request.RetentionClass, "debug")
        };

        lock (_gate)
        {
            _entries[entry.Id] = Clone(entry);
            File.AppendAllText(_jsonlPath, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
        }

        return Clone(entry);
    }

    public DiagnosticLogEntry? Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        lock (_gate)
        {
            return _entries.TryGetValue(id, out var entry) ? Clone(entry) : null;
        }
    }

    public DiagnosticLogQueryResult Query(DiagnosticLogQuery query)
    {
        query ??= new DiagnosticLogQuery();
        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take <= 0 ? 100 : query.Take, 1, MaxQueryTake);

        List<DiagnosticLogEntry> snapshot;
        lock (_gate)
        {
            snapshot = _entries.Values.Select(Clone).ToList();
        }

        var results = snapshot.AsEnumerable();

        if (query.FromUtc.HasValue)
            results = results.Where(e => e.TimestampUtc >= query.FromUtc.Value);

        if (query.ToUtc.HasValue)
            results = results.Where(e => e.TimestampUtc <= query.ToUtc.Value);

        if (query.Levels is { Length: > 0 })
        {
            var levels = new HashSet<string>(query.Levels.Select(NormalizeLevel), StringComparer.OrdinalIgnoreCase);
            results = results.Where(e => levels.Contains(e.Level));
        }

        results = FilterExact(results, query.Category, e => e.Category);
        results = FilterExact(results, query.EventName, e => e.EventName);
        results = FilterExact(results, query.CorrelationId, e => e.CorrelationId);
        results = FilterExact(results, query.TraceId, e => e.TraceId);
        results = FilterExact(results, query.SpanId, e => e.SpanId);
        results = FilterExact(results, query.SessionId, e => e.SessionId);
        results = FilterExact(results, query.ActorId, e => e.ActorId);
        results = FilterExact(results, query.EntityId, e => e.EntityId);
        results = FilterExact(results, query.CommandId, e => e.CommandId);
        results = FilterContains(results, query.SourceFile, e => e.SourceFile);
        results = FilterExact(results, query.SourceMember, e => e.SourceMember);

        if (query.SourceLine.HasValue)
            results = results.Where(e => e.SourceLine == query.SourceLine.Value);

        if (!string.IsNullOrWhiteSpace(query.TextContains))
            results = results.Where(e => ContainsText(e, query.TextContains));

        if (query.Tags is { Count: > 0 })
            results = results.Where(e => DictionaryContains(e.Tags, query.Tags));

        if (query.Properties is { Count: > 0 })
            results = results.Where(e => DictionaryContains(e.Properties, query.Properties));

        results = query.Descending
            ? results.OrderByDescending(e => e.TimestampUtc).ThenByDescending(e => e.Id)
            : results.OrderBy(e => e.TimestampUtc).ThenBy(e => e.Id);

        var filtered = results.ToList();

        return new DiagnosticLogQueryResult
        {
            Total = filtered.Count,
            Skip = skip,
            Take = take,
            Entries = filtered.Skip(skip).Take(take).ToList()
        };
    }

    public bool TryUpdate(string id, DiagnosticLogUpdateRequest request, out DiagnosticLogEntry? updatedEntry)
    {
        updatedEntry = null;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        lock (_gate)
        {
            if (!_entries.TryGetValue(id, out var existing))
                return false;

            var updated = Clone(existing);
            if (!string.IsNullOrWhiteSpace(request.Level))
                updated.Level = NormalizeLevel(request.Level);

            if (request.Message != null)
                updated.Message = request.Message;

            if (request.Tags != null)
                updated.Tags = CloneDictionary(request.Tags);

            if (request.Properties != null)
                updated.Properties = CloneDictionary(request.Properties);

            if (request.IsRedacted.HasValue)
                updated.IsRedacted = request.IsRedacted.Value;

            if (!string.IsNullOrWhiteSpace(request.RetentionClass))
                updated.RetentionClass = request.RetentionClass;

            _entries[id] = updated;
            RewriteJsonlFile();
            updatedEntry = Clone(updated);
            return true;
        }
    }

    public bool TryDelete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        lock (_gate)
        {
            if (!_entries.Remove(id))
                return false;

            RewriteJsonlFile();
            return true;
        }
    }

    void LoadExistingEntries()
    {
        if (!File.Exists(_jsonlPath))
            return;

        foreach (var line in File.ReadLines(_jsonlPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var entry = JsonSerializer.Deserialize<DiagnosticLogEntry>(line, JsonOptions);
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                    continue;

                _entries[entry.Id] = NormalizeEntry(entry);
            }
            catch
            {
                // Malformed diagnostic lines are ignored so one bad write does not blind the service.
            }
        }
    }

    void RewriteJsonlFile()
    {
        var lines = _entries.Values
            .OrderBy(e => e.TimestampUtc)
            .ThenBy(e => e.Id)
            .Select(e => JsonSerializer.Serialize(e, JsonOptions));

        File.WriteAllLines(_jsonlPath, lines);
    }

    static DiagnosticLogEntry NormalizeEntry(DiagnosticLogEntry entry)
    {
        entry.Level = NormalizeLevel(entry.Level);
        entry.Category = ValueOrDefault(entry.Category, "general");
        entry.EventName = ValueOrDefault(entry.EventName, "diagnostic.event");
        entry.Service = ValueOrDefault(entry.Service, "authoritative");
        entry.Environment = ValueOrDefault(entry.Environment, "unknown");
        entry.RetentionClass = ValueOrDefault(entry.RetentionClass, "debug");
        entry.Tags = CloneDictionary(entry.Tags);
        entry.Properties = CloneDictionary(entry.Properties);
        return entry;
    }

    static DiagnosticLogEntry Clone(DiagnosticLogEntry entry)
    {
        return new DiagnosticLogEntry
        {
            Id = entry.Id,
            TimestampUtc = entry.TimestampUtc,
            ObservedAtUnixMs = entry.ObservedAtUnixMs,
            Level = entry.Level,
            Category = entry.Category,
            EventName = entry.EventName,
            Message = entry.Message,
            Service = entry.Service,
            Environment = entry.Environment,
            CorrelationId = entry.CorrelationId,
            TraceId = entry.TraceId,
            SpanId = entry.SpanId,
            SessionId = entry.SessionId,
            ActorId = entry.ActorId,
            EntityId = entry.EntityId,
            CommandId = entry.CommandId,
            SourceFile = entry.SourceFile,
            SourceMember = entry.SourceMember,
            SourceLine = entry.SourceLine,
            ExceptionType = entry.ExceptionType,
            ExceptionMessage = entry.ExceptionMessage,
            ExceptionStackTrace = entry.ExceptionStackTrace,
            Tags = CloneDictionary(entry.Tags),
            Properties = CloneDictionary(entry.Properties),
            PayloadHash = entry.PayloadHash,
            IsRedacted = entry.IsRedacted,
            RetentionClass = entry.RetentionClass
        };
    }

    static Dictionary<string, string> CloneDictionary(IDictionary<string, string>? source)
    {
        return source == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
    }

    static IEnumerable<DiagnosticLogEntry> FilterExact(
        IEnumerable<DiagnosticLogEntry> entries,
        string? expected,
        Func<DiagnosticLogEntry, string?> selector)
    {
        return string.IsNullOrWhiteSpace(expected)
            ? entries
            : entries.Where(e => string.Equals(selector(e), expected, StringComparison.OrdinalIgnoreCase));
    }

    static IEnumerable<DiagnosticLogEntry> FilterContains(
        IEnumerable<DiagnosticLogEntry> entries,
        string? expected,
        Func<DiagnosticLogEntry, string?> selector)
    {
        return string.IsNullOrWhiteSpace(expected)
            ? entries
            : entries.Where(e => selector(e)?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true);
    }

    static bool ContainsText(DiagnosticLogEntry entry, string text)
    {
        return Contains(entry.Message, text)
            || Contains(entry.Category, text)
            || Contains(entry.EventName, text)
            || Contains(entry.ExceptionMessage, text)
            || entry.Tags.Any(kvp => Contains(kvp.Key, text) || Contains(kvp.Value, text))
            || entry.Properties.Any(kvp => Contains(kvp.Key, text) || Contains(kvp.Value, text));
    }

    static bool DictionaryContains(
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> expected)
    {
        foreach (var (key, expectedValue) in expected)
        {
            if (!values.TryGetValue(key, out var actualValue))
                return false;

            if (!string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    static bool Contains(string? value, string expected)
    {
        return value?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;
    }

    static string NormalizeLevel(string? level)
    {
        return (level ?? "").Trim().ToLowerInvariant() switch
        {
            "trace" => "Trace",
            "debug" => "Debug",
            "warn" or "warning" => "Warning",
            "error" => "Error",
            "critical" or "fatal" => "Critical",
            _ => "Information"
        };
    }

    static string ValueOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    static string? HashPayload(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
            return null;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
