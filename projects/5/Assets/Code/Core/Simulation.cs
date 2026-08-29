using System;
using System.Collections.Generic;
using DunGen.ECS.Components;
using DunGen.Events;
using DunGen.Simulation.RNG;
using Unity.Entities;

namespace DunGen.Core
{
    /// <summary>
    /// Core simulation loop: fixed timestep, deterministic RNG, event system.
    /// This is the heart of the deterministic server simulation.
    /// </summary>
    public class Simulation
    {
        private World _world;
        private EntityManager _entityManager;
        private DeterministicRNG _rng;
        private EventLog _eventLog;
        private EventBus _eventBus;
        
        private const float FIXED_TIMESTEP = 1f / 60f; // 60 Hz
        private float _accumulatedTime = 0f;
        private uint _frameNumber = 0;
        private bool _isRunning = false;
        private ulong _currentSeed = 0;

        public Simulation()
        {
            _eventLog = new EventLog();
            _eventBus = EventBus.Instance;
            
            // Create/get ECS world
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
                _world = new World("DunGenSimulation");
            
            _entityManager = _world.EntityManager;
        }

        /// <summary>Initialize simulation with seed.</summary>
        public void Initialize(ulong seed)
        {
            _currentSeed = seed;
            _rng = new DeterministicRNG(seed);
            _eventLog.Initialize(seed);
            _frameNumber = 0;
            _accumulatedTime = 0f;

            // Publish initialization event (pure data struct)
            var initEvent = new SimulationInitializedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                Seed = seed,
                MaxEntities = 10000,
                Timestamp = 0f,
                FrameNumber = 0
            };
            _eventBus.Publish(initEvent);
            _eventLog.RecordEvent(initEvent);

            _isRunning = true;
        }

        /// <summary>
        /// Perform one fixed-timestep simulation frame.
        /// Should be called from a fixed update loop.
        /// </summary>
        public void SimulationStep(float deltaTime)
        {
            if (!_isRunning)
                return;

            _accumulatedTime += deltaTime;

            while (_accumulatedTime >= FIXED_TIMESTEP)
            {
                _accumulatedTime -= FIXED_TIMESTEP;
                
                // Execute one fixed frame
                ExecuteFrame();
                _frameNumber++;
                _eventLog.AdvanceFrame();
            }
        }

        /// <summary>Execute a single simulation frame.</summary>
        private void ExecuteFrame()
        {
            // Run all ECS systems
            _world.Update();
        }

        /// <summary>Create a new entity with basic components.</summary>
        public Entity CreateEntity(string name, Vector3 position)
        {
            var entity = _entityManager.CreateEntity(typeof(Position), typeof(Name));

            _entityManager.SetComponentData(entity, new Position
            {
                X = position.X,
                Y = position.Y,
                Z = position.Z
            });

            Name.Values = name;

            var createdEvent = new EntityCreatedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = _frameNumber,
                Timestamp = _frameNumber * FIXED_TIMESTEP,
                SourceEntity = entity,
                EntityType = "Generic",
                Name = name
            };

            _eventBus.Publish(createdEvent);
            _eventLog.RecordEvent(createdEvent);

            return entity;
        }

        /// <summary>Destroy an entity.</summary>
        public void DestroyEntity(Entity entity)
        {
            if (!_entityManager.Exists(entity))
                return;

            var destroyedEvent = new EntityDestroyedEventData
            {
                EventId = _eventBus.GetNextEventId(),
                FrameNumber = _frameNumber,
                Timestamp = _frameNumber * FIXED_TIMESTEP,
                SourceEntity = entity,
                EntityType = "Generic",
                Reason = "ExplicitDestroy"
            };

            _eventBus.Publish(destroyedEvent);
            _eventLog.RecordEvent(destroyedEvent);
            _entityManager.DestroyEntity(entity);
        }

        /// <summary>Get the deterministic RNG for this simulation.</summary>
        public DeterministicRNG GetRNG() => _rng;

        /// <summary>Get the event log.</summary>
        public EventLog GetEventLog() => _eventLog;

        /// <summary>Get current frame number.</summary>
        public uint GetFrameNumber() => _frameNumber;

        /// <summary>Get current seed.</summary>
        public ulong GetSeed() => _currentSeed;

        /// <summary>Check if simulation is running.</summary>
        public bool IsRunning => _isRunning;

        /// <summary>Stop the simulation.</summary>
        public void Stop()
        {
            _isRunning = false;
        }

        /// <summary>Get the ECS world.</summary>
        public World GetWorld() => _world;

        /// <summary>Get the entity manager.</summary>
        public EntityManager GetEntityManager() => _entityManager;

        /// <summary>Export current simulation state as JSON log.</summary>
        public string ExportLog() => _eventLog.ExportToJson();
    }

    // Lightweight local vector type used by simulation-facing APIs.
    public struct Vector3
    {
        public float X, Y, Z;
        
        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
