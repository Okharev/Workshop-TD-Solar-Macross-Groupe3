using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaveSystem
{
    [CreateAssetMenu(fileName = "NewWave", menuName = "TowerDefense/Wave Config")]
    public class WaveConfig : ScriptableObject
    {
        [Header("Map Progression")]
        [Tooltip("Indices of the splines to unblock at the start of this wave")]
        public List<int> roadsToUnblock = new List<int>();

        [Header("Spawning Instructions")]
        public List<SpawnerInstruction> spawnerInstructions = new List<SpawnerInstruction>();
    }

    [Serializable]
    public class SpawnerInstruction
    {
        [Tooltip("Matches the ID on the EnemySpawner component")]
        public string spawnerID; 
        public List<WaveSegment> segments = new List<WaveSegment>();
    }

    [Serializable]
    public class WaveSegment
    {
        public GameObject enemyPrefab;
        [Min(1)] public int count = 5;
        [Tooltip("Total time to spawn this batch of enemies")]
        public float duration = 10f;
    
        [Tooltip("Delay before this segment starts (relative to previous segment finish)")]
        public float preDelay = 0f;
    }
}