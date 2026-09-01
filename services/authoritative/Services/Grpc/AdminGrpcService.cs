using Authoritative.Diagnostics;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.Extensions.Logging;

namespace Authoritative.Services.Grpc;

/// <summary>
/// Implementations of the secured MagicOnion admin gRPC surface. Handlers only
/// read through the trusted backend stores registered in DI; they perform no
/// direct mutation of player/item/world state and rely on the admin auth
/// interceptor for deny-by-default access control.
/// </summary>
public sealed class AdminGrpcService : ServiceBase<IAdminGrpcService>, IAdminGrpcService
{
    readonly IDiagnosticLogStore _diagnosticLogs;
    readonly IGeneratedItemStore _itemStore;
    readonly IAuthoritativeMetrics _metrics;
    readonly ILogger<AdminGrpcService> _log;

    public AdminGrpcService(
        IDiagnosticLogStore diagnosticLogs,
        IGeneratedItemStore itemStore,
        IAuthoritativeMetrics metrics,
        ILogger<AdminGrpcService> log)
    {
        _diagnosticLogs = diagnosticLogs;
        _itemStore = itemStore;
        _metrics = metrics;
        _log = log;
    }

    public async UnaryResult<GrpcHealthReply> GetHealth()
    {
        _metrics.MarkHeartbeat(DateTimeOffset.UtcNow);
        RecordDiagnostic("Information", "grpc.admin", "grpc.health", "gRPC health requested.");
        return await Task.FromResult(new GrpcHealthReply
        {
            Status = "ok",
            Service = "authoritative",
            TimestampUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    }

    public async UnaryResult<GrpcMetricsReply> ExportMetrics()
    {
        _metrics.MarkHeartbeat(DateTimeOffset.UtcNow);
        return await Task.FromResult(new GrpcMetricsReply { PrometheusText = _metrics.ExportPrometheus() });
    }

    public async UnaryResult<GrpcDiagnosticQueryReply> QueryDiagnostics(GrpcDiagnosticQueryRequest request)
    {
        request ??= new GrpcDiagnosticQueryRequest();
        var query = new DiagnosticLogQuery
        {
            Skip = Math.Max(0, request.Skip),
            Take = Math.Clamp(request.Take <= 0 ? 100 : request.Take, 1, 1000),
            Category = request.Category,
            EventName = request.EventName,
            SessionId = request.SessionId,
            EntityId = request.EntityId,
            TextContains = request.TextContains,
            Descending = request.Descending
        };

        if (!string.IsNullOrWhiteSpace(request.Level))
            query.Levels = new[] { request.Level };

        var result = _diagnosticLogs.Query(query);
        return await Task.FromResult(new GrpcDiagnosticQueryReply
        {
            Total = result.Total,
            Skip = result.Skip,
            Take = result.Take,
            Entries = result.Entries.Select(MapEntry).ToList()
        });
    }

    public async UnaryResult<GrpcGeneratedItemsReply> ListGeneratedItems(GrpcGeneratedItemsRequest request)
    {
        request ??= new GrpcGeneratedItemsRequest();
        var take = Math.Clamp(request.Take <= 0 ? 200 : request.Take, 1, 1000);
        var items = _itemStore.GetSnapshot().Take(take).ToList();
        return await Task.FromResult(new GrpcGeneratedItemsReply
        {
            Total = items.Count,
            Items = items.Select(MapItem).ToList()
        });
    }

    static GrpcDiagnosticEntry MapEntry(DiagnosticLogEntry entry)
    {
        return new GrpcDiagnosticEntry
        {
            Id = entry.Id,
            TimestampUnixMs = entry.ObservedAtUnixMs,
            Level = entry.Level,
            Category = entry.Category,
            EventName = entry.EventName,
            Message = entry.Message,
            EntityId = entry.EntityId,
            SessionId = entry.SessionId
        };
    }

    static GrpcGeneratedItem MapItem(PersistedGeneratedItem item)
    {
        return new GrpcGeneratedItem
        {
            Id = item.Item.Id,
            Type = item.Item.Type,
            Tier = item.Item.Tier,
            SavedAtUtc = item.SavedAtUtc.ToString("O"),
            Metadata = new Dictionary<string, string>(item.Metadata)
        };
    }

    void RecordDiagnostic(string level, string category, string eventName, string message)
    {
        try
        {
            _diagnosticLogs.Record(new DiagnosticLogWriteRequest
            {
                Level = level,
                Category = category,
                EventName = eventName,
                Message = message,
                Properties = new Dictionary<string, string>
                {
                    ["surface"] = "admin-grpc"
                }
            });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to write gRPC diagnostic log entry.");
        }
    }
}
