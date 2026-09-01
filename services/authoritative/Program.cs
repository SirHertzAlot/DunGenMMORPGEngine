using Authoritative.Diagnostics;
using Authoritative.Services;
using Authoritative.Services.Grpc;
using MagicOnion.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var diagnosticPrefixes = builder.Configuration["AUTHORITATIVE_DIAGNOSTIC_HTTP_PREFIXES"];
var urls = ToKestrelUrls(diagnosticPrefixes);
if (!string.IsNullOrWhiteSpace(urls))
    builder.WebHost.UseUrls(urls);

builder.Services.AddSingleton<Authoritative.Domain.IItemGenerator, Authoritative.Domain.ItemGenerator>();
builder.Services.AddSingleton<IGeneratedItemStore>(_ =>
{
    var dataDirectory = builder.Configuration["AUTHORITATIVE_DATA_DIR"];
    return string.IsNullOrWhiteSpace(dataDirectory)
        ? new GeneratedItemStore()
        : new GeneratedItemStore(dataDirectory);
});
builder.Services.AddSingleton<IDiagnosticLogStore>(_ =>
{
    var dataDirectory = builder.Configuration["AUTHORITATIVE_DATA_DIR"];
    return string.IsNullOrWhiteSpace(dataDirectory)
        ? new DiagnosticLogStore()
        : new DiagnosticLogStore(dataDirectory);
});
builder.Services.AddSingleton<IAuthoritativeMetrics, AuthoritativeMetrics>();
builder.Services.AddHostedService<QueueConsumer>();
builder.Services.AddHostedService<AuthoritativeHeartbeatService>();

builder.Services.AddMagicOnion(options =>
{
    options.GlobalFilters.Add<AdminAuthFilter>();
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["Access-Control-Allow-Origin"] = "*";
    context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PATCH,DELETE,OPTIONS";
    context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type,X-Admin-Api-Key";
    await next();
});

AdminHttpEndpoints.MapAdminEndpoints(app);

app.MapGet("/", () => Results.Ok(new { service = "authoritative", surface = "admin-api" }));
app.MapGet("/health", (HttpContext ctx) =>
{
    return Results.Ok(new { status = "ok", service = "authoritative" });
});
app.MapGet("/metrics", (IAuthoritativeMetrics metrics) =>
{
    metrics.MarkHeartbeat(DateTimeOffset.UtcNow);
    return Results.Text(metrics.ExportPrometheus(), "text/plain; version=0.0.4; charset=utf-8");
});

app.MapMagicOnionService();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var store = app.Services.GetRequiredService<IDiagnosticLogStore>();
    var stackRole = Environment.GetEnvironmentVariable("AUTHORITATIVE_STACK_ROLE");
    var releaseChannel = Environment.GetEnvironmentVariable("AUTHORITATIVE_RELEASE_CHANNEL");
    store.Record(new DiagnosticLogWriteRequest
    {
        Level = "Information",
        Category = "service.lifecycle",
        EventName = "authoritative.starting",
        Message = "Authoritative service is starting with diagnostic log CRUD endpoints enabled.",
        Tags = new Dictionary<string, string>
        {
            ["service"] = "authoritative",
            ["surface"] = "admin-api",
            ["stackRole"] = string.IsNullOrWhiteSpace(stackRole) ? "stable" : stackRole,
            ["releaseChannel"] = string.IsNullOrWhiteSpace(releaseChannel) ? "stable" : releaseChannel
        }
    });
});

app.Run();

static string? ToKestrelUrls(string? prefixes)
{
    if (string.IsNullOrWhiteSpace(prefixes))
        return "http://0.0.0.0:8080/";

    var first = prefixes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault();

    if (string.IsNullOrWhiteSpace(first))
        return "http://0.0.0.0:8080/";

    return first.Replace("*", "0.0.0.0");
}
