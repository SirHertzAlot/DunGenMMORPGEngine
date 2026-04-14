using DunGen.Core;
using DunGen.Events;
using UnityEngine;

namespace DunGen.Startup
{
    /// <summary>
    /// MonoBehaviour that initializes the simulation on play.
    /// Attach to a GameObject in the scene and press Play to start.
    /// </summary>
    public class SimulationStarter : MonoBehaviour
    {
        private Simulation _simulation;
        public ulong SimulationSeed = 42;

        private void Start()
        {
            _simulation = new Simulation();
            _simulation.Initialize(SimulationSeed);
            
            Debug.Log($"✓ Simulation initialized with seed: {SimulationSeed}");
            Debug.Log($"✓ Event Bus ready");
            Debug.Log($"✓ Event Log started");
        }

        private void Update()
        {
            if (_simulation != null && _simulation.IsRunning)
            {
                _simulation.SimulationStep(Time.deltaTime);
            }
        }

        private void OnGUI()
        {
            if (_simulation == null)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Simulation Running: {_simulation.IsRunning}", GUI.skin.box);
            GUILayout.Label($"Frame: {_simulation.GetFrameNumber()}", GUI.skin.box);
            GUILayout.Label($"Seed: {_simulation.GetSeed()}", GUI.skin.box);
            GUILayout.Label($"Events: {_simulation.GetEventLog().GetEvents().Count}", GUI.skin.box);
            
            if (GUILayout.Button("Export Log"))
            {
                string json = _simulation.ExportLog();
                Debug.Log(json);
            }
            
            if (GUILayout.Button("Stop"))
            {
                _simulation.Stop();
            }

            GUILayout.EndArea();
        }

        /// <summary>Get the active simulation (for testing/debugging).</summary>
        public Simulation GetSimulation() => _simulation;
    }
}
