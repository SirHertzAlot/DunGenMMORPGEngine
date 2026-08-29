#if !UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Authoritative.Services
{
    public sealed class PipelineRuntimeService : IHostedService, IDisposable
    {
        private static readonly Counter Reloads = Metrics.CreateCounter(
            "authoritative_pipeline_reloads_total",
            "Number of pipeline hot reload operations.");

        private static readonly Counter ReloadFailures = Metrics.CreateCounter(
            "authoritative_pipeline_reload_failures_total",
            "Number of failed pipeline reload operations.");

        private readonly ILogger<PipelineRuntimeService> _log;
        private readonly IDeserializer _deserializer;
        private readonly string _definitionsDirectory;
        private readonly string _activeFilePath;
        private readonly object _lock = new();
        private FileSystemWatcher? _watcher;

        private PipelineRuntimeSnapshot _snapshot = new();

        public PipelineRuntimeService(ILogger<PipelineRuntimeService> log)
            : this(log, Path.Combine(AppContext.BaseDirectory, "data", "pipeline"))
        {
        }

        public PipelineRuntimeService(ILogger<PipelineRuntimeService> log, string definitionsDirectory)
        {
            _log = log;
            _definitionsDirectory = definitionsDirectory;
            _activeFilePath = Path.Combine(_definitionsDirectory, "active-pipeline.yaml");

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            Directory.CreateDirectory(_definitionsDirectory);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ReloadNow();

            _watcher = new FileSystemWatcher(_definitionsDirectory, "*.yaml")
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnDefinitionChanged;
            _watcher.Created += OnDefinitionChanged;
            _watcher.Renamed += OnDefinitionChanged;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            DisposeWatcher();
            return Task.CompletedTask;
        }

        public void ReloadNow()
        {
            try
            {
                if (!File.Exists(_activeFilePath))
                {
                    lock (_lock)
                    {
                        _snapshot = new PipelineRuntimeSnapshot
                        {
                            IsLoaded = false,
                            ActiveDefinitionPath = _activeFilePath,
                            LastLoadedAtUtc = DateTime.UtcNow,
                            DefinitionHash = null,
                            ActiveDefinition = null
                        };
                    }

                    return;
                }

                var raw = File.ReadAllText(_activeFilePath, Encoding.UTF8);
                var parsed = _deserializer.Deserialize<PipelineDefinition>(raw);
                if (parsed == null)
                    throw new InvalidOperationException("Pipeline definition is empty.");

                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

                lock (_lock)
                {
                    _snapshot = new PipelineRuntimeSnapshot
                    {
                        IsLoaded = true,
                        ActiveDefinitionPath = _activeFilePath,
                        LastLoadedAtUtc = DateTime.UtcNow,
                        DefinitionHash = hash,
                        ActiveDefinition = parsed
                    };
                }

                Reloads.Inc();
                _log.LogInformation("Pipeline hot-reloaded from {path} with id={pipelineId}", _activeFilePath, parsed.PipelineId);
            }
            catch (Exception ex)
            {
                ReloadFailures.Inc();
                _log.LogError(ex, "Failed to hot-reload pipeline definition from {path}", _activeFilePath);
            }
        }

        public PipelineRuntimeSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new PipelineRuntimeSnapshot
                {
                    IsLoaded = _snapshot.IsLoaded,
                    ActiveDefinitionPath = _snapshot.ActiveDefinitionPath,
                    LastLoadedAtUtc = _snapshot.LastLoadedAtUtc,
                    DefinitionHash = _snapshot.DefinitionHash,
                    ActiveDefinition = _snapshot.ActiveDefinition
                };
            }
        }

        public void Dispose()
        {
            DisposeWatcher();
            GC.SuppressFinalize(this);
        }

        private void OnDefinitionChanged(object sender, FileSystemEventArgs e)
        {
            if (!string.Equals(e.FullPath, _activeFilePath, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                ReloadNow();
            }
            catch
            {
                // ReloadNow already logs and tracks failures.
            }
        }

        private void DisposeWatcher()
        {
            if (_watcher == null)
                return;

            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnDefinitionChanged;
            _watcher.Created -= OnDefinitionChanged;
            _watcher.Renamed -= OnDefinitionChanged;
            _watcher.Dispose();
            _watcher = null;
        }
    }
}
#endif
