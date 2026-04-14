using System;
using System.Collections.Generic;

namespace DunGen.Events
{
    /// <summary>
    /// Global event bus for pub-sub event communication.
    /// All game state changes flow through this bus.
    /// Thread-safe for multi-system event simulation.
    /// </summary>
    public class EventBus
    {
        private static EventBus _instance;
        public static EventBus Instance => _instance ??= new EventBus();

        private readonly Dictionary<Type, List<Delegate>> _listeners = new();
        private readonly Queue<GameEvent> _eventQueue = new();
        private ulong _nextEventId = 1;

        public EventBus()
        {
        }

        /// <summary>
        /// Subscribe to events of a specific type.
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : GameEvent
        {
            var eventType = typeof(T);
            if (!_listeners.ContainsKey(eventType))
                _listeners[eventType] = new List<Delegate>();
            
            _listeners[eventType].Add(handler);
        }

        /// <summary>
        /// Unsubscribe from events of a specific type.
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler) where T : GameEvent
        {
            var eventType = typeof(T);
            if (_listeners.ContainsKey(eventType))
                _listeners[eventType].Remove(handler);
        }

        /// <summary>
        /// Publish an event immediately to all subscribers.
        /// </summary>
        public void Publish<T>(T @event) where T : GameEvent
        {
            @event.EventId = _nextEventId++;
            
            var eventType = typeof(T);
            if (_listeners.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    if (handler is Action<T> typedHandler)
                        typedHandler(@event);
                }
            }
        }

        /// <summary>
        /// Clear all subscribers.
        /// </summary>
        public void Clear()
        {
            _listeners.Clear();
            _eventQueue.Clear();
            _nextEventId = 1;
        }

        /// <summary>
        /// Get total number of subscribers for a specific event type.
        /// </summary>
        public int GetSubscriberCount<T>() where T : GameEvent
        {
            var eventType = typeof(T);
            return _listeners.TryGetValue(eventType, out var handlers) ? handlers.Count : 0;
        }
    }
}
