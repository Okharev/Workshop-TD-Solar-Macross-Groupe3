using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pathing;
using Pathing.Gameplay;
using Placement;
using UnityEngine;

namespace Enemy
{
    public class WaveManager : MonoBehaviour
    {
        [Header("Dependencies")] public RoadNetworkGenerator roadGenerator;

        [Header("Configuration")] public List<WaveProfile> waves = new();

        public float timeBetweenWaves = 30f;


        public ReactiveInt enemiesRemaining = new(0);
        public ReactiveInt totalEnemiesInWave = new(0);
        public ReactiveFloat timeToNextWave = new(0);
        private Coroutine _countdownCoroutine;


        public bool IsWaveActive { get; private set; }

        public int CurrentWaveIndex { get; private set; } = -1;

        public event Action<int, string> OnWaveStarted;
        public event Action OnWaveFinished;
        public event Action OnAllWavesCompleted;

        [ContextMenu("Start Next Wave")]
        public void StartNextWave()
        {
            if (IsWaveActive)
            {
                Debug.LogWarning("Cannot start next wave; a wave is currently active.");
                return;
            }

            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            CurrentWaveIndex++;
            if (CurrentWaveIndex < waves.Count)
            {
                StartCoroutine(RunWaveRoutine(waves[CurrentWaveIndex]));
            }
            else
            {
                Debug.Log("All waves complete!");
                OnAllWavesCompleted?.Invoke();
            }
        }

        private void OnWaveDefeated()
        {
            IsWaveActive = false;
            OnWaveFinished?.Invoke();

            if (CurrentWaveIndex < waves.Count - 1)
                _countdownCoroutine = StartCoroutine(WaveCountdownRoutine());
            else
                OnAllWavesCompleted?.Invoke();
        }

        private IEnumerator WaveCountdownRoutine()
        {
            var timer = timeBetweenWaves;
            timeToNextWave.Value = timer;

            while (timer > 0)
            {
                yield return null;
                timer -= Time.deltaTime;

                timeToNextWave.Value = Mathf.Max(0, timer);
            }

            StartNextWave();
        }

        private IEnumerator RunWaveRoutine(WaveProfile wave)
        {
            IsWaveActive = true;
            OnWaveStarted?.Invoke(CurrentWaveIndex + 1, wave.waveName);
            Debug.Log($"Starting Wave {CurrentWaveIndex + 1}: {wave.waveName}");

            ConfigureRoadsForWave(wave);

            var totalGround = wave.groundSegments.Sum(s => s.count);
            var totalAir = wave.airSegments.Sum(s => s.count);

            totalEnemiesInWave.Value = totalGround + totalAir;
            enemiesRemaining.Value = totalGround + totalAir;

            yield return null;

            var activeSpawns = new List<Coroutine>();

            foreach (var segment in wave.groundSegments)
                if (segment.targetSpawner)
                    activeSpawns.Add(StartCoroutine(SpawnGroundSegment(segment)));

            foreach (var segment in wave.airSegments)
                if (segment.targetPath)
                    activeSpawns.Add(StartCoroutine(SpawnAirSegment(segment)));

            foreach (var c in activeSpawns) yield return c;

        }

        private IEnumerator SpawnGroundSegment(GroundWaveSegment segment)
        {
            if (segment.initialDelay > 0) yield return new WaitForSeconds(segment.initialDelay);
            for (var i = 0; i < segment.count; i++)
            {
                segment.targetSpawner.Spawn(segment.enemyPrefab, segment.specificTarget);
                if (segment.spawnInterval > 0) yield return new WaitForSeconds(segment.spawnInterval);
            }
        }

        private IEnumerator SpawnAirSegment(AirWaveSegment segment)
        {
            if (segment.initialDelay > 0) yield return new WaitForSeconds(segment.initialDelay);
            if (!segment.targetPath) yield break;
            for (var i = 0; i < segment.count; i++)
            {
                segment.targetPath.Spawn(segment.enemyPrefab, segment.specificTarget);
                if (segment.spawnInterval > 0) yield return new WaitForSeconds(segment.spawnInterval);
            }
        }

        private void ConfigureRoadsForWave(WaveProfile wave)
        {
            if (!roadGenerator || !roadGenerator.splineContainer) return;
            var totalSplines = roadGenerator.splineContainer.Splines.Count;
            for (var i = 0; i < totalSplines; i++)
            {
                var isUnlocked = wave.unlockedRoadIndices.Contains(i);
                roadGenerator.SetRoadBlocked(i, !isUnlocked);
            }
        }
    }


    [Serializable]
    public class WaveProfile
    {
        public string waveName = "Wave 1";

        [Header("Map State")] [Tooltip("Indices of roads that should be OPEN during this wave.")]
        public List<int> unlockedRoadIndices;

        [Header("Ground Units")] public List<GroundWaveSegment> groundSegments;

        [Header("Air Units")] public List<AirWaveSegment> airSegments;
    }


    [Serializable]
    public abstract class BaseWaveSegment
    {
        public GameObject enemyPrefab;
        [Min(1)] public int count = 5;
        [Min(0)] public float spawnInterval = 1.0f;

        [Tooltip("Seconds to wait before starting this specific group.")]
        public float initialDelay;

        [Header("Override Target")]
        [Tooltip(
            "Si vide, utilise la cible par défaut du Spawner/Path. Si rempli, les ennemis attaqueront cet objectif spécifique.")]
        public DestructibleObjective specificTarget;
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