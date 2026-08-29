using Microsoft.Extensions.Hosting;

namespace Authoritative.Services;

public sealed class AuthoritativeHeartbeatService : BackgroundService
{
    static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    readonly IAuthoritativeMetrics _metrics;

    public AuthoritativeHeartbeatService(IAuthoritativeMetrics metrics)
    {
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _metrics.MarkHeartbeat(DateTimeOffset.UtcNow);

            try
            {
                await Task.Delay(HeartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
