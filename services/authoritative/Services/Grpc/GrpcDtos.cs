namespace Authoritative.Services.Grpc;

public sealed class GrpcHealthReply
{
    public string Status { get; set; } = "ok";
    public string Service { get; set; } = "authoritative";
    public long TimestampUnixSeconds { get; set; }
}

public sealed class GrpcMetricsReply
{
    public string PrometheusText { get; set; } = "";
}

public sealed class GrpcDiagnosticQueryRequest
{
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
    public string? Category { get; set; }
    public string? Level { get; set; }
    public string? EventName { get; set; }
    public string? SessionId { get; set; }
    public string? EntityId { get; set; }
    public string? TextContains { get; set; }
    public bool Descending { get; set; } = true;
}

public sealed class GrpcDiagnosticQueryReply
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public List<GrpcDiagnosticEntry> Entries { get; set; } = new();
}

public sealed class GrpcDiagnosticEntry
{
    public string Id { get; set; } = "";
    public long TimestampUnixMs { get; set; }
    public string Level { get; set; } = "";
    public string Category { get; set; } = "";
    public string EventName { get; set; } = "";
    public string Message { get; set; } = "";
    public string? EntityId { get; set; }
    public string? SessionId { get; set; }
}

public sealed class GrpcGeneratedItemsRequest
{
    public int Take { get; set; } = 200;
}

public sealed class GrpcGeneratedItemsReply
{
    public int Total { get; set; }
    public List<GrpcGeneratedItem> Items { get; set; } = new();
}

public sealed class GrpcGeneratedItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Tier { get; set; } = "";
    public string SavedAtUtc { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = new();
}
