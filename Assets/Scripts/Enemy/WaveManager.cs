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

        public float timeBetweenWaves = 30f; // 30 secondes par défaut

        // --- Reactive State (Nouveau !) ---
        // On utilise tes classes réactives pour que l'UI se mette à jour toute seule
        public ReactiveInt EnemiesRemaining = new(0);
        public ReactiveInt TotalEnemiesInWave = new(0);
        public ReactiveFloat TimeToNextWave = new(0);
        private Coroutine _countdownCoroutine;

        // Runtime State

        public bool IsWaveActive { get; private set; }

        public int CurrentWaveIndex { get; private set; } = -1;

        // --- Events ---
        public event Action<int, string> OnWaveStarted;
        public event Action OnWaveFinished; // Déclenché quand tous les ennemis sont morts
        public event Action OnWaveSequenceEnded; // Fin du timer ou appui bouton
        public event Action OnAllWavesCompleted;

        [ContextMenu("Start Next Wave")]
        public void StartNextWave()
        {
            // Si une vague est active (ennemis vivants ou en train de spawn), on bloque
            if (IsWaveActive)
            {
                Debug.LogWarning("Cannot start next wave; a wave is currently active.");
                return;
            }

            // Si on est dans le compte à rebours (timer), on l'arrête pour lancer tout de suite
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

        // --- Méthode à appeler quand un ennemi meurt ---
        public void RegisterEnemyDeath()
        {
            if (EnemiesRemaining.Value > 0)
            {
                EnemiesRemaining.Value--;

                // Vérification de victoire de vague
                if (EnemiesRemaining.Value <= 0 && IsWaveActive) OnWaveDefeated();
            }
        }

        private void OnWaveDefeated()
        {
            IsWaveActive = false;
            OnWaveFinished?.Invoke();

            // Lancer le timer pour la prochaine vague si ce n'était pas la dernière
            if (CurrentWaveIndex < waves.Count - 1)
                _countdownCoroutine = StartCoroutine(WaveCountdownRoutine());
            else
                OnAllWavesCompleted?.Invoke();
        }

        private IEnumerator WaveCountdownRoutine()
        {
            var timer = timeBetweenWaves;
            TimeToNextWave.Value = timer;

            while (timer > 0)
            {
                yield return null; // Attendre une frame
                timer -= Time.deltaTime;

                // On met à jour la valeur réactive (l'UI écoutera ça)
                // On s'assure de ne pas afficher de négatif
                TimeToNextWave.Value = Mathf.Max(0, timer);
            }

            // Le temps est écoulé, on lance la vague !
            StartNextWave();
        }

        private IEnumerator RunWaveRoutine(WaveProfile wave)
        {
            IsWaveActive = true;
            OnWaveStarted?.Invoke(CurrentWaveIndex + 1, wave.waveName);
            Debug.Log($"Starting Wave {CurrentWaveIndex + 1}: {wave.waveName}");

            ConfigureRoadsForWave(wave);

            // 1. Calcul du nombre total d'ennemis pour cette vague
            var totalGround = wave.groundSegments.Sum(s => s.count);
            var totalAir = wave.airSegments.Sum(s => s.count);

            TotalEnemiesInWave.Value = totalGround + totalAir;
            EnemiesRemaining.Value = totalGround + totalAir;

            yield return null;

            var activeSpawns = new List<Coroutine>();

            // Lancement des spawners (Code existant conservé)
            foreach (var segment in wave.groundSegments)
                if (segment.targetSpawner)
                    activeSpawns.Add(StartCoroutine(SpawnGroundSegment(segment)));

            foreach (var segment in wave.airSegments)
                if (segment.targetPath)
                    activeSpawns.Add(StartCoroutine(SpawnAirSegment(segment)));

            // On attend que le SPAWN soit fini, mais la vague reste active tant que les ennemis sont vivants
            foreach (var c in activeSpawns) yield return c;

            // Note: On ne met plus _isWaveActive = false ici. 
            // C'est RegisterEnemyDeath qui le fera quand EnemiesRemaining == 0.
        }

        // --- Ground Logic (Inchangé) ---
        private IEnumerator SpawnGroundSegment(GroundWaveSegment segment)
        {
            if (segment.initialDelay > 0) yield return new WaitForSeconds(segment.initialDelay);
            for (var i = 0; i < segment.count; i++)
            {
                segment.targetSpawner.Spawn(segment.enemyPrefab, segment.specificTarget);
                if (segment.spawnInterval > 0) yield return new WaitForSeconds(segment.spawnInterval);
            }
        }

        // --- Air Logic (Inchangé) ---
        private IEnumerator SpawnAirSegment(AirWaveSegment segment)
        {
            if (segment.initialDelay > 0) yield return new WaitForSeconds(segment.initialDelay);
            if (segment.targetPath == null) yield break;
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

    // --- Data Classes ---

    [Serializable]
    public class WaveProfile
    {
        public string waveName = "Wave 1";

        [Header("Map State")] [Tooltip("Indices of roads that should be OPEN during this wave.")]
        public List<int> unlockedRoadIndices;

        [Header("Ground Units")] public List<GroundWaveSegment> groundSegments;

        [Header("Air Units")] public List<AirWaveSegment> airSegments;
    }

    // Base class for shared settings
// Dans WaveManager.cs, tout en bas

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