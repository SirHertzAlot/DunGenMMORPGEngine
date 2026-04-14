using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DunGen.Events
{
    /// <summary>
    /// Records all game events for replay and debugging.
    /// Ensures deterministic replayability: same seed + action log = same final state.
    /// </summary>
    public class EventLog
    {
        private readonly List<LoggedAction> _actions = new();
        private readonly List<GameEvent> _events = new();
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

        /// <summary>Record an event that occurred.</summary>
        public void RecordEvent(GameEvent @event)
        {
            @event.FrameNumber = _currentFrame;
            @event.Timestamp = _currentFrame / 60f; // Assuming 60 Hz
            _events.Add(@event);
        }

        /// <summary>Advance frame counter.</summary>
        public void AdvanceFrame()
        {
            _currentFrame++;
        }

        /// <summary>Get all logged actions.</summary>
        public IReadOnlyList<LoggedAction> GetActions() => _actions.AsReadOnly();

        /// <summary>Get all recorded events.</summary>
        public IReadOnlyList<GameEvent> GetEvents() => _events.AsReadOnly();

        /// <summary>Get initial seed.</summary>
        public ulong GetSeed() => _seed;

        /// <summary>Get current frame number.</summary>
        public uint GetCurrentFrame() => _currentFrame;

        /// <summary>Export log as JSON-compatible string (simple format for MVP).</summary>
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
                var @event = _events[i];
                sb.AppendLine($"    {{\n      \"id\": {i},");
                sb.AppendLine($"      \"frame\": {@event.FrameNumber},");
                sb.AppendLine($"      \"type\": \"{@event.GetEventTypeName()}\",");
                sb.AppendLine($"      \"data\": {@event.ToJsonString()}");
                sb.AppendLine($"    }}{(i < _events.Count - 1 ? "," : "")}");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
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
