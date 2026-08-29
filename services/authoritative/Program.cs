#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Authoritative.Diagnostics;
using Authoritative.Security;
using Authoritative.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

const string LocalDevCorsPolicy = "LocalDevCorsPolicy";

// Create the diagnostic store early so the logger provider can reference it
// before the DI container is built.
var diagnosticLogStore = new DiagnosticLogStore(
    Path.Combine(AppContext.BaseDirectory, "data", "diagnostic-logs"));

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new DiagnosticLogStoreLoggerProvider(diagnosticLogStore));

builder.Services.AddHttpClient();
builder.Services.AddHostedService<Authoritative.Services.MigrationHostedService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(LocalDevCorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:8083",
                "http://127.0.0.1:8083",
                "http://localhost:8081",
                "http://127.0.0.1:8081")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// list all sessions
app.MapGet("/v1/world/sessions", async (
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    var ids = await scylla.GetAllSessionIdsAsync(cancellationToken);
    return Results.Ok(ids);
});

builder.Services.AddSingleton<Authoritative.Domain.IItemGenerator, Authoritative.Domain.ItemGenerator>();
builder.Services.AddSingleton<IGeneratedItemStore, GeneratedItemStore>();
builder.Services.AddSingleton<IAdminObservabilityService, AdminObservabilityService>();
builder.Services.AddSingleton<IContainerHealthService, ContainerHealthService>();
builder.Services.AddSingleton<IDatabaseObservabilityService, DatabaseObservabilityService>();
builder.Services.AddSingleton<IPipelineRequestStore, PipelineRequestStore>();
builder.Services.AddSingleton<IPipelineDefinitionWriter, PipelineDefinitionWriter>();
builder.Services.AddSingleton<IWorldGenerationAdapter>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var externalBaseUrl = config["EXTERNAL_GENERATOR_BASE_URL"];
    if (!string.IsNullOrWhiteSpace(externalBaseUrl))
    {
        return new ExternalWorldGenerationAdapter(
            sp.GetRequiredService<IHttpClientFactory>(),
            config,
            sp.GetRequiredService<ILogger<ExternalWorldGenerationAdapter>>());
    }

    return new LocalWorldGenerationAdapter(
        sp.GetRequiredService<Authoritative.Domain.IItemGenerator>(),
        sp.GetRequiredService<IGeneratedItemStore>());
});
builder.Services.AddSingleton<ScyllaWorldPersistenceService>();
builder.Services.AddSingleton<IScyllaWorldPersistenceService>(sp => sp.GetRequiredService<ScyllaWorldPersistenceService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ScyllaWorldPersistenceService>());
builder.Services.AddSingleton<IPipelineExecutionService>(sp =>
    new PipelineExecutionService(
        sp.GetRequiredService<IWorldGenerationAdapter>(),
        sp.GetRequiredService<IAdminObservabilityService>(),
        sp.GetRequiredService<IScyllaWorldPersistenceService>(),
        Path.Combine(AppContext.BaseDirectory, "data", "world-builds")));
builder.Services.AddSingleton<IDiagnosticLogStore>(diagnosticLogStore);
builder.Services.AddSingleton<IHeadlessGeneratorService, HeadlessGeneratorService>();
builder.Services.AddSingleton<PipelineRuntimeService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PipelineRuntimeService>());
builder.Services.AddHostedService<MetricsHostedService>();
builder.Services.AddHostedService<QueueConsumer>();
builder.Services.AddSingleton<WorldEventPersistenceService>();
builder.Services.AddSingleton<IWorldEventPersistenceService>(sp => sp.GetRequiredService<WorldEventPersistenceService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WorldEventPersistenceService>());
builder.Services.AddSingleton<AgentTaskService>();
builder.Services.AddSingleton<IAgentTaskService>(sp => sp.GetRequiredService<AgentTaskService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentTaskService>());
builder.Services.AddSingleton<DungeonPoolService>();
builder.Services.AddSingleton<IDungeonPoolService>(sp => sp.GetRequiredService<DungeonPoolService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DungeonPoolService>());
builder.Services.AddSingleton<MasteryPersistenceService>();
builder.Services.AddSingleton<IMasteryPersistenceService>(sp => sp.GetRequiredService<MasteryPersistenceService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MasteryPersistenceService>());
builder.Services.AddSingleton<IMasteryService, MasteryService>();
builder.Services.AddSingleton<IUserAccountService, UserAccountService>();
builder.Services.AddSingleton<IClientRequestSecurityService, ClientRequestSecurityService>();

var app = builder.Build();
app.UseCors(LocalDevCorsPolicy);
app.UseWebSockets();

app.MapGet("/", () => Results.Ok(new
{
    service = "authoritative-backend",
    status = "ok",
    api = "admin pipeline enabled"
}));

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/v1/auth/login", (
    ClientLoginRequest request,
    IConfiguration configuration,
    IUserAccountService userAccounts,
    IClientRequestSecurityService security) =>
{
    var username = request.Username?.Trim() ?? string.Empty;
    var password = request.Password ?? string.Empty;

    if (!userAccounts.ValidateCredentials(username, password, out var account, out var error))
    {
        return Results.Unauthorized();
    }

    var session = security.CreateSession(account!.Username);
    return Results.Ok(new ClientLoginResponse
    {
        UserId = session.UserId,
        Token = session.Token,
        Canary = session.Canary,
        ExpiresAtUtc = session.ExpiresAtUtc.UtcDateTime.ToString("O")
    });
});

app.MapPost("/v1/auth/register", (
    ClientRegisterRequest request,
    IUserAccountService userAccounts,
    IClientRequestSecurityService security) =>
{
    if (!userAccounts.Register(request.Username, request.Email, request.Password, out var account, out var error))
    {
        return Results.BadRequest(new { error });
    }

    var session = security.CreateSession(account!.Username);
    return Results.Ok(new ClientRegisterResponse
    {
        UserId = session.UserId,
        Username = account.Username,
        Token = session.Token,
        Canary = session.Canary,
        ExpiresAtUtc = session.ExpiresAtUtc.UtcDateTime.ToString("O"),
        Message = "Registration successful."
    });
});

app.MapPost("/v1/auth/forgot-username", (
    ClientForgotUsernameRequest request,
    IUserAccountService userAccounts) =>
{
    if (!userAccounts.ForgotUsername(request.Email, out var username, out var error))
    {
        return Results.NotFound(new { error });
    }

    return Results.Ok(new ClientForgotUsernameResponse
    {
        Success = true,
        Username = username!,
        Email = request.Email,
        Message = $"Your username is: {username}"
    });
});

app.MapPost("/v1/auth/reset-password", (
    ClientResetPasswordRequest request,
    IUserAccountService userAccounts) =>
{
    if (!userAccounts.ResetPassword(request.UsernameOrEmail, request.NewPassword, out var error))
    {
        return Results.BadRequest(new { error });
    }

    return Results.Ok(new ClientResetPasswordResponse
    {
        Success = true,
        Message = "Password reset successfully. You can now login with your new password."
    });
});

async ValueTask<object?> ValidateClientSecurityAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
{
    var http = context.HttpContext;
    var security = http.RequestServices.GetRequiredService<IClientRequestSecurityService>();

    var authorization = http.Request.Headers.TryGetValue("Authorization", out var authHeader)
        ? authHeader.ToString()
        : string.Empty;

    var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? authorization.Substring("Bearer ".Length).Trim()
        : string.Empty;

    var user = http.Request.Headers.TryGetValue("X-Client-User", out var userHeader) ? userHeader.ToString() : string.Empty;
    var canary = http.Request.Headers.TryGetValue("X-Client-Canary", out var canaryHeader) ? canaryHeader.ToString() : string.Empty;
    var timestamp = http.Request.Headers.TryGetValue("X-Client-Timestamp", out var tsHeader) ? tsHeader.ToString() : string.Empty;
    var nonce = http.Request.Headers.TryGetValue("X-Client-Nonce", out var nonceHeader) ? nonceHeader.ToString() : string.Empty;
    var checksum = http.Request.Headers.TryGetValue("X-Client-Checksum", out var checksumHeader) ? checksumHeader.ToString() : string.Empty;

    http.Request.EnableBuffering();
    string body;
    using (var reader = new StreamReader(http.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
    {
        body = await reader.ReadToEndAsync();
    }
    http.Request.Body.Position = 0;

    var input = new ClientRequestValidationInput(
        token,
        user,
        canary,
        timestamp,
        nonce,
        checksum,
        http.Request.Method,
        http.Request.Path + http.Request.QueryString,
        body);

    if (!security.ValidateRequest(input, out var error))
    {
        return error is "invalid_token" or "user_mismatch"
            ? Results.Unauthorized()
            : Results.BadRequest(new { error });
    }

    return await next(context);
}

var admin = app.MapGroup("/admin")
    .AddEndpointFilter((context, next) =>
    {
        var http = context.HttpContext;
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        var configuredKey = config["ADMIN_API_KEY"] ?? "dev-admin-key";
        var queryKey = http.Request.Query.TryGetValue("adminKey", out var adminKeyFromQuery)
            ? adminKeyFromQuery.ToString()
            : string.Empty;

        var headerKey = http.Request.Headers.TryGetValue("X-Admin-Key", out var provided)
            ? provided.ToString()
            : string.Empty;
        var effectiveKey = !string.IsNullOrWhiteSpace(headerKey) ? headerKey : queryKey;

        if (string.IsNullOrWhiteSpace(effectiveKey) ||
            !string.Equals(effectiveKey, configuredKey, StringComparison.Ordinal))
        {
            return ValueTask.FromResult<object?>(Results.Unauthorized());
        }

        return next(context);
    });

admin.MapGet("/pipeline/requests", (IPipelineRequestStore store) => Results.Ok(store.GetAll()));

admin.MapPost("/pipeline/requests", (
    PipelineCreateRequest request,
    IPipelineRequestStore store,
    HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(request.PipelineName))
        return Results.BadRequest(new { error = "pipelineName is required" });

    var submittedBy = string.IsNullOrWhiteSpace(request.SubmittedBy)
        ? "admin"
        : request.SubmittedBy.Trim();

    var created = store.Create(request, submittedBy, httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    return Results.Created($"/admin/pipeline/requests/{created.RequestId}", created);
});

admin.MapPost("/pipeline/requests/{requestId}/approve", (
    string requestId,
    PipelineApprovalRequest approval,
    IPipelineRequestStore store,
    IPipelineDefinitionWriter writer,
    PipelineRuntimeService runtime) =>
{
    var pending = store.Get(requestId);
    if (pending == null)
        return Results.NotFound(new { error = "request not found" });

    if (pending.Status != PipelineRequestStatus.Pending)
        return Results.BadRequest(new { error = "request is not pending" });

    var approver = string.IsNullOrWhiteSpace(approval.ApprovedBy) ? "admin" : approval.ApprovedBy.Trim();
    var generated = writer.WriteApprovedDefinition(pending, approver, approval.OverrideSeed);
    var updated = store.MarkApproved(requestId, approver, generated.DefinitionPath, generated.DefinitionHash);
    runtime.ReloadNow();

    return Results.Ok(new
    {
        request = updated,
        pipeline = generated.LoadedDefinition
    });
});

admin.MapPost("/pipeline/requests/{requestId}/reject", (
    string requestId,
    PipelineRejectionRequest rejection,
    IPipelineRequestStore store) =>
{
    var existing = store.Get(requestId);
    if (existing == null)
        return Results.NotFound(new { error = "request not found" });

    if (existing.Status != PipelineRequestStatus.Pending)
        return Results.BadRequest(new { error = "request is not pending" });

    var rejectedBy = string.IsNullOrWhiteSpace(rejection.RejectedBy) ? "admin" : rejection.RejectedBy.Trim();
    var reason = string.IsNullOrWhiteSpace(rejection.Reason) ? "No reason provided" : rejection.Reason.Trim();
    var updated = store.MarkRejected(requestId, rejectedBy, reason);

    return Results.Ok(updated);
});

admin.MapGet("/pipeline/runtime/current", (PipelineRuntimeService runtime) =>
{
    var snapshot = runtime.GetSnapshot();
    return Results.Ok(snapshot);
});

admin.MapPost("/pipeline/runtime/reload", (PipelineRuntimeService runtime) =>
{
    runtime.ReloadNow();
    return Results.Ok(runtime.GetSnapshot());
});

admin.MapPost("/pipeline/runtime/execute", (
    PipelineExecutionRequest request,
    PipelineRuntimeService runtime,
    IPipelineExecutionService executor) =>
{
    var snapshot = runtime.GetSnapshot();
    if (!snapshot.IsLoaded || snapshot.ActiveDefinition == null)
        return Results.BadRequest(new { error = "no active pipeline definition loaded" });

    var execution = executor.Execute(snapshot, request ?? new PipelineExecutionRequest());
    return Results.Ok(execution);
});

admin.MapGet("/pipeline/runtime/executions", (
    int? take,
    IPipelineExecutionService executor) =>
{
    return Results.Ok(executor.GetExecutions(take ?? 25));
});

admin.MapGet("/pipeline/runtime/executions/{executionId}", (
    string executionId,
    IPipelineExecutionService executor) =>
{
    var execution = executor.GetExecution(executionId);
    return execution == null
        ? Results.NotFound(new { error = "execution not found" })
        : Results.Ok(execution);
});

admin.MapGet("/pipeline/runtime/world/current", (IPipelineExecutionService executor) =>
{
    var latest = executor.GetLatestExecution();
    return latest == null
        ? Results.NotFound(new { error = "no world execution has been run yet" })
        : Results.Ok(latest);
});

admin.MapGet("/generators/catalog", (IHeadlessGeneratorService generators) =>
{
    return Results.Ok(generators.GetCapabilities());
});

admin.MapPost("/generators/jobs", (
    GeneratorJobRequest request,
    PipelineRuntimeService runtime,
    IHeadlessGeneratorService generators) =>
{
    var job = generators.CreateJob(runtime.GetSnapshot(), request);
    return Results.Accepted($"/admin/generators/jobs/{job.JobId}", job);
});

admin.MapGet("/generators/jobs", (
    int? take,
    IHeadlessGeneratorService generators) =>
{
    return Results.Ok(generators.GetJobs(take ?? 25));
});

admin.MapGet("/generators/jobs/{jobId}", (
    string jobId,
    IHeadlessGeneratorService generators) =>
{
    var job = generators.GetJob(jobId);
    return job == null
        ? Results.NotFound(new { error = "generator job not found" })
        : Results.Ok(job);
});

admin.MapGet("/observability/snapshot", (
    string? sessionId,
        PipelineRuntimeService runtime,
        IPipelineExecutionService executor,
        IGeneratedItemStore itemStore,
        IAdminObservabilityService observability) =>
{
    var snapshot = observability.GetSnapshot(runtime.GetSnapshot(), executor.GetLatestExecution(), itemStore.GetSnapshot(), sessionId);
        return Results.Ok(snapshot);
});

admin.MapGet("/observability/events", (
    string? sessionId,
        int? take,
        IAdminObservabilityService observability) =>
{
    return Results.Ok(observability.GetRecentEvents(take ?? 100, sessionId));
});

admin.MapGet("/observability/containers/health", async (
    IContainerHealthService health,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await health.GetHealthAsync(cancellationToken));
});

admin.MapGet("/observability/containers/logs", async (
    int? tail,
    IContainerHealthService health,
    CancellationToken cancellationToken) =>
{
    var normalizedTail = Math.Clamp(tail ?? 250, 50, 2000);
    return Results.Ok(await health.GetLogInsightsAsync(normalizedTail, cancellationToken));
});

admin.MapGet("/observability/containers", (IContainerHealthService health) =>
{
    return Results.Ok(health.GetKnownContainerNames());
});

admin.MapGet("/observability/containers/{containerName}/logs", async (
    string containerName,
    int? tail,
    bool? timestamps,
    IContainerHealthService health,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(containerName))
        return Results.BadRequest(new { error = "containerName is required" });
    var result = await health.GetContainerLogsAsync(containerName, tail ?? 250, timestamps ?? false, cancellationToken);
    return Results.Ok(result);
});

admin.MapGet("/observability/databases/snapshot", async (
    IDatabaseObservabilityService databases,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await databases.GetSnapshotAsync(cancellationToken));
});

admin.MapGet("/observability/databases/{database}/query", async (
    string database,
    string query,
    IDatabaseObservabilityService databases,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await databases.QueryAsync(database, query, cancellationToken));
});

admin.MapPost("/observability/databases/{database}/maintenance", async (
    string database,
    DatabaseMaintenanceRequest request,
    IDatabaseObservabilityService databases,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await databases.RunMaintenanceAsync(database, request, cancellationToken));
});

admin.MapGet("/observability/databases/redis/keys/{key}", async (
    string key,
    IDatabaseObservabilityService databases,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await databases.GetRedisKeyAsync(key, cancellationToken));
});

admin.MapPost("/observability/databases/redis/keys", async (
    RedisKeyMutationRequest request,
    IDatabaseObservabilityService databases,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await databases.SetRedisKeyAsync(request, cancellationToken));
});

admin.MapDelete("/observability/databases/redis/keys/{key}", async (
    string key,
    IDatabaseObservabilityService databases,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await databases.DeleteRedisKeyAsync(key, cancellationToken));
});

admin.MapPost("/v1/diagnostics/logs/query", (
    DiagnosticLogQuery query,
    IDiagnosticLogStore logs) =>
{
    return Results.Ok(logs.Query(query));
});

admin.MapPost("/v1/diagnostics/logs", (
    DiagnosticLogWriteRequest request,
    IDiagnosticLogStore logs) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "Message is required." });

    var entry = logs.Record(request);
    return Results.Ok(new { id = entry.Id, timestampUtc = entry.TimestampUtc });
});

// ── Game-client world query endpoints (/v1/world/) ──────────────────────────
// These are intentionally unauthenticated — the game client connects from inside
// the Docker network and doesn't hold an admin key.  Add auth here if you expose
// this service publicly.

app.MapGet("/v1/world/sessions/{sessionId}", async (
    string sessionId,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    var result = await scylla.GetSessionAsync(sessionId, cancellationToken);
    return result == null
        ? Results.NotFound(new { error = $"Session '{sessionId}' not found in ScyllaDB." })
        : Results.Ok(result);
});

app.MapGet("/v1/world/sessions/{sessionId}/rooms", async (
    string sessionId,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    var rooms = await scylla.GetRoomsAsync(sessionId, cancellationToken);
    return Results.Ok(new { sessionId, count = rooms.Count, rooms });
});

app.MapGet("/v1/world/sessions/{sessionId}/enemies", async (
    string sessionId,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    var enemies = await scylla.GetEnemiesAsync(sessionId, cancellationToken);
    return Results.Ok(new { sessionId, count = enemies.Count, enemies });
});

app.MapGet("/v1/world/sessions/{sessionId}/loot", async (
    string sessionId,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    var loot = await scylla.GetLootAsync(sessionId, cancellationToken);
    return Results.Ok(new { sessionId, count = loot.Count, loot });
});

app.MapGet("/v1/world/sessions/{sessionId}/snapshots/{entityId}", async (
    string sessionId,
    string entityId,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    var snapshot = await scylla.GetEntitySnapshotAsync(sessionId, entityId, cancellationToken);
    return snapshot == null
        ? Results.NotFound(new { error = "snapshot not found" })
        : Results.Ok(new { sessionId, entityId, snapshotJson = snapshot });
});

app.MapGet("/v1/world/sessions/{sessionId}/metadata", async (
    string sessionId,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    var meta = await scylla.GetSessionMetadataAsync(sessionId, cancellationToken);
    return meta == null
        ? Results.NotFound(new { error = "session metadata not found" })
        : Results.Ok(new { sessionId, properties = meta });
});

record SnapshotWriteRequest(string EntityType, string SnapshotJson);

app.MapPost("/v1/world/sessions/{sessionId}/snapshots/{entityId}", async (
    HttpRequest req,
    string sessionId,
    string entityId,
    SnapshotWriteRequest request,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.SnapshotJson))
        return Results.BadRequest(new { error = "SnapshotJson is required" });

    // optional headers: X-Snapshot-TTL (seconds), X-Snapshot-Version
    int ttl = 0;
    int version = 1;
    if (req.Headers.TryGetValue("X-Snapshot-TTL", out var ttlVals) && int.TryParse(ttlVals.ToString(), out var t)) ttl = t;
    if (req.Headers.TryGetValue("X-Snapshot-Version", out var verVals) && int.TryParse(verVals.ToString(), out var v)) version = v;

    var ok = await scylla.InsertEntitySnapshotAsync(sessionId, entityId, request.EntityType ?? "unknown", request.SnapshotJson, version: version, ttlSeconds: ttl, ct: cancellationToken);
    return ok ? Results.Ok(new { sessionId, entityId }) : Results.StatusCode(500);
});

app.MapPost("/v1/world/sessions/{sessionId}/metadata", async (
    string sessionId,
    System.Collections.Generic.Dictionary<string, string> properties,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    if (properties == null || properties.Count == 0)
        return Results.BadRequest(new { error = "properties map is required" });

    var ok = await scylla.UpsertSessionMetadataAsync(sessionId, properties, cancellationToken);
    return ok ? Results.Ok(new { sessionId }) : Results.StatusCode(500);
});

// Convenience: full snapshot for a session in one call
app.MapGet("/v1/world/sessions/{sessionId}/snapshot", async (
    string sessionId,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    var sessionTask = scylla.GetSessionAsync(sessionId, cancellationToken);
    var roomsTask   = scylla.GetRoomsAsync(sessionId, cancellationToken);
    var enemiesTask = scylla.GetEnemiesAsync(sessionId, cancellationToken);
    var lootTask    = scylla.GetLootAsync(sessionId, cancellationToken);
    await Task.WhenAll(sessionTask, roomsTask, enemiesTask, lootTask);

    var session = await sessionTask;
    if (session == null)
        return Results.NotFound(new { error = $"Session '{sessionId}' not found in ScyllaDB." });

    return Results.Ok(new
    {
        session,
        rooms   = await roomsTask,
        enemies = await enemiesTask,
        loot    = await lootTask
    });
});

app.MapGet("/v1/world/sessions/{sessionId}/binary-snapshot", async (
    string sessionId,
    IHeadlessGeneratorService generators,
    IPipelineExecutionService executor,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    // Try in-memory first
    var latestJob = generators.GetLatestJobForSession(sessionId);
    var latestExecution = latestJob?.Execution ?? executor.GetExecutions(50)
        .OfType<PipelineExecutionRecord>()
        .Where(x => string.Equals(x.SessionId, sessionId, StringComparison.Ordinal))
        .OrderByDescending(x => x.CompletedAtUtc)
        .FirstOrDefault();

    if (latestExecution?.World != null)
    {
        var bytes = BinaryWorldSnapshotSerializer.SerializeWorldArtifact(sessionId, latestExecution.ExecutionId, latestExecution.World);
        return Results.File(bytes, "application/octet-stream", $"snapshot_{sessionId}.bin");
    }

    // Fallback: query ScyllaDB
    var sessionRow = await scylla.GetSessionAsync(sessionId, cancellationToken);
    if (sessionRow == null)
        return Results.NotFound(new { error = $"Session '{sessionId}' not found in ScyllaDB or memory." });

    var roomsTask   = scylla.GetRoomsAsync(sessionId, cancellationToken);
    var enemiesTask = scylla.GetEnemiesAsync(sessionId, cancellationToken);
    var lootTask    = scylla.GetLootAsync(sessionId, cancellationToken);
    await Task.WhenAll(roomsTask, enemiesTask, lootTask);

    var world = new GeneratedWorldArtifact
    {
        Seed         = sessionRow.Seed,
        Width        = sessionRow.Width,
        Height       = sessionRow.Height,
        DungeonLevel = sessionRow.DungeonLevel,
        Rooms = (await roomsTask).Select(r => new WorldRoom
        {
            Id = r.RoomId, X = r.X, Y = r.Y, Width = r.Width, Height = r.Height
        }).ToList(),
        Enemies = (await enemiesTask).Select(e => new WorldEnemy
        {
            Id = e.EnemyId, Archetype = e.Archetype, X = e.X, Y = e.Y, Level = e.Level
        }).ToList(),
        Loot = (await lootTask).Select(l => new WorldLoot
        {
            ItemId = l.ItemId, ItemType = l.ItemType, Tier = l.Tier, X = l.X, Y = l.Y
        }).ToList()
    };

    var binaryBytes = BinaryWorldSnapshotSerializer.SerializeWorldArtifact(sessionId, sessionRow.ExecutionId, world);
    return Results.File(binaryBytes, "application/octet-stream", $"snapshot_{sessionId}.bin");
});

// ── Pool endpoints ──────────────────────────────────────────────────────────
app.MapGet("/v1/pool/status", (IDungeonPoolService pool) =>
{
    return Results.Ok(pool.GetStatistics());
});

app.MapPost("/v1/pool/claim", async (
    IDungeonPoolService pool,
    int difficultyLevel,
    CancellationToken ct) =>
{
    var result = await pool.ClaimDungeonAsync(difficultyLevel, ct);
    if (result == null)
        return Results.NotFound(new { error = $"No available dungeon at difficulty level {difficultyLevel}" });

    return Results.Ok(result);
});

// ── Mastery endpoints ───────────────────────────────────────────────────────
var masteryRoutes = app.MapGroup("/v1/mastery")
    .AddEndpointFilter(ValidateClientSecurityAsync);

masteryRoutes.MapPost("/offers", async (
    IMasteryService mastery,
    string userId,
    string itemType,
    string masteryTier,
    CancellationToken ct) =>
{
    try
    {
        var offer = await mastery.GenerateOfferAsync(userId, itemType, masteryTier, ct);
        return Results.Ok(offer);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

masteryRoutes.MapPost("/select", async (
    IMasteryService mastery,
    string userId,
    string offerId,
    string skillId,
    CancellationToken ct) =>
{
    try
    {
        var selected = await mastery.SelectOptionAsync(userId, offerId, skillId, ct);
        return Results.Ok(selected);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

masteryRoutes.MapGet("/progress", async (
    IMasteryService mastery,
    string userId,
    string itemType,
    CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await mastery.GetProgressAsync(userId, itemType, ct));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// ── Admin pool endpoints ────────────────────────────────────────────────────
admin.MapPost("/pool/config", (IDungeonPoolService pool, double generationRatio) =>
{
    try
    {
        pool.SetGenerationRatio(generationRatio);
        return Results.Ok(new { message = $"Generation ratio set to {generationRatio}", stats = pool.GetStatistics() });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

admin.MapPost("/pool/generate-batch", async (
    IDungeonPoolService pool,
    int difficultyLevel,
    int count,
    CancellationToken ct) =>
{
    await pool.GenerateBatchAsync(difficultyLevel, count, ct);
    return Results.Ok(new { message = $"Batch generation started: {count} dungeons at level {difficultyLevel}", stats = pool.GetStatistics() });
});

// Exporter raw-metrics proxy endpoints
var exporterUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["redis"]    = "http://redis-exporter:9121/metrics",
    ["postgres"] = "http://postgres-exporter:9187/metrics",
    ["rabbitmq"] = "http://rabbitmq-exporter:9419/metrics",
    ["scylla"]   = "http://scylla:9180/metrics",
};

admin.MapGet("/observability/exporters", () =>
{
    return Results.Ok(exporterUrls.Select(kv => new { exporter = kv.Key, metricsUrl = kv.Value }));
});

admin.MapGet("/observability/exporters/{exporter}/raw", async (
    string exporter,
    IHttpClientFactory httpFactory,
    CancellationToken cancellationToken) =>
{
    if (!exporterUrls.TryGetValue(exporter, out var url))
        return Results.NotFound(new { error = $"Unknown exporter '{exporter}'. Known: {string.Join(", ", exporterUrls.Keys)}" });

    var client = httpFactory.CreateClient();
    client.Timeout = TimeSpan.FromSeconds(10);
    try
    {
        var raw = await client.GetStringAsync(url, cancellationToken);
        var lines = raw.Split('\n', StringSplitOptions.None);
        return Results.Ok(new
        {
            exporter,
            fetchedAtUtc = DateTime.UtcNow,
            success = true,
            error = (string?)null,
            lineCount = lines.Length,
            lines,
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            exporter,
            fetchedAtUtc = DateTime.UtcNow,
            success = false,
            error = ex.Message,
            lineCount = 0,
            lines = Array.Empty<string>(),
        });
    }
});

admin.MapPost("/observability/sessions/{sessionId}/events", (
    string sessionId,
    WorldSessionEventIngestRequest request,
    IAdminObservabilityService observability,
    IWorldEventPersistenceService persistence) =>
{
    if (string.IsNullOrWhiteSpace(sessionId))
        return Results.BadRequest(new { error = "sessionId is required" });

    var worldEvent = new WorldSessionEvent
    {
        SessionId = sessionId,
        EventType = string.IsNullOrWhiteSpace(request.EventType) ? "custom" : request.EventType.Trim(),
        Category = string.IsNullOrWhiteSpace(request.Category) ? "simulation" : request.Category.Trim(),
        Frame = request.Frame,
        EntityId = request.EntityId?.Trim() ?? string.Empty,
        Message = string.IsNullOrWhiteSpace(request.Message)
        ? "Session event received from upstream service."
        : request.Message.Trim(),
        TimestampUtc = DateTime.UtcNow,
        Data = request.Data ?? new Dictionary<string, string>(StringComparer.Ordinal)
    };

    observability.RecordWorldEvent(worldEvent);
    persistence.EnqueueEvent(worldEvent);
    return Results.Accepted($"/admin/observability/sessions/{sessionId}/timeline", worldEvent);
});

admin.MapGet("/observability/sessions/{sessionId}/timeline", (
    string sessionId,
    int? take,
    IAdminObservabilityService observability) =>
{
    return Results.Ok(observability.GetSessionTimeline(sessionId, take ?? 200));
});

admin.MapGet("/observability/sessions/{sessionId}/events/history", async (
    string sessionId,
    int? take,
    IWorldEventPersistenceService persistence,
    CancellationToken cancellationToken) =>
{
    var events = await persistence.QueryEventsAsync(sessionId, take ?? 200, cancellationToken);
    return Results.Ok(events);
});

admin.MapGet("/observability/sessions/{sessionId}/events/summary", async (
    string sessionId,
    IWorldEventPersistenceService persistence,
    CancellationToken cancellationToken) =>
{
    var summary = await persistence.GetSessionSummaryAsync(sessionId, cancellationToken);
    return Results.Ok(summary);
});

admin.MapGet("/observability/stream", async (
        HttpContext http,
    string? sessionId,
        PipelineRuntimeService runtime,
        IPipelineExecutionService executor,
        IGeneratedItemStore itemStore,
        IAdminObservabilityService observability) =>
{
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers.ContentType = "text/event-stream";

        while (!http.RequestAborted.IsCancellationRequested)
        {
            var snapshot = observability.GetSnapshot(runtime.GetSnapshot(), executor.GetLatestExecution(), itemStore.GetSnapshot(), sessionId);
                var json = JsonSerializer.Serialize(snapshot);

                await http.Response.WriteAsync($"event: snapshot\n", http.RequestAborted);
                await http.Response.WriteAsync($"data: {json}\n\n", http.RequestAborted);
                await http.Response.Body.FlushAsync(http.RequestAborted);

                try
                {
                        await Task.Delay(TimeSpan.FromSeconds(1), http.RequestAborted);
                }
                catch (TaskCanceledException)
                {
                        break;
                }
        }
});

admin.MapGet("/observability/ws", async (
        HttpContext http,
        string? sessionId,
        PipelineRuntimeService runtime,
        IPipelineExecutionService executor,
        IGeneratedItemStore itemStore,
        IAdminObservabilityService observability) =>
{
        if (!http.WebSockets.IsWebSocketRequest)
            return Results.BadRequest(new { error = "websocket upgrade required" });

        using var socket = await http.WebSockets.AcceptWebSocketAsync();
        while (socket.State == WebSocketState.Open && !http.RequestAborted.IsCancellationRequested)
        {
            var snapshot = observability.GetSnapshot(runtime.GetSnapshot(), executor.GetLatestExecution(), itemStore.GetSnapshot(), sessionId);
            var payload = JsonSerializer.Serialize(new { type = "snapshot", data = snapshot });
            var bytes = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken: http.RequestAborted);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), http.RequestAborted);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", http.RequestAborted);
        }

        return Results.Empty;
});

var client = app.MapGroup("/client")
    .AddEndpointFilter(ValidateClientSecurityAsync);

client.MapGet("/sessions/{sessionId}/world/bootstrap", (
    string sessionId,
    HttpContext http,
    IConfiguration config,
    IHeadlessGeneratorService generators,
    IPipelineExecutionService executor) =>
{
    var latestJob = generators.GetLatestJobForSession(sessionId);
    var latestExecution = latestJob?.Execution ?? executor.GetExecutions(50)
        .OfType<PipelineExecutionRecord>()
        .Where(x => string.Equals(x.SessionId, sessionId, StringComparison.Ordinal))
        .OrderByDescending(x => x.CompletedAtUtc)
        .FirstOrDefault();

    var scheme = http.Request.Scheme;
    var host = http.Request.Host.Value;
    var baseUrl = $"{scheme}://{host}";
    var query = $"sessionId={Uri.EscapeDataString(sessionId)}&adminKey={Uri.EscapeDataString(config["ADMIN_API_KEY"] ?? "dev-admin-key")}";

    var response = new UnitySessionBootstrapResponse
    {
        SessionId = sessionId,
        HasWorld = latestExecution != null,
        ExecutionId = latestExecution?.ExecutionId,
        RoomCount = latestExecution?.World.Rooms.Count ?? 0,
        EnemyCount = latestExecution?.World.Enemies.Count ?? 0,
        LootCount = latestExecution?.World.Loot.Count ?? 0,
        SnapshotUrl = $"{baseUrl}/admin/observability/snapshot?{query}",
        StreamUrl = $"{baseUrl}/admin/observability/stream?{query}",
        WebSocketUrl = $"{(scheme == "https" ? "wss" : "ws")}://{host}/admin/observability/ws?{query}",
        TimelineUrl = $"{baseUrl}/client/sessions/{Uri.EscapeDataString(sessionId)}/timeline"
    };

    return Results.Ok(response);
});

client.MapGet("/sessions/{sessionId}/timeline", (
    string sessionId,
    int? take,
    IAdminObservabilityService observability) =>
{
    return Results.Ok(new UnitySessionTimelineEnvelope
    {
        SessionId = sessionId,
        Events = observability.GetSessionTimeline(sessionId, take ?? 200)
    });
});

client.MapGet("/sessions/{sessionId}/world/current", async (
    string sessionId,
    IHeadlessGeneratorService generators,
    IPipelineExecutionService executor,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    // Try in-memory first (fast path — data present since last boot)
    var latestJob = generators.GetLatestJobForSession(sessionId);
    var latestExecution = latestJob?.Execution ?? executor.GetExecutions(50)
        .OfType<PipelineExecutionRecord>()
        .Where(x => string.Equals(x.SessionId, sessionId, StringComparison.Ordinal))
        .OrderByDescending(x => x.CompletedAtUtc)
        .FirstOrDefault();

    if (latestExecution != null)
        return Results.Ok(new UnitySessionWorldEnvelope
        {
            SessionId = sessionId,
            ExecutionId = latestExecution.ExecutionId,
            World = latestExecution.World
        });

    // Fallback: load durable data from ScyllaDB (survives server restarts)
    var sessionRow = await scylla.GetSessionAsync(sessionId, cancellationToken);
    if (sessionRow == null)
        return Results.NotFound(new { error = "no world execution has been run for this session" });

    var roomsTask   = scylla.GetRoomsAsync(sessionId, cancellationToken);
    var enemiesTask = scylla.GetEnemiesAsync(sessionId, cancellationToken);
    var lootTask    = scylla.GetLootAsync(sessionId, cancellationToken);
    await Task.WhenAll(roomsTask, enemiesTask, lootTask);

    var world = new GeneratedWorldArtifact
    {
        Seed         = sessionRow.Seed,
        Width        = sessionRow.Width,
        Height       = sessionRow.Height,
        DungeonLevel = sessionRow.DungeonLevel,
        Rooms = (await roomsTask).Select(r => new WorldRoom
        {
            Id = r.RoomId, X = r.X, Y = r.Y, Width = r.Width, Height = r.Height
        }).ToList(),
        Enemies = (await enemiesTask).Select(e => new WorldEnemy
        {
            Id = e.EnemyId, Archetype = e.Archetype, X = e.X, Y = e.Y, Level = e.Level
        }).ToList(),
        Loot = (await lootTask).Select(l => new WorldLoot
        {
            ItemId = l.ItemId, ItemType = l.ItemType, Tier = l.Tier, X = l.X, Y = l.Y
        }).ToList()
    };

    return Results.Ok(new UnitySessionWorldEnvelope
    {
        SessionId  = sessionId,
        ExecutionId = sessionRow.ExecutionId,
        World = world
    });
});

// ── Agent task endpoints ─────────────────────────────────────────────────
admin.MapPost("/agent/tasks", async (
    AgentTaskSubmitRequest req,
    IAgentTaskService agent,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(req.Description))
        return Results.BadRequest(new { error = "description is required" });
    var task = await agent.SubmitTaskAsync(req.Description.Trim(), cancellationToken);
    return Results.Created($"/admin/agent/tasks/{task.Id}", task);
});

admin.MapGet("/agent/tasks", async (
    string? status,
    IAgentTaskService agent,
    CancellationToken cancellationToken) =>
{
    var tasks = await agent.GetTasksAsync(status, cancellationToken);
    return Results.Ok(tasks);
});

admin.MapGet("/agent/tasks/{taskId}", async (
    string taskId,
    IAgentTaskService agent,
    CancellationToken cancellationToken) =>
{
    var task = await agent.GetTaskAsync(taskId, cancellationToken);
    return task is null ? Results.NotFound(new { error = "task not found" }) : Results.Ok(task);
});

admin.MapDelete("/agent/tasks/{taskId}", async (
    string taskId,
    IAgentTaskService agent,
    CancellationToken cancellationToken) =>
{
    await agent.CancelTaskAsync(taskId, cancellationToken);
    return Results.Ok(new { ok = true });
});

await app.RunAsync();
#endif
