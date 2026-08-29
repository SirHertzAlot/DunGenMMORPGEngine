#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Authoritative.Services
{
    public interface IContainerHealthService
    {
        Task<IReadOnlyCollection<ServiceHealthStatus>> GetHealthAsync(CancellationToken cancellationToken);
        Task<IReadOnlyCollection<ContainerLogInsight>> GetLogInsightsAsync(int tail, CancellationToken cancellationToken);
        Task<ContainerLogFull> GetContainerLogsAsync(string containerName, int tail, bool timestamps, CancellationToken cancellationToken);
        IReadOnlyCollection<string> GetKnownContainerNames();
    }

    public sealed class ContainerHealthService : IContainerHealthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ContainerHealthService> _log;

        public ContainerHealthService(IHttpClientFactory httpClientFactory, ILogger<ContainerHealthService> log)
        {
            _httpClientFactory = httpClientFactory;
            _log = log;
        }

        public async Task<IReadOnlyCollection<ServiceHealthStatus>> GetHealthAsync(CancellationToken cancellationToken)
        {
            var probes = BuildProbes();
            var tasks = probes.Select(probe => CheckProbeAsync(probe, cancellationToken));
            var results = await Task.WhenAll(tasks);
            return results.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        }

        public async Task<IReadOnlyCollection<ContainerLogInsight>> GetLogInsightsAsync(int tail, CancellationToken cancellationToken)
        {
            var normalizedTail = Math.Clamp(tail, 50, 2000);
            var containerNames = BuildContainerNames();
            var tasks = containerNames.Select(name => ReadContainerLogInsightAsync(name, normalizedTail, cancellationToken));
            var results = await Task.WhenAll(tasks);
            return results.OrderBy(x => x.ContainerName, StringComparer.Ordinal).ToArray();
        }

        public IReadOnlyCollection<string> GetKnownContainerNames() => BuildContainerNames();

        public async Task<ContainerLogFull> GetContainerLogsAsync(string containerName, int tail, bool timestamps, CancellationToken cancellationToken)
        {
            var normalizedTail = Math.Clamp(tail, 10, 5000);
            var normalizedName = containerName.Trim();
            var started = DateTime.UtcNow;

            var args = timestamps
                ? $"logs --tail {normalizedTail} --timestamps {normalizedName}"
                : $"logs --tail {normalizedTail} {normalizedName}";

            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    return new ContainerLogFull
                    {
                        ContainerName = normalizedName,
                        CapturedAtUtc = started,
                        Available = false,
                        Error = "docker process could not be started",
                        TailRequested = normalizedTail
                    };
                }

                var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                // docker logs writes to stderr by default even for stdout content
                var combined = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
                if (!string.IsNullOrWhiteSpace(stdout) && !string.IsNullOrWhiteSpace(stderr))
                    combined = stdout + stderr;

                if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(combined))
                {
                    return new ContainerLogFull
                    {
                        ContainerName = normalizedName,
                        CapturedAtUtc = started,
                        Available = false,
                        Error = string.IsNullOrWhiteSpace(stderr) ? $"exit code {process.ExitCode}" : stderr.Trim(),
                        TailRequested = normalizedTail
                    };
                }

                var lines = combined
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                return new ContainerLogFull
                {
                    ContainerName = normalizedName,
                    CapturedAtUtc = started,
                    Available = true,
                    TailRequested = normalizedTail,
                    Lines = lines
                };
            }
            catch (Exception ex)
            {
                return new ContainerLogFull
                {
                    ContainerName = normalizedName,
                    CapturedAtUtc = started,
                    Available = false,
                    Error = ex.Message,
                    TailRequested = normalizedTail
                };
            }
        }

        private async Task<ServiceHealthStatus> CheckProbeAsync(ServiceProbe probe, CancellationToken cancellationToken)
        {
            var started = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (probe.Kind == ProbeKind.Http)
                    return await CheckHttpAsync(probe, started, stopwatch, cancellationToken);

                return await CheckTcpAsync(probe, started, stopwatch, cancellationToken);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Health probe failed for {probe}", probe.Name);
                return new ServiceHealthStatus
                {
                    Name = probe.Name,
                    Kind = probe.Kind.ToString().ToLowerInvariant(),
                    Target = probe.Target,
                    IsOnline = false,
                    CheckedAtUtc = started,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Message = ex.Message
                };
            }
        }

        private async Task<ServiceHealthStatus> CheckHttpAsync(ServiceProbe probe, DateTime started, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(probe.Target, timeoutCts.Token);
            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            var message = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? ""
                : body.Trim();

            return new ServiceHealthStatus
            {
                Name = probe.Name,
                Kind = probe.Kind.ToString().ToLowerInvariant(),
                Target = probe.Target,
                IsOnline = response.IsSuccessStatusCode,
                CheckedAtUtc = started,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                StatusCode = (int)response.StatusCode,
                Message = message.Length > 200 ? message[..200] : message
            };
        }

        private static async Task<ServiceHealthStatus> CheckTcpAsync(ServiceProbe probe, DateTime started, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

            using var client = new TcpClient();
            await client.ConnectAsync(probe.Host!, probe.Port!.Value, timeoutCts.Token);

            return new ServiceHealthStatus
            {
                Name = probe.Name,
                Kind = probe.Kind.ToString().ToLowerInvariant(),
                Target = probe.Target,
                IsOnline = true,
                CheckedAtUtc = started,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Message = "tcp open"
            };
        }

        private async Task<ContainerLogInsight> ReadContainerLogInsightAsync(string containerName, int tail, CancellationToken cancellationToken)
        {
            var started = DateTime.UtcNow;
            var args = $"logs --tail {tail} {containerName}";
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    return UnavailableInsight(containerName, started, "docker process could not be started");
                }

                var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                var output = await stdoutTask;
                var error = await stderrTask;

                if (process.ExitCode != 0)
                {
                    var message = string.IsNullOrWhiteSpace(error) ? $"docker exited with code {process.ExitCode}" : error.Trim();
                    return UnavailableInsight(containerName, started, message);
                }

                var lines = output
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .ToArray();

                var errorCount = lines.Count(line => ContainsToken(line, "error") || ContainsToken(line, "exception") || ContainsToken(line, "fatal"));
                var warningCount = lines.Count(line => ContainsToken(line, "warn"));

                return new ContainerLogInsight
                {
                    ContainerName = containerName,
                    CapturedAtUtc = started,
                    SourceAvailable = true,
                    LineCount = lines.Length,
                    ErrorCount = errorCount,
                    WarningCount = warningCount,
                    LastLines = lines.TakeLast(12).ToArray(),
                    HealthHint = BuildHint(lines.Length, errorCount, warningCount)
                };
            }
            catch (Exception ex)
            {
                return UnavailableInsight(containerName, started, ex.Message);
            }
        }

        private static bool ContainsToken(string line, string token)
        {
            return line.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildHint(int lineCount, int errorCount, int warningCount)
        {
            if (lineCount == 0)
                return "no recent logs";
            if (errorCount > 0)
                return "errors detected";
            if (warningCount > 0)
                return "warnings detected";
            return "healthy activity";
        }

        private static ContainerLogInsight UnavailableInsight(string containerName, DateTime started, string reason)
        {
            return new ContainerLogInsight
            {
                ContainerName = containerName,
                CapturedAtUtc = started,
                SourceAvailable = false,
                HealthHint = "log source unavailable",
                Message = reason,
                LastLines = Array.Empty<string>()
            };
        }

        private static IReadOnlyCollection<ServiceProbe> BuildProbes()
        {
            return new[]
            {
                ServiceProbe.Http("admin-ui", "http://admin-ui/"),
                ServiceProbe.Http("authoritative-primary", "http://authoritative-primary/healthz"),
                ServiceProbe.Http("authoritative-secondary", "http://authoritative-secondary/healthz"),
                ServiceProbe.Http("generator-service", "http://generator-service:3000/healthz"),
                ServiceProbe.Http("prometheus", "http://prometheus:9090/-/healthy"),
                ServiceProbe.Http("grafana", "http://grafana:3000/api/health"),
                ServiceProbe.Http("redis-exporter", "http://redis-exporter:9121/metrics"),
                ServiceProbe.Http("postgres-exporter", "http://postgres-exporter:9187/metrics"),
                ServiceProbe.Http("rabbitmq-exporter", "http://rabbitmq-exporter:9419/metrics"),
                ServiceProbe.Tcp("redis", "redis", 6379),
                ServiceProbe.Tcp("postgres", "postgres", 5432),
                ServiceProbe.Tcp("rabbitmq", "rabbitmq", 5672),
                ServiceProbe.Tcp("scylla", "scylla", 9042)
            };
        }

        private static IReadOnlyCollection<string> BuildContainerNames()
        {
            return new[]
            {
                "admin-ui-zip",
                "authoritative-primary",
                "authoritative-secondary",
                "generator-service",
                "prometheus",
                "grafana",
                "redis",
                "postgres",
                "rabbitmq",
                "scylla",
                "redis-exporter",
                "postgres-exporter",
                "rabbitmq-exporter"
            };
        }
    }

    public sealed class ServiceHealthStatus
    {
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public int? StatusCode { get; set; }
        public long ResponseTimeMs { get; set; }
        public DateTime CheckedAtUtc { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class ContainerLogInsight
    {
        public string ContainerName { get; set; } = string.Empty;
        public DateTime CapturedAtUtc { get; set; }
        public bool SourceAvailable { get; set; }
        public int LineCount { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public string HealthHint { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public IReadOnlyCollection<string> LastLines { get; set; } = Array.Empty<string>();
    }

    public sealed class ContainerLogFull
    {
        public string ContainerName { get; set; } = string.Empty;
        public DateTime CapturedAtUtc { get; set; }
        public bool Available { get; set; }
        public string? Error { get; set; }
        public int TailRequested { get; set; }
        public string[] Lines { get; set; } = Array.Empty<string>();
    }

    internal enum ProbeKind
    {
        Http,
        Tcp
    }

    internal sealed class ServiceProbe
    {
        private ServiceProbe()
        {
        }

        public string Name { get; init; } = string.Empty;
        public ProbeKind Kind { get; init; }
        public string Target { get; init; } = string.Empty;
        public string? Host { get; init; }
        public int? Port { get; init; }

        public static ServiceProbe Http(string name, string target)
        {
            return new ServiceProbe
            {
                Name = name,
                Kind = ProbeKind.Http,
                Target = target
            };
        }

        public static ServiceProbe Tcp(string name, string host, int port)
        {
            return new ServiceProbe
            {
                Name = name,
                Kind = ProbeKind.Tcp,
                Host = host,
                Port = port,
                Target = $"{host}:{port}"
            };
        }
    }
}
#endif