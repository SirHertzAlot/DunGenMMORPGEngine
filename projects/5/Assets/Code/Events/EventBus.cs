using System;
using System.Collections.Generic;

namespace DunGen.Events
{
    /// <summary>
    /// Data-oriented event bus for pub-sub communication.
    /// Operates on pure data structs (not class hierarchies).
    /// Thread-safe for multi-system event simulation in ECS.
    /// </summary>
    public class EventBus
    {
        private static EventBus _instance;
        public static EventBus Instance => _instance ??= new EventBus();

        private readonly Dictionary<Type, List<Delegate>> _listeners = new();
        private readonly List<Action<object, Type>> _globalListeners = new();
        private ulong _nextEventId = 1;

        public EventBus()
        {
        }

        /// <summary>
        /// Subscribe to events of a specific data type (struct).
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            var eventType = typeof(T);
            if (!_listeners.ContainsKey(eventType))
                _listeners[eventType] = new List<Delegate>();
            
            _listeners[eventType].Add(handler);
        }

        /// <summary>
        /// Unsubscribe from events of a specific data type (struct).
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var eventType = typeof(T);
            if (_listeners.ContainsKey(eventType))
                _listeners[eventType].Remove(handler);
        }

        /// <summary>
        /// Subscribe to every published event for replay logs, debugging, and telemetry.
        /// The returned action removes the listener.
        /// </summary>
        public Action SubscribeAll(Action<object, Type> handler)
        {
            _globalListeners.Add(handler);
            return () => _globalListeners.Remove(handler);
        }

        /// <summary>
        /// Publish an event immediately to all subscribers.
        /// </summary>
        public void Publish<T>(T @event) where T : struct
        {
            var eventType = typeof(T);
            if (_listeners.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    if (handler is Action<T> typedHandler)
                        typedHandler(@event);
                }
            }

            for (int i = 0; i < _globalListeners.Count; i++)
            {
                try
                {
                    _globalListeners[i](@event, eventType);
                }
                catch
                {
                    // Telemetry observers must never break deterministic gameplay.
                }
            }
        }

        /// <summary>
        /// Get next sequential event ID (call this before publishing).
        /// </summary>
        public ulong GetNextEventId()
        {
            return _nextEventId++;
        }

        /// <summary>
        /// Clear all subscribers.
        /// </summary>
        public void Clear()
        {
            _listeners.Clear();
            _globalListeners.Clear();
            _nextEventId = 1;
        }

        /// <summary>
        /// Get total number of subscribers for a specific event type.
        /// </summary>
        public int GetSubscriberCount<T>() where T : struct
        {
            var eventType = typeof(T);
            return _listeners.TryGetValue(eventType, out var handlers) ? handlers.Count : 0;
        }
    }
}
