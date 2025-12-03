using System;
using System.Collections;
using System.Collections.Generic;
using Enemy;
using UnityEngine;
using Pathing; 

namespace Pathing.Gameplay
{
    public class WaveManager : MonoBehaviour
    {
        [Header("Dependencies")]
        public RoadNetworkGenerator roadGenerator;

        [Header("Configuration")]
        public List<WaveProfile> waves = new List<WaveProfile>();

        // --- Events ---
        public event Action<int, string> OnWaveStarted;
        public event Action OnWaveFinished;
        public event Action OnAllWavesCompleted;

        // Runtime State
        private int _currentWaveIndex = -1;
        private bool _isWaveActive = false;

        public bool IsWaveActive => _isWaveActive;
        public int CurrentWaveIndex => _currentWaveIndex;

        // We no longer need Start() to build Dictionaries because we use direct references!

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

            // 1. Configure Roads (Open/Close gates)
            ConfigureRoadsForWave(wave);
            yield return null; // Wait 1 frame for physics/navmesh updates if needed

            // 2. Prepare Parallel Execution
            // We want Ground and Air to start at the same time, but finish only when BOTH are done.
            List<Coroutine> activeSpawns = new List<Coroutine>();

            // --- Launch Ground Units ---
            foreach (var segment in wave.groundSegments)
            {
                if (segment.targetSpawner != null)
                {
                    Coroutine c = StartCoroutine(SpawnGroundSegment(segment));
                    activeSpawns.Add(c);
                }
                else
                {
                    Debug.LogError($"[WaveManager] Wave '{wave.waveName}' has a Ground Segment with no Spawner assigned!");
                }
            }

            // --- Launch Air Units ---
            foreach (var segment in wave.airSegments)
            {
                if (segment.targetPath != null)
                {
                    Coroutine c = StartCoroutine(SpawnAirSegment(segment));
                    activeSpawns.Add(c);
                }
                else
                {
                    Debug.LogError($"[WaveManager] Wave '{wave.waveName}' has an Air Segment with no Path assigned!");
                }
            }

            // 3. Wait for ALL spawners to finish
            foreach (var c in activeSpawns)
            {
                yield return c;
            }

            _isWaveActive = false;
            OnWaveFinished?.Invoke();
        }

        // --- Ground Logic ---
        private IEnumerator SpawnGroundSegment(GroundWaveSegment segment)
        {
            if (segment.initialDelay > 0) yield return new WaitForSeconds(segment.initialDelay);

            for (int i = 0; i < segment.count; i++)
            {
                // Direct reference call
                segment.targetSpawner.Spawn(segment.enemyPrefab);
                
                if (segment.spawnInterval > 0) yield return new WaitForSeconds(segment.spawnInterval);
            }
        }

        // --- Air Logic ---
// Dans WaveManager.cs

        private IEnumerator SpawnAirSegment(AirWaveSegment segment)
        {
            if (segment.initialDelay > 0) yield return new WaitForSeconds(segment.initialDelay);

            // Plus de calculs complexes ici, on délègue tout à l'AirPath
            AirPath path = segment.targetPath;

            if (path == null)
            {
                Debug.LogError($"[WaveManager] Le segment aérien n'a pas de 'targetPath' assigné !");
                yield break;
            }

            for (int i = 0; i < segment.count; i++)
            {
                // Une seule ligne propre :
                path.Spawn(segment.enemyPrefab); //

                if (segment.spawnInterval > 0) yield return new WaitForSeconds(segment.spawnInterval);
            }
        }

        private void ConfigureRoadsForWave(WaveProfile wave)
        {
            if (!roadGenerator || !roadGenerator.splineContainer) return;

            int totalSplines = roadGenerator.splineContainer.Splines.Count;

            for (int i = 0; i < totalSplines; i++)
            {
                // If the index is in the list, UNBLOCK it. Otherwise, BLOCK it.
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
        [Tooltip("Indices of roads that should be OPEN during this wave.")]
        public List<int> unlockedRoadIndices;

        [Header("Ground Units")]
        public List<GroundWaveSegment> groundSegments;

        [Header("Air Units")]
        public List<AirWaveSegment> airSegments;
    }

    // Base class for shared settings
    [Serializable]
    public abstract class BaseWaveSegment
    {
        public GameObject enemyPrefab;
        [Min(1)] public int count = 5;
        [Min(0)] public float spawnInterval = 1.0f;
        [Tooltip("Seconds to wait before starting this specific group.")]
        public float initialDelay = 0.0f;
    }

    [Serializable]
    public class GroundWaveSegment : BaseWaveSegment
    {
        [Tooltip("Drag the GameObject with the EnemySpawner component here.")]
        public EnemySpawner targetSpawner;
    }

    [Serializable]
    public class AirWaveSegment : BaseWaveSegment
    {
        [Tooltip("Drag the GameObject with the AirPath component here.")]
        public AirPath targetPath;
    }
}