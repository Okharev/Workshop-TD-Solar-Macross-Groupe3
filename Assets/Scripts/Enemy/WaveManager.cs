using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathing; 

namespace Pathing.Gameplay
{
    public class WaveManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Reference to your generated road network.")]
        public RoadNetworkGenerator roadGenerator;

        [Header("Configuration")]
        public List<WaveProfile> waves = new List<WaveProfile>();
        

        // --- Events for UI (Decoupled) ---
        public event Action<int, string> OnWaveStarted;      // (WaveIndex, WaveName)
        public event Action OnWaveFinished;                  // Called when spawning finishes
        public event Action OnAllWavesCompleted;             // Called when no waves remain

        // Runtime State
        private int _currentWaveIndex = -1;
        private Dictionary<string, EnemySpawner> _spawnerMap = new Dictionary<string, EnemySpawner>();
        private bool _isWaveActive = false;

        public bool IsWaveActive => _isWaveActive;
        public int CurrentWaveIndex => _currentWaveIndex;
        public int TotalWaves => waves.Count;

        private void Start()
        {
            // Index all spawners in the scene for quick lookup by ID
            var spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
            foreach (var spawner in spawners)
            {
                if (!string.IsNullOrEmpty(spawner.spawnerID))
                {
                    if (!_spawnerMap.ContainsKey(spawner.spawnerID))
                        _spawnerMap.Add(spawner.spawnerID, spawner);
                    else
                        Debug.LogWarning($"[WaveManager] Duplicate Spawner ID found: {spawner.spawnerID}");
                }
            }
        }

        /// <summary>
        /// Call this via your UI Button, Event Trigger, or Context Menu
        /// </summary>
        [ContextMenu("Start Next Wave")]
        public void StartNextWave()
        {
            if (_isWaveActive) 
            {
                Debug.LogWarning("Cannot start next wave; a wave is currently active.");
                return;
            }
            
            _currentWaveIndex++;
            if (_currentWaveIndex < waves.Count)
            {
                StartCoroutine(RunWaveRoutine(waves[_currentWaveIndex]));
            }
            else
            {
                Debug.Log("All waves complete!");
                OnAllWavesCompleted?.Invoke();
            }
        }

        private IEnumerator RunWaveRoutine(WaveProfile wave)
        {
            _isWaveActive = true;
            OnWaveStarted?.Invoke(_currentWaveIndex + 1, wave.waveName);
            Debug.Log($"Starting Wave {_currentWaveIndex + 1}: {wave.waveName}");

            // 1. Configure Roads (Block/Unblock based on profile)
            ConfigureRoadsForWave(wave);

            // 2. Re-bake NavMesh (Wait one frame for active states to update)
            yield return null; 


            // 3. Execute Spawn Segments
            foreach (var segment in wave.segments)
            {
                // Segment Initial Delay
                if (segment.initialDelay > 0)
                    yield return new WaitForSeconds(segment.initialDelay);

                if (_spawnerMap.TryGetValue(segment.spawnerID, out var targetSpawner))
                {
                    for (int i = 0; i < segment.count; i++)
                    {
                        targetSpawner.Spawn(segment.enemyPrefab);
                        
                        if (segment.spawnInterval > 0)
                            yield return new WaitForSeconds(segment.spawnInterval);
                    }
                }
                else
                {
                    Debug.LogError($"[WaveManager] Spawner ID '{segment.spawnerID}' not found in scene!");
                }
            }

            _isWaveActive = false;
            OnWaveFinished?.Invoke();

        }

        private void ConfigureRoadsForWave(WaveProfile wave)
        {
            if (!roadGenerator || !roadGenerator.splineContainer) return;

            int totalSplines = roadGenerator.splineContainer.Splines.Count;

            for (int i = 0; i < totalSplines; i++)
            {
                // If the index is in the unlocked list, IsBlocked = false.
                bool isUnlocked = wave.unlockedRoadIndices.Contains(i);
                roadGenerator.SetRoadBlocked(i, !isUnlocked);
            }
        }
    }

    // --- Data Classes ---

    [Serializable]
    public class WaveProfile
    {
        public string waveName = "Wave 1";
        
        [Header("Map State")]
        [Tooltip("The Spline Indices that will be OPEN for this wave. All others will be blocked.")]
        public List<int> unlockedRoadIndices;

        [Header("Spawning")]
        public List<WaveSegment> segments;
    }

    [Serializable]
    public class WaveSegment
    {
        [Tooltip("Must match the ID on an EnemySpawner component in the scene.")]
        public string spawnerID;
        public GameObject enemyPrefab;
        
        [Min(1)] public int count = 5;
        [Min(0)] public float spawnInterval = 1.0f;
        
        [Tooltip("Wait time before this segment starts spawning.")]
        public float initialDelay = 0.0f;
    }
}