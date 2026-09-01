using Grpc.Core;
using MagicOnion.Server;
using Microsoft.Extensions.Configuration;

namespace Authoritative.Services.Grpc;

/// <summary>
/// Deny-by-default authentication filter registered globally for the MagicOnion
/// admin gRPC surface. Every call must present an <c>x-admin-api-key</c> request
/// header (metadata) matching the configured <c>AUTHORITATIVE_ADMIN_API_KEY</c>.
/// When no key is configured the surface is closed (deny) rather than left open,
/// so the admin gRPC API cannot be exposed accidentally.
/// </summary>
public sealed class AdminAuthFilter : MagicOnionFilterAttribute
{
    readonly string _expectedKey;

    public AdminAuthFilter(IConfiguration configuration)
    {
        _expectedKey = configuration["AUTHORITATIVE_ADMIN_API_KEY"] ?? "";
    }

    public override async ValueTask Invoke(ServiceContext context, Func<ServiceContext, ValueTask> next)
    {
        if (!AdminAuth.Evaluate(_expectedKey, context.CallContext.RequestHeaders))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "admin_api_key_required"));

        await next(context);
    }
}
