#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Authoritative.Diagnostics
{
    /// <summary>
    /// Routes all .NET ILogger calls into IDiagnosticLogStore so they appear
    /// in the /admin/v1/diagnostics/logs/query results.
    /// </summary>
    public sealed class DiagnosticLogStoreLoggerProvider : ILoggerProvider
    {
        private readonly IDiagnosticLogStore _store;
        private readonly ConcurrentDictionary<string, DiagnosticLogStoreLogger> _loggers = new(StringComparer.Ordinal);

        public DiagnosticLogStoreLoggerProvider(IDiagnosticLogStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name => new DiagnosticLogStoreLogger(name, _store));

        public void Dispose() => _loggers.Clear();
    }

    internal sealed class DiagnosticLogStoreLogger : ILogger
    {
        private readonly string _category;
        private readonly IDiagnosticLogStore _store;

        // Guard flag prevents re-entrant logging if the store itself emits log calls.
        [System.ThreadStatic]
        private static bool _recording;

        internal DiagnosticLogStoreLogger(string category, IDiagnosticLogStore store)
        {
            _category = category;
            _store = store;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || _recording) return;

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception == null) return;

            // Derive a short category and event name from the fully-qualified logger name.
            // "Authoritative.Services.WorldEventPersistenceService" →
            //   category = "services", eventName = "dotnet.worldeventpersistenceservice"
            var parts = _category.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var shortCategory = parts.Length >= 2
                ? parts[^2].ToLowerInvariant()
                : (parts.Length == 1 ? parts[0].ToLowerInvariant() : "general");
            var shortName = parts.Length >= 1
                ? parts[^1].ToLowerInvariant()
                : "log";
            var eventName = $"dotnet.{shortName}";

            var level = logLevel switch
            {
                LogLevel.Trace    => "Trace",
                LogLevel.Debug    => "Debug",
                LogLevel.Warning  => "Warning",
                LogLevel.Error    => "Error",
                LogLevel.Critical => "Critical",
                _                 => "Information"
            };

            _recording = true;
            try
            {
                _store.Record(new DiagnosticLogWriteRequest
                {
                    Level          = level,
                    Category       = shortCategory,
                    EventName      = eventName,
                    Message        = message,
                    RetentionClass = logLevel >= LogLevel.Warning ? "operational" : "debug",
                }, exception);
            }
            catch
            {
                // Never let logging infrastructure throw — swallow silently.
            }
            finally
            {
                _recording = false;
            }
        }
    }
}
#endif
