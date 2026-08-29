using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Authoritative.Services;

public interface IAuthoritativeMetrics
{
    void RecordCommandReceived(string actionType);
    void RecordCommandSucceeded(string actionType);
    void RecordCommandDuplicate(string actionType);
    void RecordCommandValidationFailure(string reason);
    void RecordCommandProcessingFailure(string actionType);
    void RecordDeadLetterPublished();
    void RecordDeadLetterFailed();
    void RecordAckLatency(TimeSpan latency);
    void MarkHeartbeat(DateTimeOffset timestamp);
    string ExportPrometheus();
}

public sealed class AuthoritativeMetrics : IAuthoritativeMetrics
{
    static readonly double[] AckLatencyBuckets =
    {
        0.005,
        0.01,
        0.025,
        0.05,
        0.1,
        0.25,
        0.5,
        1,
        2.5,
        5,
        10
    };

    readonly ConcurrentDictionary<string, long> _commandReceived = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, long> _commandSucceeded = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, long> _commandDuplicate = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, long> _validationFailures = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, long> _processingFailures = new(StringComparer.Ordinal);
    readonly long[] _ackLatencyBucketCounts = new long[AckLatencyBuckets.Length];
    long _deadLettersPublished;
    long _deadLettersFailed;
    long _ackLatencyCount;
    double _ackLatencySumSeconds;
    long _heartbeatUnixSeconds;

    public void RecordCommandReceived(string actionType) => Increment(_commandReceived, NormalizeLabel(actionType, "unknown"));

    public void RecordCommandSucceeded(string actionType) => Increment(_commandSucceeded, NormalizeLabel(actionType, "unknown"));

    public void RecordCommandDuplicate(string actionType) => Increment(_commandDuplicate, NormalizeLabel(actionType, "unknown"));

    public void RecordCommandValidationFailure(string reason) => Increment(_validationFailures, NormalizeLabel(reason, "invalid"));

    public void RecordCommandProcessingFailure(string actionType) => Increment(_processingFailures, NormalizeLabel(actionType, "unknown"));

    public void RecordDeadLetterPublished() => Interlocked.Increment(ref _deadLettersPublished);

    public void RecordDeadLetterFailed() => Interlocked.Increment(ref _deadLettersFailed);

    public void RecordAckLatency(TimeSpan latency)
    {
        var seconds = Math.Max(0, latency.TotalSeconds);
        for (int i = 0; i < AckLatencyBuckets.Length; i++)
        {
            if (seconds <= AckLatencyBuckets[i])
                Interlocked.Increment(ref _ackLatencyBucketCounts[i]);
        }

        Interlocked.Increment(ref _ackLatencyCount);
        lock (this)
        {
            _ackLatencySumSeconds += seconds;
        }
    }

    public void MarkHeartbeat(DateTimeOffset timestamp)
    {
        Interlocked.Exchange(ref _heartbeatUnixSeconds, timestamp.ToUnixTimeSeconds());
    }

    public string ExportPrometheus()
    {
        var builder = new StringBuilder();
        AppendHeader(builder, "authoritative_commands_received_total", "Total authoritative commands received by action type.", "counter");
        AppendLabeledCounters(builder, "authoritative_commands_received_total", "action_type", _commandReceived);

        AppendHeader(builder, "authoritative_commands_succeeded_total", "Total authoritative commands processed successfully by action type.", "counter");
        AppendLabeledCounters(builder, "authoritative_commands_succeeded_total", "action_type", _commandSucceeded);

        AppendHeader(builder, "authoritative_commands_duplicate_total", "Total duplicate authoritative commands acknowledged without mutation by action type.", "counter");
        AppendLabeledCounters(builder, "authoritative_commands_duplicate_total", "action_type", _commandDuplicate);

        AppendHeader(builder, "authoritative_command_validation_failures_total", "Total authoritative command validation failures by reason.", "counter");
        AppendLabeledCounters(builder, "authoritative_command_validation_failures_total", "reason", _validationFailures);

        AppendHeader(builder, "authoritative_command_processing_failures_total", "Total authoritative command processing failures by action type.", "counter");
        AppendLabeledCounters(builder, "authoritative_command_processing_failures_total", "action_type", _processingFailures);

        AppendHeader(builder, "authoritative_dead_letters_total", "Total authoritative command dead-letter attempts by status.", "counter");
        AppendCounter(builder, "authoritative_dead_letters_total", "status", "published", Interlocked.Read(ref _deadLettersPublished));
        AppendCounter(builder, "authoritative_dead_letters_total", "status", "failed", Interlocked.Read(ref _deadLettersFailed));

        AppendHeader(builder, "authoritative_ack_latency_seconds", "Latency from command receipt to acknowledgement.", "histogram");
        long cumulative = 0;
        for (int i = 0; i < AckLatencyBuckets.Length; i++)
        {
            cumulative = Interlocked.Read(ref _ackLatencyBucketCounts[i]);
            builder.Append(CultureInfo.InvariantCulture, $"authoritative_ack_latency_seconds_bucket{{le=\"{AckLatencyBuckets[i]}\"}} {cumulative}\n");
        }

        builder.Append(CultureInfo.InvariantCulture, $"authoritative_ack_latency_seconds_bucket{{le=\"+Inf\"}} {Interlocked.Read(ref _ackLatencyCount)}\n");
        builder.Append(CultureInfo.InvariantCulture, $"authoritative_ack_latency_seconds_sum {GetAckLatencySumSeconds()}\n");
        builder.Append(CultureInfo.InvariantCulture, $"authoritative_ack_latency_seconds_count {Interlocked.Read(ref _ackLatencyCount)}\n");

        AppendHeader(builder, "authoritative_service_heartbeat_timestamp_seconds", "Unix timestamp of the latest authoritative service heartbeat.", "gauge");
        builder.Append(CultureInfo.InvariantCulture, $"authoritative_service_heartbeat_timestamp_seconds {Interlocked.Read(ref _heartbeatUnixSeconds)}\n");
        return builder.ToString();
    }

    static void Increment(ConcurrentDictionary<string, long> counters, string label)
    {
        counters.AddOrUpdate(label, 1, (_, current) => current + 1);
    }

    static string NormalizeLabel(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    static void AppendHeader(StringBuilder builder, string name, string help, string type)
    {
        builder.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
        builder.Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');
    }

    static void AppendLabeledCounters(StringBuilder builder, string name, string labelName, ConcurrentDictionary<string, long> counters)
    {
        foreach (var pair in counters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            AppendCounter(builder, name, labelName, pair.Key, pair.Value);
    }

    static void AppendCounter(StringBuilder builder, string name, string labelName, string labelValue, long value)
    {
        builder.Append(name)
            .Append('{')
            .Append(labelName)
            .Append("=\"")
            .Append(EscapeLabelValue(labelValue))
            .Append("\"} ")
            .Append(CultureInfo.InvariantCulture, $"{value}\n");
    }

    static string EscapeLabelValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\"", "\\\"");
    }

    double GetAckLatencySumSeconds()
    {
        lock (this)
        {
            return _ackLatencySumSeconds;
        }
    }
}

public sealed class NullAuthoritativeMetrics : IAuthoritativeMetrics
{
    public static readonly NullAuthoritativeMetrics Instance = new();

    NullAuthoritativeMetrics()
    {
    }

    public void RecordCommandReceived(string actionType) { }
    public void RecordCommandSucceeded(string actionType) { }
    public void RecordCommandDuplicate(string actionType) { }
    public void RecordCommandValidationFailure(string reason) { }
    public void RecordCommandProcessingFailure(string actionType) { }
    public void RecordDeadLetterPublished() { }
    public void RecordDeadLetterFailed() { }
    public void RecordAckLatency(TimeSpan latency) { }
    public void MarkHeartbeat(DateTimeOffset timestamp) { }
    public string ExportPrometheus() => "";
}
