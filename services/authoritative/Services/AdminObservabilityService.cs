#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Authoritative.Services
{
    public interface IAdminObservabilityService
    {
        void RecordAction(string actionType, string outcome, IReadOnlyDictionary<string, string>? metadata = null);
        void RecordExecution(PipelineExecutionRecord execution);
        void RecordWorldEvent(WorldSessionEvent worldEvent);
        AdminObservabilitySnapshot GetSnapshot(
            PipelineRuntimeSnapshot runtime,
            PipelineExecutionRecord? latestExecution,
            IReadOnlyCollection<PersistedGeneratedItem> generatedItems,
            string? sessionId = null);
        IReadOnlyCollection<AdminObservabilityEvent> GetRecentEvents(int take = 100, string? sessionId = null);
        IReadOnlyCollection<WorldSessionEvent> GetSessionTimeline(string sessionId, int take = 200);
    }

    public sealed class AdminObservabilityService : IAdminObservabilityService
    {
        private readonly ConcurrentQueue<AdminObservabilityEvent> _events = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<WorldSessionEvent>> _sessionTimelines = new(StringComparer.Ordinal);
        private readonly object _executionLock = new();
        private readonly int _maxEvents;
        private readonly int _maxSessionTimelineEvents;
        private PipelineExecutionRecord? _latestExecution;

        public AdminObservabilityService(int maxEvents = 500, int maxSessionTimelineEvents = 1000)
        {
            _maxEvents = Math.Max(50, maxEvents);
            _maxSessionTimelineEvents = Math.Max(100, maxSessionTimelineEvents);
        }

        public void RecordAction(string actionType, string outcome, IReadOnlyDictionary<string, string>? metadata = null)
        {
            Enqueue(new AdminObservabilityEvent
            {
                EventId = "evt_" + Guid.NewGuid().ToString("N"),
                Type = "action",
                TimestampUtc = DateTime.UtcNow,
                Message = $"Action '{actionType}' processed with outcome '{outcome}'.",
                Data = BuildData(actionType, outcome, metadata)
            });
        }

        public void RecordExecution(PipelineExecutionRecord execution)
        {
            lock (_executionLock)
            {
                _latestExecution = execution;
            }

            Enqueue(new AdminObservabilityEvent
            {
                EventId = "evt_" + Guid.NewGuid().ToString("N"),
                Type = "pipeline-execution",
                TimestampUtc = DateTime.UtcNow,
                Message = $"Pipeline execution completed: {execution.ExecutionId}.",
                Data = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["executionId"] = execution.ExecutionId,
                    ["pipelineId"] = execution.PipelineId,
                    ["requestId"] = execution.RequestId,
                    ["sessionId"] = execution.SessionId ?? string.Empty,
                    ["status"] = execution.Status,
                    ["rooms"] = execution.World.Rooms.Count.ToString(),
                    ["enemies"] = execution.World.Enemies.Count.ToString(),
                    ["loot"] = execution.World.Loot.Count.ToString()
                }
            });
        }

        public void RecordWorldEvent(WorldSessionEvent worldEvent)
        {
            if (string.IsNullOrWhiteSpace(worldEvent.SessionId))
                return;

            worldEvent.EventId = string.IsNullOrWhiteSpace(worldEvent.EventId)
                ? "wevt_" + Guid.NewGuid().ToString("N")
                : worldEvent.EventId;
            if (worldEvent.TimestampUtc == default)
                worldEvent.TimestampUtc = DateTime.UtcNow;

            var timeline = _sessionTimelines.GetOrAdd(worldEvent.SessionId, _ => new ConcurrentQueue<WorldSessionEvent>());
            timeline.Enqueue(worldEvent);
            while (timeline.Count > _maxSessionTimelineEvents && timeline.TryDequeue(out _))
            {
            }

            Enqueue(new AdminObservabilityEvent
            {
                EventId = "evt_" + Guid.NewGuid().ToString("N"),
                Type = "world-session",
                TimestampUtc = worldEvent.TimestampUtc,
                Message = worldEvent.Message,
                Data = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sessionId"] = worldEvent.SessionId,
                    ["eventType"] = worldEvent.EventType,
                    ["category"] = worldEvent.Category,
                    ["frame"] = worldEvent.Frame.ToString(),
                    ["entityId"] = worldEvent.EntityId
                }
            });
        }

        public AdminObservabilitySnapshot GetSnapshot(
            PipelineRuntimeSnapshot runtime,
            PipelineExecutionRecord? latestExecution,
            IReadOnlyCollection<PersistedGeneratedItem> generatedItems,
            string? sessionId = null)
        {
            PipelineExecutionRecord? execution = latestExecution;
            if (execution == null)
            {
                lock (_executionLock)
                {
                    execution = _latestExecution;
                }
            }

            var normalizedSessionId = NormalizeSessionId(sessionId);
            var filteredItems = generatedItems;
            if (!string.IsNullOrEmpty(normalizedSessionId))
            {
                filteredItems = generatedItems
                    .Where(item => item.Metadata.TryGetValue("sessionId", out var itemSessionId) &&
                                   string.Equals(itemSessionId, normalizedSessionId, StringComparison.Ordinal))
                    .ToArray();

                if (execution != null && !string.Equals(execution.SessionId, normalizedSessionId, StringComparison.Ordinal))
                    execution = null;
            }

            var items = filteredItems
                .OrderByDescending(x => x.SavedAtUtc)
                .Take(10)
                .ToArray();

            return new AdminObservabilitySnapshot
            {
                CapturedAtUtc = DateTime.UtcNow,
                SessionFilter = normalizedSessionId,
                Runtime = runtime,
                LatestExecution = execution,
                GeneratedItemCount = filteredItems.Count,
                RecentItems = items,
                RecentEvents = GetRecentEvents(50, normalizedSessionId),
                SessionTimeline = !string.IsNullOrEmpty(normalizedSessionId)
                    ? GetSessionTimeline(normalizedSessionId, 200)
                    : Array.Empty<WorldSessionEvent>()
            };
        }

        public IReadOnlyCollection<AdminObservabilityEvent> GetRecentEvents(int take = 100, string? sessionId = null)
        {
            var normalizedSessionId = NormalizeSessionId(sessionId);
            IEnumerable<AdminObservabilityEvent> query = _events;
            if (!string.IsNullOrEmpty(normalizedSessionId))
            {
                query = query.Where(evt => evt.Data.TryGetValue("sessionId", out var eventSessionId) &&
                                           string.Equals(eventSessionId, normalizedSessionId, StringComparison.Ordinal));
            }

            return query
                .Reverse()
                .Take(Math.Max(1, take))
                .ToArray();
        }

        public IReadOnlyCollection<WorldSessionEvent> GetSessionTimeline(string sessionId, int take = 200)
        {
            var normalizedSessionId = NormalizeSessionId(sessionId);
            if (string.IsNullOrEmpty(normalizedSessionId))
                return Array.Empty<WorldSessionEvent>();

            if (!_sessionTimelines.TryGetValue(normalizedSessionId, out var timeline))
                return Array.Empty<WorldSessionEvent>();

            return timeline
                .Reverse()
                .Take(Math.Max(1, take))
                .ToArray();
        }

        private void Enqueue(AdminObservabilityEvent evt)
        {
            _events.Enqueue(evt);
            while (_events.Count > _maxEvents && _events.TryDequeue(out _))
            {
            }
        }

        private static Dictionary<string, string> BuildData(string actionType, string outcome, IReadOnlyDictionary<string, string>? metadata)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["actionType"] = actionType,
                ["outcome"] = outcome
            };

            if (metadata != null)
            {
                foreach (var kv in metadata)
                {
                    data[kv.Key] = kv.Value;
                }
            }

            return data;
        }

        private static string? NormalizeSessionId(string? sessionId)
        {
            return string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim();
        }
    }

    public sealed class AdminObservabilityEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, string> Data { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class AdminObservabilitySnapshot
    {
        public DateTime CapturedAtUtc { get; set; }
        public string? SessionFilter { get; set; }
        public PipelineRuntimeSnapshot Runtime { get; set; } = new();
        public PipelineExecutionRecord? LatestExecution { get; set; }
        public int GeneratedItemCount { get; set; }
        public IReadOnlyCollection<PersistedGeneratedItem> RecentItems { get; set; } = Array.Empty<PersistedGeneratedItem>();
        public IReadOnlyCollection<AdminObservabilityEvent> RecentEvents { get; set; } = Array.Empty<AdminObservabilityEvent>();
        public IReadOnlyCollection<WorldSessionEvent> SessionTimeline { get; set; } = Array.Empty<WorldSessionEvent>();
    }
}
#endif
