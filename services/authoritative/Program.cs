using Authoritative.Diagnostics;
using Authoritative.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var stackRole = Environment.GetEnvironmentVariable("AUTHORITATIVE_STACK_ROLE");
var releaseChannel = Environment.GetEnvironmentVariable("AUTHORITATIVE_RELEASE_CHANNEL");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<Authoritative.Domain.IItemGenerator, Authoritative.Domain.ItemGenerator>();
        services.AddSingleton<IGeneratedItemStore>(_ =>
        {
            var dataDirectory = context.Configuration["AUTHORITATIVE_DATA_DIR"];
            return string.IsNullOrWhiteSpace(dataDirectory)
                ? new GeneratedItemStore()
                : new GeneratedItemStore(dataDirectory);
        });
        services.AddSingleton<IDiagnosticLogStore>(_ =>
        {
            var dataDirectory = context.Configuration["AUTHORITATIVE_DATA_DIR"];
            return string.IsNullOrWhiteSpace(dataDirectory)
                ? new DiagnosticLogStore()
                : new DiagnosticLogStore(dataDirectory);
        });
        services.AddSingleton<IAuthoritativeMetrics, AuthoritativeMetrics>();
        services.AddHostedService<QueueConsumer>();
        services.AddHostedService<DiagnosticHttpService>();
        services.AddHostedService<AuthoritativeHeartbeatService>();
    })
    .ConfigureLogging((ctx, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

host.Services.GetRequiredService<IDiagnosticLogStore>().Record(new DiagnosticLogWriteRequest
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

await host.RunAsync();
