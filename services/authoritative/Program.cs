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
using Authoritative.GraphQL;
using Authoritative.Multiplayer;
using Authoritative.Security;
using Authoritative.Services;
using Authoritative.Services.Grpc;
using Grpc.AspNetCore.Web;
using HotChocolate.AspNetCore;
using HotChocolate.CostAnalysis;
using MagicOnion.Server;
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
builder.Services.AddSingleton<IWorldStreamEmitter, WorldStreamEmitter>();
builder.Services.AddSingleton<WorldStreamRelay>();
builder.Services.AddSingleton<IWorldStreamRelay>(sp => sp.GetRequiredService<WorldStreamRelay>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WorldStreamRelay>());
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
builder.Services.AddSingleton<IAuthoritativeActionService, AuthoritativeActionService>();
builder.Services.AddSingleton<DataLoaderCounters>();
builder.Services.AddSingleton<IGridConfigStore, AdminGridConfigStore>();
builder.Services.AddSingleton<IAdminFileStore, AdminFileStore>();

// GraphQL API layer (HotChocolate). Joins the existing REST action API with a
// cursor-paginated, DataLoader-backed schema for session/world/event reads and
// server-authoritative action submission. Introspection stays visible only in
// dev or when GRAPHQL_ENABLE_PLAYGROUND is set, so it is not exposed in prod.
var graphqlEnabled = string.Equals(builder.Environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase)
    || string.Equals(builder.Configuration["GRAPHQL_ENABLE_PLAYGROUND"], "true", StringComparison.OrdinalIgnoreCase);

var graphqlServer = builder.Services
    .AddGraphQLServer()
    .AddQueryType<GraphQLQuery>()
    .AddMutationType<GraphQLMutation>()
    .AddDataLoader<RoomsBySessionDataLoader>()
    .AddDataLoader<EnemiesBySessionDataLoader>()
    .AddDataLoader<LootBySessionDataLoader>()
    .AddDataLoader<EventsBySessionDataLoader>()
    .AddMaxExecutionDepthRule(32)
    .ModifyCostOptions(o => o.MaxFieldCost = 10000)
    .ModifyRequestOptions(o =>
    {
        o.ExecutionTimeout = TimeSpan.FromSeconds(30);
    });

if (!graphqlEnabled)
    graphqlServer.DisableIntrospection(true);

// Admin gRPC surface (MagicOnion). All gRPC calls are gated by the deny-by-default
// AdminAuthFilter which requires an x-admin-api-key metadata header matching the
// configured admin key. Additive to the existing HTTP admin endpoints.
builder.Services.AddMagicOnion(options =>
{
    options.GlobalFilters.Add<AdminAuthFilter>();
});

var app = builder.Build();
app.UseCors(LocalDevCorsPolicy);
app.UseWebSockets();
app.UseGrpcWeb();

// list all sessions
app.MapGet("/v1/world/sessions", async (
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    var ids = await scylla.GetAllSessionIdsAsync(cancellationToken);
    return Results.Ok(ids);
});

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
        var configuredKey = config["ADMIN_API_KEY"]
            ?? (DevCredentials.AreEnabled(config) ? "dev-admin-key" : null);
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

admin.MapGet("/generators/jobs/{jobId}/ingestion", async (
    string jobId,
    IHeadlessGeneratorService generators,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    var job = generators.GetJob(jobId);
    if (job == null)
        return Results.NotFound(new { error = "generator job not found" });

    // Only world-pipeline jobs are persisted to ScyllaDB.
    var execution = job.Execution;
    var worldPersisted = false;
    var rooms = 0;
    var enemies = 0;
    var loot = 0;

    if (string.Equals(job.GeneratorId, "world-pipeline", StringComparison.Ordinal) && execution != null)
    {
        var sessionId = execution.SessionId ?? job.SessionId ?? execution.ExecutionId;

        // Persistence is background (async channel); retry briefly so a freshly
        // completed job can surface a definitive answer instead of a false "missing".
        const int attempts = 10;
        for (var i = 0; i < attempts; i++)
        {
            var sessionRow = await scylla.GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (sessionRow != null)
            {
                var roomsTask   = scylla.GetRoomsAsync(sessionId, cancellationToken);
                var enemiesTask = scylla.GetEnemiesAsync(sessionId, cancellationToken);
                var lootTask    = scylla.GetLootAsync(sessionId, cancellationToken);
                await Task.WhenAll(roomsTask, enemiesTask, lootTask).ConfigureAwait(false);

                rooms   = (await roomsTask.ConfigureAwait(false)).Count;
                enemies = (await enemiesTask.ConfigureAwait(false)).Count;
                loot    = (await lootTask.ConfigureAwait(false)).Count;
                worldPersisted = true;
                break;
            }

            if (!scylla.IsAvailable())
                break;

            try { await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }

        return Results.Ok(new
        {
            jobId,
            generatorId = job.GeneratorId,
            executionId = execution.ExecutionId,
            sessionId,
            worldPersisted,
            rooms,
            enemies,
            loot,
            scyllaAvailable = scylla.IsAvailable(),
            checkedAtUtc = DateTime.UtcNow
        });
    }

    return Results.Ok(new
    {
        jobId,
        generatorId = job.GeneratorId,
        worldPersisted = false,
        scyllaAvailable = scylla.IsAvailable(),
        checkedAtUtc = DateTime.UtcNow,
        message = "Generator type is not persisted to ScyllaDB; only world-pipeline results are ingested."
    });
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

// ── Admin world explorer / ingestion ────────────────────────────────────────
static GeneratedWorldArtifact SanitizeWorldArtifact(GeneratedWorldArtifact w)
{
    w.Seed         = Math.Clamp(w.Seed, 0, int.MaxValue);
    w.Width        = Math.Clamp(w.Width, 16, 1024);
    w.Height       = Math.Clamp(w.Height, 16, 1024);
    w.DungeonLevel = Math.Clamp(w.DungeonLevel, 0, 100);

    w.Rooms = w.Rooms
        .Where(r => r.Width > 0 && r.Height > 0)
        .Take(4096)
        .Select(r => new WorldRoom
        {
            Id = Math.Clamp(r.Id, 0, int.MaxValue),
            X = Math.Clamp(r.X, 0, w.Width),
            Y = Math.Clamp(r.Y, 0, w.Height),
            Width = Math.Clamp(r.Width, 1, Math.Max(1, w.Width)),
            Height = Math.Clamp(r.Height, 1, Math.Max(1, w.Height)),
        }).ToList();

    w.Enemies = w.Enemies
        .Take(8192)
        .Select(e => new WorldEnemy
        {
            Id = Math.Clamp(e.Id, 0, int.MaxValue),
            Archetype = SanitizeToken(e.Archetype, 32),
            X = Math.Clamp(e.X, 0, w.Width),
            Y = Math.Clamp(e.Y, 0, w.Height),
            Level = Math.Clamp(e.Level, 0, 999),
        }).ToList();

    w.Loot = w.Loot
        .Take(8192)
        .Select(l => new WorldLoot
        {
            ItemId = SanitizeToken(l.ItemId, 64),
            ItemType = SanitizeToken(l.ItemType, 32),
            Tier = SanitizeToken(l.Tier, 16),
            X = Math.Clamp(l.X, 0, w.Width),
            Y = Math.Clamp(l.Y, 0, w.Height),
        }).ToList();

    return w;
}

static string SanitizeToken(string value, int maxLength)
{
    if (string.IsNullOrWhiteSpace(value)) return "unknown";
    var cleaned = new string(value.Trim().Where(c =>
        char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' || c == '/' || c == ' ').ToArray());
    if (string.IsNullOrWhiteSpace(cleaned)) return "unknown";
    return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
}

static PipelineExecutionRecord BuildExecutionFromIngest(string sessionId, WorldIngestRequest req)
{
    var now = DateTime.UtcNow;
    return new PipelineExecutionRecord
    {
        ExecutionId = string.IsNullOrWhiteSpace(req.ExecutionId) ? $"ingest-{Guid.NewGuid():N}" : SanitizeToken(req.ExecutionId, 80),
        PipelineId = string.IsNullOrWhiteSpace(req.PipelineId) ? "admin-ingest" : SanitizeToken(req.PipelineId, 80),
        RequestId = $"ingest-{sessionId}",
        SessionId = sessionId,
        RequestedBy = "admin-ui",
        Notes = req.Notes ?? "Ingested via admin world explorer",
        StartedAtUtc = now,
        CompletedAtUtc = now,
        Status = "completed",
        World = SanitizeWorldArtifact(req.World),
    };
}

admin.MapGet("/world/sessions", async (
    IScyllaWorldPersistenceService scylla,
    CancellationToken ct) =>
{
    var ids = await scylla.GetAllSessionIdsAsync(ct);
    var summaries = new List<object>();
    foreach (var id in ids)
    {
        var row = await scylla.GetSessionAsync(id, ct);
        if (row == null) continue;
        summaries.Add(new
        {
            sessionId = row.SessionId,
            executionId = row.ExecutionId,
            pipelineId = row.PipelineId,
            seed = row.Seed,
            width = row.Width,
            height = row.Height,
            dungeonLevel = row.DungeonLevel,
            roomCount = row.RoomCount,
            enemyCount = row.EnemyCount,
            lootCount = row.LootCount,
            persistedAtUtc = row.CreatedAt,
        });
    }
    summaries = summaries.OrderByDescending(s => ((dynamic)s).persistedAtUtc).ToList();
    return Results.Ok(new { count = summaries.Count, sessions = summaries });
});

admin.MapGet("/world/sessions/{sessionId}", async (
    string sessionId,
    IScyllaWorldPersistenceService scylla,
    CancellationToken ct) =>
{
    var session = await scylla.GetSessionAsync(sessionId, ct);
    if (session == null) return Results.NotFound(new { error = "world session not found" });

    var roomsTask = scylla.GetRoomsAsync(sessionId, ct);
    var enemiesTask = scylla.GetEnemiesAsync(sessionId, ct);
    var lootTask = scylla.GetLootAsync(sessionId, ct);
    var metaTask = scylla.GetSessionMetadataAsync(sessionId, ct);
    await Task.WhenAll(roomsTask, enemiesTask, lootTask, metaTask);

    return Results.Ok(new
    {
        session,
        rooms = await roomsTask,
        enemies = await enemiesTask,
        loot = await lootTask,
        metadata = await metaTask,
    });
});

admin.MapPost("/world/sessions/{sessionId}/ingest", async (
    string sessionId,
    WorldIngestRequest request,
    IScyllaWorldPersistenceService scylla,
    CancellationToken ct) =>
{
    if (request?.World == null || request.World.Rooms == null || request.World.Rooms.Count == 0)
        return Results.BadRequest(new { error = "ingest payload requires a world with at least one room" });

    if (request.World.Rooms.Count > 11000
        || (request.World.Enemies?.Count ?? 0) > 20000
        || (request.World.Loot?.Count ?? 0) > 20000)
        return Results.BadRequest(new { error = "world exceeds ingestion limits (rooms<=11000, enemies<=20000, loot<=20000)" });

    if (!scylla.IsAvailable())
        return Results.Problem(title: "ScyllaDB world persistence is not available", statusCode: 503);

    var record = BuildExecutionFromIngest(sessionId, request);
    var outcome = await scylla.PersistWorldAsync(record, ct);

    if (!outcome.Success)
        return Results.Problem(title: "World ingestion failed", detail: outcome.Error, statusCode: 502);

    return Results.Ok(new
    {
        success = true,
        sessionId = outcome.SessionId,
        executionId = outcome.ExecutionId,
        rooms = outcome.Rooms,
        enemies = outcome.Enemies,
        loot = outcome.Loot,
    });
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
    var adminKey = config["ADMIN_API_KEY"] ?? (DevCredentials.AreEnabled(config) ? "dev-admin-key" : string.Empty);
    var query = $"sessionId={Uri.EscapeDataString(sessionId)}&adminKey={Uri.EscapeDataString(adminKey)}";

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
        LiveStreamUrl = $"{(scheme == "https" ? "wss" : "ws")}://{host}/client/stream/ws?sessionId={Uri.EscapeDataString(sessionId)}",
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

client.MapGet("/stream/ws", async (
    string? sessionId,
    HttpContext http,
    IWorldStreamRelay relay) =>
{
    if (!http.WebSockets.IsWebSocketRequest)
        return Results.BadRequest(new { error = "websocket upgrade required" });

    using var socket = await http.WebSockets.AcceptWebSocketAsync();
    var effectiveSession = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId;

    // Catch up from the Redis hot-cache buffer so recent frames are not missed.
    if (effectiveSession != null)
        await relay.CatchUpAsync(socket, effectiveSession, http.RequestAborted);

    relay.Subscribe(socket, effectiveSession);
    try
    {
        while (socket.State == WebSocketState.Open && !http.RequestAborted.IsCancellationRequested)
        {
            var buffer = new byte[1024];
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), http.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
        }
    }
    catch (OperationCanceledException)
    {
        // client aborted; clean up below
    }
    catch (WebSocketException)
    {
        // client closed abruptly; clean up below
    }
    finally
    {
        relay.Unsubscribe(socket);
    }

    if (socket.State == WebSocketState.Open)
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", http.RequestAborted);

    return Results.Empty;
});

// Write-path approval: entity snapshot + session metadata writes are gated
// behind the signed /client group (ValidateClientSecurityAsync), so a hostile
// caller cannot push arbitrary durable state into Scylla without an
// authenticated, checksummed client request. See SnapshotSender for the client.
client.MapPost("/world/sessions/{sessionId}/snapshots/{entityId}", async (
    HttpRequest req,
    string sessionId,
    string entityId,
    SnapshotWriteRequest request,
    IScyllaWorldPersistenceService scylla,
    CancellationToken cancellationToken) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.SnapshotJson))
        return Results.BadRequest(new { error = "SnapshotJson is required" });

    int ttl = 0;
    int version = 1;
    if (req.Headers.TryGetValue("X-Snapshot-TTL", out var ttlVals) && int.TryParse(ttlVals.ToString(), out var t)) ttl = t;
    if (req.Headers.TryGetValue("X-Snapshot-Version", out var verVals) && int.TryParse(verVals.ToString(), out var v)) version = v;

    var ok = await scylla.InsertEntitySnapshotAsync(sessionId, entityId, request.EntityType ?? "unknown", request.SnapshotJson, version: version, ttlSeconds: ttl, ct: cancellationToken);
    return ok ? Results.Ok(new { sessionId, entityId }) : Results.StatusCode(500);
});

client.MapPost("/world/sessions/{sessionId}/metadata", async (
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

// ── Authoritative action API ──────────────────────────────────────────────
var actions = app.MapGroup("/v1/actions")
    .AddEndpointFilter(ValidateClientSecurityAsync);

actions.MapPost("/{sessionId}", async (
    string sessionId,
    AuthoritativeActionRequest request,
    IAuthoritativeActionService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ActionId))
        return Results.BadRequest(new { error = "actionId is required" });

    if (string.IsNullOrWhiteSpace(request.SessionId))
        request.SessionId = sessionId;

    if (!string.Equals(request.SessionId, sessionId, StringComparison.Ordinal))
        return Results.BadRequest(new { error = "sessionId in body does not match the route" });

    var response = await service.SubmitActionAsync(request, cancellationToken);
    return Results.Ok(response);
});

actions.MapGet("/{sessionId}/state", async (
    string sessionId,
    IAuthoritativeActionService service,
    CancellationToken cancellationToken) =>
{
    var state = await service.GetStateAsync(sessionId, cancellationToken);
    return state is null
        ? Results.NotFound(new { error = "no world available for this session" })
        : Results.Ok(state);
});

actions.MapGet("/{sessionId}/timeline", (
    string sessionId,
    int? take,
    IAuthoritativeActionService service) =>
{
    return Results.Ok(new AuthoritativeTimelineEnvelope
    {
        SessionId = sessionId,
        Events = service.GetTimeline(sessionId, take ?? 50)
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

// ── Admin grid-config surface ──────────────────────────────────────────────
// Lets the admin panel save/load terrain/dungeon grid configurations. Additive,
// authenticated via the /admin group filter (nginx sets X-Admin-Key). The panel
// owns the shape of the gridConfig object; the backend stores it as opaque JSON.
admin.MapPost("/grid-configs/{gridId}", (
    string gridId,
    GridConfigSaveRequest request,
    IGridConfigStore grids) =>
{
    if (string.IsNullOrWhiteSpace(gridId) || request?.GridConfig == null)
        return Results.BadRequest(new { error = "gridId and gridConfig are required" });

    var json = JsonSerializer.Serialize(request.GridConfig);
    grids.Save(gridId, json);
    return Results.Ok(new { gridId = gridId.Trim(), saved = true });
});

admin.MapGet("/grid-configs", (IGridConfigStore grids) =>
{
    var configs = grids.GetAll()
        .Select(c => new { gridId = c.GridId, savedAtUtc = c.SavedAtUtc })
        .ToList();
    return Results.Ok(new { count = configs.Count, configs });
});

admin.MapGet("/grid-configs/{gridId}", (string gridId, IGridConfigStore grids) =>
{
    if (!grids.TryGet(gridId, out var config))
        return Results.NotFound(new { error = "grid config not found" });

    object? parsed;
    try
    {
        parsed = JsonSerializer.Deserialize<object>(config!.GridConfigJson);
    }
    catch
    {
        return Results.Problem(title: "grid config is not valid JSON", statusCode: 500);
    }

    return Results.Ok(new { gridId = config!.GridId, savedAtUtc = config.SavedAtUtc, gridConfig = parsed });
});

// ── Admin file-store surface ───────────────────────────────────────────────
// Lets the admin panel list/upload/download/delete 3D asset files and extracted
// archive contents. Additive and authenticated via the /admin group filter.
admin.MapGet("/files", (IAdminFileStore files) =>
{
    var list = files.List()
        .Select(f => new
        {
            id = f.Id,
            name = f.Name,
            size = f.Size,
            fileType = f.FileType,
            uploadedAtUnixMs = f.UploadedAtUnixMs,
            relativePath = f.RelativePath,
            isDirectory = f.IsDirectory,
            archiveType = f.ArchiveType,
            extractionSourceId = f.ExtractionSourceId
        })
        .ToList();
    return Results.Ok(new { count = list.Count, files = list });
});

admin.MapPost("/files", (AdminFileWriteRequest request, IAdminFileStore files) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.DataBase64))
        return Results.BadRequest(new { error = "name and dataBase64 are required" });

    byte[] contents;
    try
    {
        contents = Convert.FromBase64String(request.DataBase64);
    }
    catch (FormatException)
    {
        return Results.BadRequest(new { error = "dataBase64 is not valid base64" });
    }

    var meta = new AdminFileMeta
    {
        Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
        Name = request.Name.Trim(),
        Size = request.Size >= 0 ? request.Size : contents.Length,
        FileType = string.IsNullOrWhiteSpace(request.FileType) ? "GLB" : request.FileType.Trim(),
        UploadedAtUnixMs = request.UploadedAtUnixMs,
        RelativePath = request.RelativePath ?? string.Empty,
        IsDirectory = request.IsDirectory,
        ArchiveType = request.ArchiveType,
        ExtractionSourceId = request.ExtractionSourceId
    };

    var id = files.Save(meta, contents);
    return Results.Ok(new { id, size = contents.Length });
});

admin.MapGet("/files/{fileId}/download", (string fileId, IAdminFileStore files) =>
{
    if (!files.TryGet(fileId, out var meta, out var contents))
        return Results.NotFound(new { error = "file not found" });

    return Results.File(contents!, "application/octet-stream", meta!.Name);
});

admin.MapDelete("/files/{fileId}", (string fileId, IAdminFileStore files) =>
{
    return files.Delete(fileId)
        ? Results.Ok(new { ok = true })
        : Results.NotFound(new { error = "file not found" });
});

// ── GraphQL API ────────────────────────────────────────────────────────────
// Served at /graphql and gated with the SAME client security used by the
// /client REST group so queries/mutations require the client API key.
app.MapGraphQL("/graphql")
    .AddEndpointFilter(ValidateClientSecurityAsync);

// Admin gRPC-Web surface (MagicOnion). gRPC-Web is active via app.UseGrpcWeb() above.
app.MapMagicOnionService();

await app.RunAsync();

record SnapshotWriteRequest(string EntityType, string SnapshotJson);

record GridConfigSaveRequest(object GridConfig);

record AdminFileWriteRequest(
    string? Id,
    string? Name,
    long Size,
    string? FileType,
    long UploadedAtUnixMs,
    string? RelativePath,
    bool IsDirectory,
    string? ArchiveType,
    string? ExtractionSourceId,
    string? DataBase64);
#endif
