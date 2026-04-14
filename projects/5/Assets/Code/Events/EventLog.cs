using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace DunGen.Events
{
    /// <summary>
    /// Records all game events for replay and debugging.
    /// Data-oriented: works with pure event data structs.
    /// Ensures deterministic replayability: same seed + action log = same final state.
    /// </summary>
    public class EventLog
    {
        private readonly List<LoggedAction> _actions = new();
        private readonly List<(object @event, string typeName, uint frameNumber)> _events = new();
        private ulong _seed;
        private uint _currentFrame;

        [Serializable]
        public struct LoggedAction
        {
            public uint FrameNumber;
            public string ActionType;
            public string Params; // JSON string of action parameters
            public ulong RNGStateBefore;
            public ulong RNGStateAfter;
        }

        /// <summary>Initialize log with starting seed.</summary>
        public void Initialize(ulong seed)
        {
            _seed = seed;
            _actions.Clear();
            _events.Clear();
            _currentFrame = 0;
        }

        /// <summary>Log an action that was taken.</summary>
        public void LogAction(string actionType, string actionParams, ulong rngStateBefore, ulong rngStateAfter)
        {
            var action = new LoggedAction
            {
                FrameNumber = _currentFrame,
                ActionType = actionType,
                Params = actionParams,
                RNGStateBefore = rngStateBefore,
                RNGStateAfter = rngStateAfter
            };
            _actions.Add(action);
        }

        /// <summary>Record a data-oriented event (struct).</summary>
        public void RecordEvent<T>(T @event) where T : struct
        {
            string typeName = typeof(T).Name;
            // Remove "EventData" suffix for cleaner type names
            if (typeName.EndsWith("EventData"))
                typeName = typeName.Substring(0, typeName.Length - 9); // Remove "EventData"
            
            _events.Add((@event, typeName, _currentFrame));
        }

        /// <summary>Advance frame counter.</summary>
        public void AdvanceFrame()
        {
            _currentFrame++;
        }

        /// <summary>Get all logged actions.</summary>
        public IReadOnlyList<LoggedAction> GetActions() => _actions.AsReadOnly();

        /// <summary>Get all recorded events as objects (for compatibility).</summary>
        public IReadOnlyList<object> GetEvents() => _events.Select(e => e.@event).ToList();

        /// <summary>Get initial seed.</summary>
        public ulong GetSeed() => _seed;

        /// <summary>Get current frame number.</summary>
        public uint GetCurrentFrame() => _currentFrame;

        /// <summary>Export log as JSON-compatible string.</summary>
        public string ExportToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"seed\": {_seed},");
            sb.AppendLine($"  \"totalFrames\": {_currentFrame},");
            sb.AppendLine("  \"actions\": [");

            for (int i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];
                sb.AppendLine($"    {{");
                sb.AppendLine($"      \"frame\": {action.FrameNumber},");
                sb.AppendLine($"      \"type\": \"{action.ActionType}\",");
                sb.AppendLine($"      \"params\": {action.Params},");
                sb.AppendLine($"      \"rngBefore\": {action.RNGStateBefore},");
                sb.AppendLine($"      \"rngAfter\": {action.RNGStateAfter}");
                sb.AppendLine($"    }}{(i < _actions.Count - 1 ? "," : "")}");
            }

            sb.AppendLine("  ],");
            sb.AppendLine("  \"events\": [");

            for (int i = 0; i < _events.Count; i++)
            {
                var (evt, typeName, frameNumber) = _events[i];
                sb.AppendLine($"    {{");
                sb.AppendLine($"      \"id\": {i},");
                sb.AppendLine($"      \"frame\": {frameNumber},");
                sb.AppendLine($"      \"type\": \"{typeName}\",");
                sb.AppendLine($"      \"data\": {SerializeEventToJson(evt)}");
                sb.AppendLine($"    }}{(i < _events.Count - 1 ? "," : "")}");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Serialize a struct event to JSON by reflecting over its fields.
        /// Data-oriented: no GetEventTypeName() or ToJsonString() methods.
        /// </summary>
        private string SerializeEventToJson(object evt)
        {
            if (evt == null)
                return "null";

            var type = evt.GetType();
            var sb = new StringBuilder();
            sb.Append("{");

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var value = field.GetValue(evt);
                
                sb.Append($"\"{field.Name}\":");
                
                if (value == null)
                    sb.Append("null");
                else if (value is string str)
                    sb.Append($"\"{str}\"");
                else if (value is bool b)
                    sb.Append(b ? "true" : "false");
                else if (value is int[] intArr || value is float[] floatArr)
                    sb.Append(SerializeArray(value));
                else
                    sb.Append(value);
                
                if (i < fields.Length - 1)
                    sb.Append(",");
            }

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>Serialize arrays to JSON.</summary>
        private string SerializeArray(object arr)
        {
            if (arr is int[] intArr)
                return $"[{string.Join(",", intArr)}]";
            if (arr is float[] floatArr)
                return $"[{string.Join(",", floatArr)}]";
            if (arr is string[] strArr)
                return $"[{string.Join(",", strArr.Select(s => $"\"{s}\""))}]";
            return "[]";
        }

        /// <summary>Clear all logs.</summary>
        public void Clear()
        {
            _actions.Clear();
            _events.Clear();
            _currentFrame = 0;
        }
    }
}
