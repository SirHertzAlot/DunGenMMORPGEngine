using System.Net;
using System.Text.Json;
using Authoritative.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Authoritative.Services;

public sealed class DiagnosticHttpService : BackgroundService
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    readonly ILogger<DiagnosticHttpService> _log;
    readonly IDiagnosticLogStore _diagnosticLogs;
    readonly IAuthoritativeMetrics _metrics;
    readonly string[] _prefixes;
    readonly string? _adminApiKey;
    readonly HttpListener _listener = new();

    public DiagnosticHttpService(
        ILogger<DiagnosticHttpService> log,
        IConfiguration configuration,
        IDiagnosticLogStore diagnosticLogs,
        IAuthoritativeMetrics metrics)
    {
        _log = log;
        _diagnosticLogs = diagnosticLogs;
        _metrics = metrics;
        _adminApiKey = configuration["AUTHORITATIVE_ADMIN_API_KEY"];

        var configuredPrefixes = configuration["AUTHORITATIVE_DIAGNOSTIC_HTTP_PREFIXES"];
        _prefixes = string.IsNullOrWhiteSpace(configuredPrefixes)
            ? new[] { "http://*:8080/" }
            : configuredPrefixes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!HttpListener.IsSupported)
        {
            _log.LogWarning("HttpListener is not supported on this platform; diagnostic HTTP API is disabled.");
            return;
        }

        foreach (var prefix in _prefixes)
            _listener.Prefixes.Add(prefix);

        try
        {
            _listener.Start();
            _log.LogInformation("Diagnostic HTTP API listening on {prefixes}", string.Join(", ", _prefixes));
            _diagnosticLogs.Record(new DiagnosticLogWriteRequest
            {
                Level = "Information",
                Category = "diagnostics.http",
                EventName = "diagnostics.http.started",
                Message = "Diagnostic HTTP API started.",
                Properties = new Dictionary<string, string>
                {
                    ["prefixes"] = string.Join(",", _prefixes)
                }
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Diagnostic HTTP API could not start.");
            _diagnosticLogs.Record(new DiagnosticLogWriteRequest
            {
                Level = "Error",
                Category = "diagnostics.http",
                EventName = "diagnostics.http.start_failed",
                Message = "Diagnostic HTTP API could not start."
            }, ex);
            return;
        }

        using var stopRegistration = stoppingToken.Register(() =>
        {
            try
            {
                _listener.Stop();
            }
            catch
            {
                // Shutdown is best-effort.
            }
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException) when (stoppingToken.IsCancellationRequested || !_listener.IsListening)
            {
                break;
            }
            catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context), stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch
        {
            // Shutdown is best-effort.
        }

        return base.StopAsync(cancellationToken);
    }

    async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            ApplyCorsHeaders(context.Response);
            await RouteAsync(context);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Diagnostic HTTP request failed.");
            await WriteJsonAsync(context.Response, HttpStatusCode.InternalServerError, new { error = "diagnostic_request_failed" });
        }
        finally
        {
            context.Response.Close();
        }
    }

    async Task RouteAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
        if (path.Length == 0)
            path = "/";

        if (string.Equals(request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.NoContent, null);
            return;
        }

        if (string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
            && string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { status = "ok", service = "authoritative" });
            return;
        }

        if (string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
            && string.Equals(path, "/metrics", StringComparison.OrdinalIgnoreCase))
        {
            _metrics.MarkHeartbeat(DateTimeOffset.UtcNow);
            await WriteTextAsync(context.Response, HttpStatusCode.OK, "text/plain; version=0.0.4; charset=utf-8", _metrics.ExportPrometheus());
            return;
        }

        if (!path.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { error = "not_found" });
            return;
        }

        if (!IsAuthorized(request))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.Unauthorized, new { error = "admin_api_key_required" });
            return;
        }

        const string collectionPath = "/admin/v1/diagnostics/logs";
        if (string.Equals(path, $"{collectionPath}/query", StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            var query = await ReadJsonAsync<DiagnosticLogQuery>(request) ?? new DiagnosticLogQuery();
            await WriteJsonAsync(context.Response, HttpStatusCode.OK, _diagnosticLogs.Query(query));
            return;
        }

        if (string.Equals(path, collectionPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            var write = await ReadJsonAsync<DiagnosticLogWriteRequest>(request) ?? new DiagnosticLogWriteRequest();
            var entry = _diagnosticLogs.Record(write);
            context.Response.Headers["Location"] = $"{collectionPath}/{entry.Id}";
            await WriteJsonAsync(context.Response, HttpStatusCode.Created, entry);
            return;
        }

        if (path.StartsWith($"{collectionPath}/", StringComparison.OrdinalIgnoreCase))
        {
            var id = path[(collectionPath.Length + 1)..];
            await RouteLogByIdAsync(context, id);
            return;
        }

        await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { error = "not_found" });
    }

    async Task RouteLogByIdAsync(HttpListenerContext context, string id)
    {
        var method = context.Request.HttpMethod;
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            var entry = _diagnosticLogs.Get(id);
            if (entry == null)
                await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { error = "not_found" });
            else
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, entry);
            return;
        }

        if (string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase))
        {
            var update = await ReadJsonAsync<DiagnosticLogUpdateRequest>(context.Request) ?? new DiagnosticLogUpdateRequest();
            if (_diagnosticLogs.TryUpdate(id, update, out var updated))
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, updated);
            else
                await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { error = "not_found" });
            return;
        }

        if (string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            if (_diagnosticLogs.TryDelete(id))
                await WriteJsonAsync(context.Response, HttpStatusCode.NoContent, null);
            else
                await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { error = "not_found" });
            return;
        }

        await WriteJsonAsync(context.Response, HttpStatusCode.MethodNotAllowed, new { error = "method_not_allowed" });
    }

    bool IsAuthorized(HttpListenerRequest request)
    {
        if (string.IsNullOrWhiteSpace(_adminApiKey))
            return true;

        return string.Equals(request.Headers["X-Admin-Api-Key"], _adminApiKey, StringComparison.Ordinal);
    }

    static async Task<T?> ReadJsonAsync<T>(HttpListenerRequest request)
    {
        if (!request.HasEntityBody)
            return default;

        return await JsonSerializer.DeserializeAsync<T>(request.InputStream, JsonOptions);
    }

    static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode statusCode, object? value)
    {
        response.StatusCode = (int)statusCode;
        if (value == null)
            return;

        response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(response.OutputStream, value, JsonOptions);
    }

    static async Task WriteTextAsync(HttpListenerResponse response, HttpStatusCode statusCode, string contentType, string value)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = contentType;
        using var writer = new StreamWriter(response.OutputStream);
        await writer.WriteAsync(value);
    }

    static void ApplyCorsHeaders(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PATCH,DELETE,OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type,X-Admin-Api-Key";
    }
}
