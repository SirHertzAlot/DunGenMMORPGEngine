using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
#if !UNITY_5_3_OR_NEWER
using System.Diagnostics;
using Newtonsoft.Json;
#endif

#nullable enable

namespace Authoritative.Diagnostics
{
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
}

public sealed class DiagnosticLogWriteRequest
{
    public string Level { get; set; } = "Information";
    public string Category { get; set; } = "general";
    public string EventName { get; set; } = "diagnostic.event";
    public string Message { get; set; } = "";
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

public sealed class DiagnosticLogQueryResult
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public IReadOnlyList<DiagnosticLogEntry> Entries { get; set; } = Array.Empty<DiagnosticLogEntry>();
}

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

public sealed class DiagnosticLogStore : IDiagnosticLogStore
{
    const int MaxQueryTake = 1000;

    readonly object _gate = new();
    readonly Dictionary<string, DiagnosticLogEntry> _entries = new(StringComparer.Ordinal);
    readonly string _jsonlPath;
    readonly string _environment;

    public DiagnosticLogStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _jsonlPath = Path.Combine(dataDirectory, "diagnostic-events.jsonl");
        _environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "local";
        LoadExistingEntries();
    }

    public DiagnosticLogEntry Record(
        DiagnosticLogWriteRequest request,
        Exception? exception = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string sourceMember = "",
        [CallerLineNumber] int sourceLine = 0)
    {
        var now = DateTimeOffset.UtcNow;
#if !UNITY_5_3_OR_NEWER
        var activity = Activity.Current;
#endif
        var entry = new DiagnosticLogEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = now,
            ObservedAtUnixMs = now.ToUnixTimeMilliseconds(),
            Level = NormalizeLevel(request.Level),
            Category = ValueOrDefault(request.Category, "general"),
            EventName = ValueOrDefault(request.EventName, "diagnostic.event"),
            Message = request.Message ?? "",
            Environment = _environment,
            CorrelationId = EmptyToNull(request.CorrelationId),
#if !UNITY_5_3_OR_NEWER
            TraceId = EmptyToNull(request.TraceId) ?? activity?.TraceId.ToString(),
            SpanId = EmptyToNull(request.SpanId) ?? activity?.SpanId.ToString(),
#else
            TraceId = EmptyToNull(request.TraceId),
            SpanId = EmptyToNull(request.SpanId),
#endif
            SessionId = EmptyToNull(request.SessionId),
            ActorId = EmptyToNull(request.ActorId),
            EntityId = EmptyToNull(request.EntityId),
            CommandId = EmptyToNull(request.CommandId),
            SourceFile = EmptyToNull(request.SourceFile) ?? EmptyToNull(sourceFile),
            SourceMember = EmptyToNull(request.SourceMember) ?? EmptyToNull(sourceMember),
            SourceLine = request.SourceLine > 0 ? request.SourceLine : sourceLine,
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
            File.AppendAllText(_jsonlPath, SerializeForStorage(entry) + Environment.NewLine);
        }

        return Clone(entry);
    }

    public DiagnosticLogEntry? Get(string id)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(id, out var entry) ? Clone(entry) : null;
        }
    }

    public DiagnosticLogQueryResult Query(DiagnosticLogQuery query)
    {
        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take <= 0 ? 100 : query.Take, 1, MaxQueryTake);
        List<DiagnosticLogEntry> snapshot;

        lock (_gate)
        {
            snapshot = _entries.Values.Select(Clone).ToList();
        }

        var filtered = snapshot.AsEnumerable();
        if (query.FromUtc.HasValue) filtered = filtered.Where(e => e.TimestampUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) filtered = filtered.Where(e => e.TimestampUtc <= query.ToUtc.Value);
        if (query.Levels is { Length: > 0 })
        {
            var levels = new HashSet<string>(query.Levels.Select(NormalizeLevel), StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(e => levels.Contains(e.Level));
        }

        filtered = FilterExact(filtered, query.Category, e => e.Category);
        filtered = FilterExact(filtered, query.EventName, e => e.EventName);
        filtered = FilterExact(filtered, query.CorrelationId, e => e.CorrelationId);
        filtered = FilterExact(filtered, query.TraceId, e => e.TraceId);
        filtered = FilterExact(filtered, query.SpanId, e => e.SpanId);
        filtered = FilterExact(filtered, query.SessionId, e => e.SessionId);
        filtered = FilterExact(filtered, query.ActorId, e => e.ActorId);
        filtered = FilterExact(filtered, query.EntityId, e => e.EntityId);
        filtered = FilterExact(filtered, query.CommandId, e => e.CommandId);
        filtered = FilterContains(filtered, query.SourceFile, e => e.SourceFile);
        filtered = FilterExact(filtered, query.SourceMember, e => e.SourceMember);
        if (query.SourceLine.HasValue) filtered = filtered.Where(e => e.SourceLine == query.SourceLine.Value);
        if (!string.IsNullOrWhiteSpace(query.TextContains)) filtered = filtered.Where(e => ContainsText(e, query.TextContains));
        if (query.Tags is { Count: > 0 }) filtered = filtered.Where(e => DictionaryContains(e.Tags, query.Tags));
        if (query.Properties is { Count: > 0 }) filtered = filtered.Where(e => DictionaryContains(e.Properties, query.Properties));

        filtered = query.Descending
            ? filtered.OrderByDescending(e => e.TimestampUtc).ThenByDescending(e => e.Id)
            : filtered.OrderBy(e => e.TimestampUtc).ThenBy(e => e.Id);

        var results = filtered.ToList();
        return new DiagnosticLogQueryResult
        {
            Total = results.Count,
            Skip = skip,
            Take = take,
            Entries = results.Skip(skip).Take(take).ToList()
        };
    }

    public bool TryUpdate(string id, DiagnosticLogUpdateRequest request, out DiagnosticLogEntry? updatedEntry)
    {
        updatedEntry = null;
        lock (_gate)
        {
            if (!_entries.TryGetValue(id, out var existing)) return false;

            var updated = Clone(existing);
            if (!string.IsNullOrWhiteSpace(request.Level)) updated.Level = NormalizeLevel(request.Level);
            if (request.Message != null) updated.Message = request.Message;
            if (request.Tags != null) updated.Tags = CloneDictionary(request.Tags);
            if (request.Properties != null) updated.Properties = CloneDictionary(request.Properties);
            if (request.IsRedacted.HasValue) updated.IsRedacted = request.IsRedacted.Value;
            if (!string.IsNullOrWhiteSpace(request.RetentionClass)) updated.RetentionClass = request.RetentionClass;
            _entries[id] = updated;
            RewriteJsonlFile();
            updatedEntry = Clone(updated);
            return true;
        }
    }

    public bool TryDelete(string id)
    {
        lock (_gate)
        {
            if (!_entries.Remove(id)) return false;
            RewriteJsonlFile();
            return true;
        }
    }

    void LoadExistingEntries()
    {
        if (!File.Exists(_jsonlPath)) return;
        foreach (var line in File.ReadLines(_jsonlPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = DeserializeFromStorage(line);
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
                entry.Tags = CloneDictionary(entry.Tags);
                entry.Properties = CloneDictionary(entry.Properties);
                _entries[entry.Id] = entry;
            }
            catch
            {
                // Keep loading even if one line is malformed.
            }
        }
    }

    void RewriteJsonlFile()
    {
        File.WriteAllLines(_jsonlPath, _entries.Values
            .OrderBy(e => e.TimestampUtc)
            .Select(SerializeForStorage));
    }

    static string SerializeForStorage(DiagnosticLogEntry entry)
    {
#if !UNITY_5_3_OR_NEWER
        return JsonConvert.SerializeObject(entry, Formatting.None);
#else
        var parts = new[]
        {
            EncodeField(entry.Id),
            EncodeField(entry.ObservedAtUnixMs.ToString()),
            EncodeField(entry.Level),
            EncodeField(entry.Category),
            EncodeField(entry.EventName),
            EncodeField(entry.Message),
            EncodeField(entry.SessionId ?? string.Empty),
            EncodeField(entry.CorrelationId ?? string.Empty),
            EncodeField(entry.TraceId ?? string.Empty),
            EncodeField(entry.SpanId ?? string.Empty),
            EncodeField(entry.PayloadHash ?? string.Empty),
            EncodeField(entry.IsRedacted ? "1" : "0"),
            EncodeField(entry.RetentionClass),
        };

        return string.Join("|", parts);
#endif
    }

    static DiagnosticLogEntry? DeserializeFromStorage(string line)
    {
#if !UNITY_5_3_OR_NEWER
        return JsonConvert.DeserializeObject<DiagnosticLogEntry>(line);
#else
        var parts = line.Split('|');
        if (parts.Length < 13)
            return null;

        var unixMsText = DecodeField(parts[1]);
        if (!long.TryParse(unixMsText, out var unixMs))
            unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new DiagnosticLogEntry
        {
            Id = DecodeField(parts[0]),
            ObservedAtUnixMs = unixMs,
            TimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(unixMs),
            Level = ValueOrDefault(DecodeField(parts[2]), "Information"),
            Category = ValueOrDefault(DecodeField(parts[3]), "general"),
            EventName = ValueOrDefault(DecodeField(parts[4]), "diagnostic.event"),
            Message = DecodeField(parts[5]),
            SessionId = EmptyToNull(DecodeField(parts[6])),
            CorrelationId = EmptyToNull(DecodeField(parts[7])),
            TraceId = EmptyToNull(DecodeField(parts[8])),
            SpanId = EmptyToNull(DecodeField(parts[9])),
            PayloadHash = EmptyToNull(DecodeField(parts[10])),
            IsRedacted = DecodeField(parts[11]) == "1",
            RetentionClass = ValueOrDefault(DecodeField(parts[12]), "debug"),
        };
#endif
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

    static IEnumerable<DiagnosticLogEntry> FilterExact(IEnumerable<DiagnosticLogEntry> entries, string? expected, Func<DiagnosticLogEntry, string?> selector)
    {
        return string.IsNullOrWhiteSpace(expected) ? entries : entries.Where(e => string.Equals(selector(e), expected, StringComparison.OrdinalIgnoreCase));
    }

    static IEnumerable<DiagnosticLogEntry> FilterContains(IEnumerable<DiagnosticLogEntry> entries, string? expected, Func<DiagnosticLogEntry, string?> selector)
    {
        return string.IsNullOrWhiteSpace(expected) ? entries : entries.Where(e => selector(e)?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true);
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

    static bool DictionaryContains(IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> expected)
    {
        return expected.All(kvp => values.TryGetValue(kvp.Key, out var actual) && string.Equals(actual, kvp.Value, StringComparison.OrdinalIgnoreCase));
    }

    static bool Contains(string? value, string expected) => value?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;

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

    static string ValueOrDefault(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static string? HashPayload(string? payload)
    {
        if (string.IsNullOrEmpty(payload)) return null;
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    static string EncodeField(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    static string DecodeField(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            return string.Empty;

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch
        {
            return string.Empty;
        }
    }
}
}