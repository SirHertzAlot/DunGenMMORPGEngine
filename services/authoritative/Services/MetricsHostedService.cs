#if !UNITY_5_3_OR_NEWER
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Authoritative.Services
{
    /// <summary>
    /// Exposes Prometheus metrics from the authoritative worker process.
    /// </summary>
    public sealed class MetricsHostedService : IHostedService
    {
        private readonly ILogger<MetricsHostedService> _log;
        private KestrelMetricServer? _server;

        public MetricsHostedService(ILogger<MetricsHostedService> log)
        {
            _log = log;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var metricsPort = ReadMetricsPort();
            _server = new KestrelMetricServer(port: metricsPort);
            _server.Start();
            _log.LogInformation("Prometheus metrics endpoint listening on :{port}/metrics", metricsPort);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_server == null)
                return;

            await _server.StopAsync();
            _server = null;
        }

        private static int ReadMetricsPort()
        {
            var raw = Environment.GetEnvironmentVariable("METRICS_PORT");
            return int.TryParse(raw, out var port) && port > 0 ? port : 9464;
        }
    }
}
#endif
