using System.Text.Json;
using Authoritative.Diagnostics;

namespace Authoritative.Services;

/// <summary>
/// Migrated the raw HttpListener admin API onto ASP.NET Core minimal endpoints,
/// preserving the exact public routes, auth header (<c>X-Admin-Api-Key</c>),
/// CORS behavior, and JSON shapes previously served by
/// <see cref="DiagnosticHttpService"/>. No public route or payload contract
/// changed as part of the Worker-to-ASP.NET-Core migration.
/// </summary>
public static class AdminHttpEndpoints
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    /// <summary>The collection path served by this admin surface.</summary>
    public const string CollectionPath = "/admin/v1/diagnostics/logs";

    public static void MapAdminEndpoints(WebApplication app)
    {
        var adminGroup = app.MapGroup("/admin").DisableAntiforgery();

        adminGroup.MapMethods("/{**rest}", new[] { "OPTIONS" }, () => Results.NoContent());

        adminGroup.MapPost("/v1/diagnostics/logs/query", async (HttpRequest request, IDiagnosticLogStore logs, HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx))
                return Unauthorized();

            var query = await ReadJsonAsync<DiagnosticLogQuery>(request) ?? new DiagnosticLogQuery();
            return Results.Json(logs.Query(query));
        });

        adminGroup.MapPost("/v1/diagnostics/logs", async (HttpRequest request, IDiagnosticLogStore logs, HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx))
                return Unauthorized();

            var write = await ReadJsonAsync<DiagnosticLogWriteRequest>(request) ?? new DiagnosticLogWriteRequest();
            var entry = logs.Record(write);
            return Results.Created($"{CollectionPath}/{entry.Id}", entry);
        });

        adminGroup.MapGet("/v1/diagnostics/logs/{id}", (string id, IDiagnosticLogStore logs, HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx))
                return Unauthorized();

            var entry = logs.Get(id);
            return entry == null ? Results.NotFound(new { error = "not_found" }) : Results.Json(entry);
        });

        adminGroup.MapPatch("/v1/diagnostics/logs/{id}", async (string id, HttpRequest request, IDiagnosticLogStore logs, HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx))
                return Unauthorized();

            var update = await ReadJsonAsync<DiagnosticLogUpdateRequest>(request) ?? new DiagnosticLogUpdateRequest();
            return logs.TryUpdate(id, update, out var updated)
                ? Results.Json(updated)
                : Results.NotFound(new { error = "not_found" });
        });

        adminGroup.MapDelete("/v1/diagnostics/logs/{id}", (string id, IDiagnosticLogStore logs, HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx))
                return Unauthorized();

            return logs.TryDelete(id)
                ? Results.NoContent()
                : Results.NotFound(new { error = "not_found" });
        });
    }

    static IResult Unauthorized() => Results.Json(new { error = "admin_api_key_required" }, statusCode: StatusCodes.Status401Unauthorized);

    static bool IsAuthorized(HttpContext context)
    {
        var adminApiKey = context.RequestServices
            .GetRequiredService<IConfiguration>()["AUTHORITATIVE_ADMIN_API_KEY"];

        if (string.IsNullOrWhiteSpace(adminApiKey))
            return true;

        return string.Equals(context.Request.Headers["X-Admin-Api-Key"], adminApiKey, StringComparison.Ordinal);
    }

    static async Task<T?> ReadJsonAsync<T>(HttpRequest request)
    {
        if (!request.HasJsonContentType())
            return default;

        return await request.ReadFromJsonAsync<T>(JsonOptions);
    }
}
