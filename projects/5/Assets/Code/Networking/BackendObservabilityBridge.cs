using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DunGen.Events;
using DunGen.Events.Combat;
using UnityEngine;
using UnityEngine.Networking;

namespace DunGen.Networking
{
    public sealed class BackendObservabilityBridge : MonoBehaviour
    {
        [SerializeField] private BackendConnectionConfig connectionConfig;
        [SerializeField] private string sessionIdOverride = "";
        [SerializeField] private bool enabledOnStart = true;

        public event Action<WorldSessionEventIngestDto> EventQueuedForPost;

        private EventBus _eventBus;
        private bool _isSubscribed;
        private float _nextConnectivityLogTime;

        public static bool TryEmitClientEvent(
            string eventType,
            string category,
            string entityId,
            string message,
            Dictionary<string, string> data = null,
            uint frame = 0)
        {
            var bridge = FindAnyObjectByType<BackendObservabilityBridge>();
            if (bridge == null)
                return false;

            bridge.PostEvent(new WorldSessionEventIngestDto
            {
                eventType = string.IsNullOrWhiteSpace(eventType) ? "client.event" : eventType,
                category = string.IsNullOrWhiteSpace(category) ? "client" : category,
                frame = frame,
                entityId = entityId ?? string.Empty,
                message = string.IsNullOrWhiteSpace(message) ? "Client event" : message,
                data = data,
            });
            return true;
        }

        private string SessionId => string.IsNullOrWhiteSpace(sessionIdOverride)
            ? (connectionConfig != null ? connectionConfig.DefaultSessionId : "session-001")
            : sessionIdOverride.Trim();

        /// <summary>Inject config at runtime (used by <see cref="NetworkingBootstrap"/>).</summary>
        public void SetConfig(BackendConnectionConfig config) => connectionConfig = config;

        private void OnEnable()
        {
            if (!enabledOnStart)
                return;

            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Subscribe()
        {
            if (_isSubscribed)
                return;

            _eventBus = EventBus.Instance;
            _eventBus.AnyEventPublished += HandleAnyEventPublished;
            _isSubscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_isSubscribed || _eventBus == null)
                return;

            _eventBus.AnyEventPublished -= HandleAnyEventPublished;
            _isSubscribed = false;
        }

        private void HandleAnyEventPublished(Type eventType, object eventPayload)
        {
            if (eventType == null || eventPayload == null)
                return;

            var eventTypeName = TrimEventSuffix(eventType.Name);
            var frame = ReadUIntField(eventPayload, "FrameNumber", "Frame");
            var entityId = ReadStringField(eventPayload,
                "EntityId",
                "SourceEntityId",
                "VictimEntityId",
                "RecipientEntityId",
                "DeceasedEntityId",
                "CombatSessionId",
                "NextActorId");

            PostEvent(new WorldSessionEventIngestDto
            {
                eventType = $"ecs.{ToKebab(eventTypeName)}",
                category = InferCategory(eventTypeName),
                frame = frame,
                entityId = entityId,
                message = BuildSummaryMessage(eventTypeName, eventPayload),
                data = BuildPayloadData(eventTypeName, eventPayload)
            });
        }

        private static Dictionary<string, string> BuildPayloadData(string eventTypeName, object eventPayload)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["eventTypeName"] = eventTypeName,
                ["unityTime"] = Time.unscaledTime.ToString("F3"),
            };

            var fields = eventPayload.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                var value = fields[i].GetValue(eventPayload);
                if (value == null)
                    continue;

                data[fields[i].Name] = value.ToString();
            }

            return data;
        }

        private static string TrimEventSuffix(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return "event";

            return typeName.EndsWith("EventData", StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - "EventData".Length)
                : typeName;
        }

        private static string ToKebab(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "event";

            var sb = new StringBuilder(input.Length + 8);
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (char.IsUpper(c) && i > 0)
                    sb.Append('-');

                sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        private static string InferCategory(string eventTypeName)
        {
            if (eventTypeName.Contains("Combat", StringComparison.OrdinalIgnoreCase)
                || eventTypeName.Contains("Damage", StringComparison.OrdinalIgnoreCase)
                || eventTypeName.Contains("Death", StringComparison.OrdinalIgnoreCase)
                || eventTypeName.Contains("Turn", StringComparison.OrdinalIgnoreCase)
                || eventTypeName.Contains("Round", StringComparison.OrdinalIgnoreCase))
                return "combat";

            if (eventTypeName.Contains("Move", StringComparison.OrdinalIgnoreCase)
                || eventTypeName.Contains("Position", StringComparison.OrdinalIgnoreCase)
                || eventTypeName.Contains("Path", StringComparison.OrdinalIgnoreCase))
                return "movement";

            if (eventTypeName.Contains("Loot", StringComparison.OrdinalIgnoreCase)
                || eventTypeName.Contains("Level", StringComparison.OrdinalIgnoreCase)
                || eventTypeName.Contains("Xp", StringComparison.OrdinalIgnoreCase)
                || eventTypeName.Contains("Progress", StringComparison.OrdinalIgnoreCase))
                return "progression";

            return "simulation";
        }

        private static uint ReadUIntField(object payload, params string[] candidates)
        {
            foreach (var name in candidates)
            {
                var field = payload.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                    continue;

                var value = field.GetValue(payload);
                if (value is uint u) return u;
                if (value is int i && i >= 0) return (uint)i;
                if (value is ulong ul && ul <= uint.MaxValue) return (uint)ul;
            }

            return 0;
        }

        private static string ReadStringField(object payload, params string[] candidates)
        {
            foreach (var name in candidates)
            {
                var field = payload.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                    continue;

                var value = field.GetValue(payload);
                if (value == null)
                    continue;

                var text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return string.Empty;
        }

        private static string BuildSummaryMessage(string eventTypeName, object payload)
        {
            var fields = payload.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            var sb = new StringBuilder();
            sb.Append(eventTypeName);

            var appended = 0;
            foreach (var field in fields)
            {
                if (appended >= 4)
                    break;

                var value = field.GetValue(payload);
                if (value == null)
                    continue;

                sb.Append(appended == 0 ? ": " : ", ");
                sb.Append(field.Name);
                sb.Append('=');
                sb.Append(value);
                appended++;
            }

            return sb.ToString();
        }

        private void HandleCombatStarted(CombatStartedEventData evt)
        {
            PostEvent(new WorldSessionEventIngestDto
            {
                eventType = "combat.started",
                category = "combat",
                frame = evt.FrameNumber,
                entityId = evt.CombatSessionId.ToString(),
                message = $"Combat session {evt.CombatSessionId} started."
            });
        }

        private void HandleDamageInflicted(DamageInflictedEventData evt)
        {
            PostEvent(new WorldSessionEventIngestDto
            {
                eventType = "combat.damage",
                category = "combat",
                frame = evt.FrameNumber,
                entityId = evt.VictimEntityId.ToString(),
                message = $"{evt.DamageSource} dealt {evt.DamageDealt} {evt.DamageType} damage."
            });
        }

        private void HandleTurnTransition(TurnTransitionEventData evt)
        {
            PostEvent(new WorldSessionEventIngestDto
            {
                eventType = "combat.turn.transition",
                category = "combat",
                frame = evt.FrameNumber,
                entityId = evt.NextActorId.ToString(),
                message = $"Turn advanced from {evt.PreviousActorId} to {evt.NextActorId}."
            });
        }

        private void HandleRoundTransition(RoundTransitionEventData evt)
        {
            PostEvent(new WorldSessionEventIngestDto
            {
                eventType = "combat.round.transition",
                category = "combat",
                frame = evt.FrameNumber,
                entityId = evt.NextRoundNumber.ToString(),
                message = $"Round {evt.CompletedRoundNumber} completed with {evt.TotalDamageThisRound} damage dealt."
            });
        }

        private void HandleEntityMoved(EntityMovedEventData evt)
        {
            PostEvent(new WorldSessionEventIngestDto
            {
                eventType = "movement.entity.moved",
                category = "movement",
                frame = evt.FrameNumber,
                entityId = evt.SourceEntity.Index.ToString(),
                message = $"Entity moved from ({evt.FromX}, {evt.FromY}) to ({evt.ToX}, {evt.ToY})."
            });
        }

        private void HandleLootGranted(LootGrantedEventData evt)
        {
            PostEvent(new WorldSessionEventIngestDto
            {
                eventType = "progression.loot.granted",
                category = "loot",
                frame = evt.FrameNumber,
                entityId = evt.RecipientEntityId.ToString(),
                message = $"Entity {evt.RecipientEntityId} received {evt.GoldAmount} gold from loot table {evt.LootTableId}."
            });
        }

        private void HandleLevelUp(LevelUpEventData evt)
        {
            PostEvent(new WorldSessionEventIngestDto
            {
                eventType = "progression.level.up",
                category = "progression",
                frame = evt.FrameNumber,
                entityId = evt.EntityId.ToString(),
                message = $"Entity {evt.EntityId} leveled from {evt.PreviousLevel} to {evt.NewLevel}."
            });
        }

        private void HandleDeath(DeathEventData evt)
        {
            PostEvent(new WorldSessionEventIngestDto
            {
                eventType = "combat.death",
                category = "combat",
                frame = evt.FrameNumber,
                entityId = evt.DeceasedEntityId.ToString(),
                message = $"Entity {evt.DeceasedEntityId} died from {evt.CauseOfDeath}."
            });
        }

        private void PostEvent(WorldSessionEventIngestDto payload)
        {
            EventQueuedForPost?.Invoke(payload);

            if (connectionConfig == null)
            {
                if (Time.unscaledTime >= _nextConnectivityLogTime)
                {
                    _nextConnectivityLogTime = Time.unscaledTime + 5f;
                    Debug.LogWarning("BackendObservabilityBridge has no connection config; skipping event post.");
                }
                return;
            }

            StartCoroutine(PostEventCoroutine(payload));
        }

        private IEnumerator PostEventCoroutine(WorldSessionEventIngestDto payload)
        {
            var eventPath = $"/admin/observability/sessions/{SessionId}/events?adminKey={UnityWebRequest.EscapeURL(connectionConfig.AdminApiKey)}";
            var json = BuildEventJson(payload);
            var lastError = string.Empty;
            string finalAttemptedUrl = string.Empty;
            var anyAttempted = false;

            foreach (var baseUrl in BuildCandidateBaseUrls())
            {
                anyAttempted = true;
                var url = $"{baseUrl}{eventPath}";
                finalAttemptedUrl = url;
                using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    var bodyRaw = Encoding.UTF8.GetBytes(json);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = connectionConfig.RequestTimeoutSeconds;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                        yield break;

                    lastError = request.error;
                }
            }

            if (anyAttempted && Time.unscaledTime >= _nextConnectivityLogTime)
            {
                _nextConnectivityLogTime = Time.unscaledTime + 5f;
                Debug.LogWarning(
                    $"BackendObservabilityBridge failed to post event to all candidates. " +
                    $"Last URL: {finalAttemptedUrl}; error: {lastError}. " +
                    $"Check authoritative/admin-ui containers and BackendConnectionConfig URLs.");
            }
        }

        private static string BuildEventJson(WorldSessionEventIngestDto payload)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            AppendJsonPair(sb, "eventType", payload.eventType, true);
            AppendJsonPair(sb, "category", payload.category, true);
            AppendJsonPair(sb, "frame", payload.frame.ToString(), false, true);
            AppendJsonPair(sb, "entityId", payload.entityId, true);
            AppendJsonPair(sb, "message", payload.message, true);

            sb.Append("\"data\":{");
            if (payload.data != null)
            {
                var first = true;
                foreach (var kvp in payload.data)
                {
                    if (!first)
                        sb.Append(',');

                    sb.Append('"').Append(EscapeJson(kvp.Key)).Append("\":\"").Append(EscapeJson(kvp.Value ?? string.Empty)).Append('"');
                    first = false;
                }
            }
            sb.Append("}}");
            return sb.ToString();
        }

        private static void AppendJsonPair(StringBuilder sb, string key, string value, bool quoteValue, bool isNumeric = false)
        {
            sb.Append('"').Append(EscapeJson(key)).Append("\":");
            if (quoteValue && !isNumeric)
            {
                sb.Append('"').Append(EscapeJson(value ?? string.Empty)).Append('"');
            }
            else
            {
                sb.Append(value ?? "0");
            }
            sb.Append(',');
        }

        private static string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private IEnumerable<string> BuildCandidateBaseUrls()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<string>();

            static string Normalize(string url) => url.Trim().TrimEnd('/');

            void AddIfPresent(string url)
            {
                if (string.IsNullOrWhiteSpace(url))
                    return;

                var normalized = Normalize(url);
                if (!seen.Add(normalized))
                    return;

                ordered.Add(normalized);
            }

            AddIfPresent("http://127.0.0.1:8081");
            AddIfPresent("http://localhost:8081");
            AddIfPresent("http://127.0.0.1:8083");
            AddIfPresent("http://localhost:8083");
            AddIfPresent(connectionConfig.AuthoritativeBaseUrl);
            AddIfPresent(connectionConfig.AdminUiBaseUrl);

            foreach (var url in ordered)
                yield return url;
        }
    }

    [Serializable]
    public sealed class WorldSessionEventIngestDto
    {
        public string eventType;
        public string category;
        public uint frame;
        public string entityId;
        public string message;
        public Dictionary<string, string> data;
    }
}
