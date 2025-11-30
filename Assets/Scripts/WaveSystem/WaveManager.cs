using System.Collections.Generic;
using System.Linq;
using Pathing;
using UnityEngine;

namespace WaveSystem
{
    public class WaveManager : MonoBehaviour
    {
        [Header("References")] public RoadNetworkGenerator roadGenerator;

        public List<EnemySpawner> spawners = new();

        [Header("Configuration")] public List<WaveConfig> waves;

        public bool autoStart;

        private int _currentWaveIndex = -1;

        private void Start()
        {
            // Auto-find spawners if empty
            if (spawners.Count == 0) spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None).ToList();

            if (autoStart) StartNextWave();
        }

        [ContextMenu("Start Next Wave")]
        public void StartNextWave()
        {
            _currentWaveIndex++;
            if (_currentWaveIndex >= waves.Count)
            {
                Debug.Log("All waves complete!");
                return;
            }

            RunWave(waves[_currentWaveIndex]);
        }

        private void RunWave(WaveConfig config)
        {
            Debug.Log($"Starting Wave {_currentWaveIndex + 1}");

            // roadGenerator.ResetBlockages();

            // 1. Unblock Roads
            // foreach (var roadIndex in config.roadsToUnblock) roadGenerator.UnlockRoad(roadIndex);

            // 2. Trigger Spawners
            foreach (var instruction in config.spawnerInstructions)
            {
                // Find the matching spawner
                var spawner = spawners.FirstOrDefault(s => s.spawnerID == instruction.spawnerID);
                if (spawner != null)
                    spawner.ExecuteSegments(instruction.segments);
                else
                    Debug.LogWarning($"Wave Config references Spawner '{instruction.spawnerID}' but it was not found.");
            }

            // Emit events for UI here later
        }
    }
}