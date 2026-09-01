#if !UNITY_5_3_OR_NEWER
using System.Collections.Generic;

namespace Authoritative.Services.Grpc
{
    public sealed class GrpcHealthReply
    {
        public string Status { get; set; } = "ok";
        public string Service { get; set; } = "authoritative";
        public string Version { get; set; } = "1.0.0";
    }

    public sealed class GrpcDiagnosticQueryRequest
    {
        public string? Level { get; set; }
        public string? SessionId { get; set; }
        public string? Category { get; set; }
        public string? EventName { get; set; }
        public string? TextContains { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 100;
        public bool Descending { get; set; } = true;
    }

    public sealed class GrpcDiagnosticEntry
    {
        public string Id { get; set; } = "";
        public long TimestampUnixMs { get; set; }
        public string Level { get; set; } = "Information";
        public string Category { get; set; } = "general";
        public string EventName { get; set; } = "diagnostic.event";
        public string Message { get; set; } = "";
        public string? SessionId { get; set; }
        public string? EntityId { get; set; }
        public string? CommandId { get; set; }
        public string? CorrelationId { get; set; }
        public string? TraceId { get; set; }
    }

    public sealed class GrpcDiagnosticQueryReply
    {
        public int Total { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public List<GrpcDiagnosticEntry> Entries { get; set; } = new();
    }

    public sealed class GrpcGeneratedItemsRequest
    {
        public int Skip { get; set; }
        public int Take { get; set; } = 100;
    }

    public sealed class GrpcGeneratedItem
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Tier { get; set; } = "";
        public long SavedUnixMs { get; set; }
        public System.Collections.Generic.Dictionary<string, string> Metadata { get; set; } = new();
    }

    public sealed class GrpcGeneratedItemsReply
    {
        public int Total { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public List<GrpcGeneratedItem> Items { get; set; } = new();
    }
}
#endif
