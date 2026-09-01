#if !UNITY_5_3_OR_NEWER
using MagicOnion;

namespace Authoritative.Services.Grpc
{
    /// <summary>
    /// Admin gRPC service contract, additive to the existing HTTP admin surface.
    /// Exposed through ASP.NET Core MagicOnion with gRPC-Web support and a
    /// deny-by-default authorization filter (see <see cref="AdminAuthFilter"/>).
    /// </summary>
    public interface IAdminGrpcService : IService<IAdminGrpcService>
    {
        UnaryResult<GrpcHealthReply> GetHealth();
        UnaryResult<GrpcDiagnosticQueryReply> QueryDiagnostics(GrpcDiagnosticQueryRequest request);
        UnaryResult<GrpcGeneratedItemsReply> ListGeneratedItems(GrpcGeneratedItemsRequest request);
    }
}
#endif
