#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;

namespace Authoritative.Services
{
    /// <summary>
    /// Wire envelope for near-real-time world/action streaming from the
    /// authoritative backend to connected clients (Three.js visualizer and
    /// game clients). ScyllaDB is the durable replay source; these messages are
    /// the live emission which is also buffered in Redis so nothing is missed.
    /// </summary>
    public sealed class WorldStreamMessage
    {
        /// <summary>Event kind: "action.*", "system.*", "world.persisted", "world.build", etc.</summary>
        public string Type { get; set; } = "system.event";

        public string SessionId { get; set; } = "global";

        public uint Frame { get; set; }

        public string EntityId { get; set; } = string.Empty;

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        public Dictionary<string, string>? Data { get; set; }

        public static WorldStreamMessage FromWorldSessionEvent(WorldSessionEvent evt)
        {
            return new WorldStreamMessage
            {
                Type = evt.EventType,
                SessionId = evt.SessionId,
                Frame = evt.Frame,
                EntityId = evt.EntityId,
                TimestampUtc = evt.TimestampUtc,
                Data = new Dictionary<string, string>(evt.Data, StringComparer.Ordinal)
                {
                    ["category"] = evt.Category,
                    ["message"] = evt.Message,
                    ["eventId"] = evt.EventId
                }
            };
        }
    }
}
#endif
