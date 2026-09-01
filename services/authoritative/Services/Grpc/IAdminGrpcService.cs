using MagicOnion;

namespace Authoritative.Services.Grpc;

/// <summary>
/// Admin gRPC surface exposed over Kestrel. Every method is protected by the
/// deny-by-default <see cref="AdminAuthFilter"/>; callers must present a
/// valid admin credential in request metadata or the call is rejected before
/// reaching the service handler.
/// </summary>
public interface IAdminGrpcService : IService<IAdminGrpcService>
{
    /// <summary>Return a lightweight health acknowledgement.</summary>
    UnaryResult<GrpcHealthReply> GetHealth();

    /// <summary>
    /// Return the authoritative service metrics in Prometheus text format.
    /// </summary>
    UnaryResult<GrpcMetricsReply> ExportMetrics();

    /// <summary>Run a bounded diagnostic-log query against the authoritative log store.</summary>
    UnaryResult<GrpcDiagnosticQueryReply> QueryDiagnostics(GrpcDiagnosticQueryRequest request);

    /// <summary>Return a bounded snapshot of generated items persisted by the authoritative service.</summary>
    UnaryResult<GrpcGeneratedItemsReply> ListGeneratedItems(GrpcGeneratedItemsRequest request);
}
