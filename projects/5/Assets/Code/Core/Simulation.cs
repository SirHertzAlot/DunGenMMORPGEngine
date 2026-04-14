using System;
using System.Collections.Generic;
using DunGen.Events;
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

            // Publish initialization event
            var initEvent = new SimulationInitializedEvent
            {
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
            var entity = _entityManager.CreateEntity();
            
            // Add basic components (we'll define these next)
            // _entityManager.AddComponentData(entity, new Position { Value = position });
            // _entityManager.AddComponentData(entity, new Name { Value = name });
            
            return entity;
        }

        /// <summary>Destroy an entity.</summary>
        public void DestroyEntity(Entity entity)
        {
            if (_entityManager.Exists(entity))
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

    // Placeholder vector for early prototyping (will use Unity.Mathematics later)
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
