#if !UNITY_5_3_OR_NEWER
using System;
using System.Linq;
using System.Threading.Tasks;
using Authoritative.Diagnostics;
using Authoritative.Services;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.Extensions.Logging;

namespace Authoritative.Services.Grpc
{
    /// <summary>
    /// Implementation of the admin gRPC surface over the existing diagnostic log and
    /// generated-item stores. Additive; the existing HTTP admin endpoints are unchanged.
    /// ServiceBase binds the interface contract to this concrete service.
    /// </summary>
    public sealed class AdminGrpcService : ServiceBase<IAdminGrpcService>, IAdminGrpcService
    {
        const int MaxTake = 1000;

        readonly IDiagnosticLogStore _logs;
        readonly IGeneratedItemStore _items;
        readonly ILogger<AdminGrpcService> _logger;

        public AdminGrpcService(
            IDiagnosticLogStore logs,
            IGeneratedItemStore items,
            ILogger<AdminGrpcService> logger)
        {
            _logs = logs;
            _items = items;
            _logger = logger;
        }

        public async UnaryResult<GrpcHealthReply> GetHealth()
        {
            return await Task.FromResult(new GrpcHealthReply
            {
                Status = "ok",
                Service = "authoritative",
                Version = "1.0.0"
            });
        }

        public async UnaryResult<GrpcDiagnosticQueryReply> QueryDiagnostics(GrpcDiagnosticQueryRequest request)
        {
            request ??= new GrpcDiagnosticQueryRequest();
            var query = new DiagnosticLogQuery
            {
                Levels = string.IsNullOrWhiteSpace(request.Level) ? null : new[] { request.Level! },
                SessionId = NullIfEmpty(request.SessionId),
                Category = NullIfEmpty(request.Category),
                EventName = NullIfEmpty(request.EventName),
                TextContains = NullIfEmpty(request.TextContains),
                Skip = Math.Max(0, request.Skip),
                Take = Math.Clamp(request.Take <= 0 ? 100 : request.Take, 1, MaxTake),
                Descending = request.Descending
            };

            var result = _logs.Query(query);
            return await Task.FromResult(new GrpcDiagnosticQueryReply
            {
                Total = result.Total,
                Skip = result.Skip,
                Take = result.Take,
                Entries = result.Entries
                    .Select(e => new GrpcDiagnosticEntry
                    {
                        Id = e.Id,
                        TimestampUnixMs = e.ObservedAtUnixMs,
                        Level = e.Level,
                        Category = e.Category,
                        EventName = e.EventName,
                        Message = e.Message,
                        SessionId = e.SessionId,
                        EntityId = e.EntityId,
                        CommandId = e.CommandId,
                        CorrelationId = e.CorrelationId,
                        TraceId = e.TraceId
                    })
                    .ToList()
            });
        }

        public async UnaryResult<GrpcGeneratedItemsReply> ListGeneratedItems(GrpcGeneratedItemsRequest request)
        {
            request ??= new GrpcGeneratedItemsRequest();
            var snapshot = _items.GetSnapshot()
                .OrderByDescending(i => i.SavedAtUtc)
                .ToList();

            var skip = Math.Max(0, request.Skip);
            var take = Math.Clamp(request.Take <= 0 ? 100 : request.Take, 1, MaxTake);
            var page = snapshot.Skip(skip).Take(take).ToList();

            return await Task.FromResult(new GrpcGeneratedItemsReply
            {
                Total = snapshot.Count,
                Skip = skip,
                Take = take,
                Items = page
                    .Select(i => new GrpcGeneratedItem
                    {
                        Id = i.Item.Id,
                        Type = i.Item.Type,
                        Tier = i.Item.Tier,
                        SavedUnixMs = new DateTimeOffset(i.SavedAtUtc).ToUnixTimeMilliseconds(),
                        Metadata = new System.Collections.Generic.Dictionary<string, string>(i.Metadata)
                    })
                    .ToList()
            });
        }

        static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
#endif
