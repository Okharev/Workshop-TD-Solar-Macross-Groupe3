using System;
using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    [DefaultExecutionOrder(-100)]
    public class EnergyGridManager : MonoBehaviour
    {
        [Header("Optimization Settings")]
        [Tooltip("Size of the spatial bucket. Should be roughly the size of your average power radius.")]
        [SerializeField]
        private float cellSize = 20f;

        private readonly List<EnergyConsumer> _cachedConsumers = new();

        // --- Cached Lists (For sorting/iterating without Allocations) ---
        private readonly List<EnergyProducer> _cachedProducers = new();
        private readonly HashSet<EnergyConsumer> _consumers = new();
        private readonly ConsumerComparer _consumerSorter = new();

        // --- Raw Registries ---
        private readonly HashSet<EnergyProducer> _producers = new();

        // --- Comparers (Structs to avoid boxing) ---
        private readonly ProducerComparer _producerSorter = new();

        // --- Spatial Partitioning ---
        // Maps a Grid Coordinate (x,y) -> List of Producers covering that cell
        private readonly Dictionary<Vector2Int, List<EnergyProducer>> _spatialGrid = new();

        private bool _isDirty;
        public static EnergyGridManager Instance { get; private set; }

        // --- Result Graph ---
        public Dictionary<EnergyConsumer, Dictionary<EnergyProducer, int>> ConnectionGraph { get; } = new();
        public IReadOnlyCollection<EnergyProducer> AllProducers => _producers;
        public IReadOnlyCollection<EnergyConsumer> AllConsumers => _consumers;

        private void Awake()
        {
            if (Instance && Instance != this) Destroy(gameObject);
            else Instance = this;
        }

        private void LateUpdate()
        {
            if (_isDirty)
            {
                ResolveGrid();
                _isDirty = false;
            }
        }

        public event Action OnGridResolved;

        public void Register(EnergyProducer p)
        {
            _producers.Add(p);
            MarkDirty();
        }

        public void Unregister(EnergyProducer p)
        {
            _producers.Remove(p);
            MarkDirty();
        }

        public void Register(EnergyConsumer c)
        {
            _consumers.Add(c);
            MarkDirty();
        }

        public void Unregister(EnergyConsumer c)
        {
            _consumers.Remove(c);
            MarkDirty();
        }

        public void MarkDirty()
        {
            _isDirty = true;
        }

        private void ResolveGrid()
        {
            // 1. CLEAR & PREPARE DATA
            ConnectionGraph.Clear();

            // Clear Spatial Grid buckets (but keep the List instances to avoid GC)
            foreach (var list in _spatialGrid.Values) list.Clear();

            // Refresh Cached Lists from HashSets
            _cachedProducers.Clear();
            _cachedProducers.AddRange(_producers);

            _cachedConsumers.Clear();
            _cachedConsumers.AddRange(_consumers);

            // 2. SORTING (Garbage Free)
            // Sort Producers: Mobile > High Capacity
            _cachedProducers.Sort(_producerSorter);
            // Sort Consumers: High Priority > X Position
            _cachedConsumers.Sort(_consumerSorter);

            // 3. BUILD SPATIAL GRID
            // Instead of checking distance, producers registers themselves into
            // every grid cell their radius touches.
            foreach (var producer in _cachedProducers)
            {
                if (!producer.isActiveAndEnabled) continue;

                producer.ResetLoad(); // Reset load while we are here

                var r = producer.BroadcastRadius.Value;
                var pos = producer.transform.position;

                // Calculate min/max grid cells this producer touches
                var minX = Mathf.FloorToInt((pos.x - r) / cellSize);
                var maxX = Mathf.FloorToInt((pos.x + r) / cellSize);
                var minZ = Mathf.FloorToInt((pos.z - r) / cellSize);
                var maxZ = Mathf.FloorToInt((pos.z + r) / cellSize);

                for (var x = minX; x <= maxX; x++)
                for (var z = minZ; z <= maxZ; z++)
                {
                    var coord = new Vector2Int(x, z);
                    if (!_spatialGrid.TryGetValue(coord, out var bucket))
                    {
                        bucket = new List<EnergyProducer>();
                        _spatialGrid[coord] = bucket;
                    }

                    bucket.Add(producer);
                }
            }

            // 4. RESOLUTION LOOP
            foreach (var consumer in _cachedConsumers)
            {
                if (!consumer.isActiveAndEnabled) continue;

                var totalNeeded = consumer.TotalRequirement.Value;
                if (totalNeeded <= 0)
                {
                    consumer.SetPoweredState(true);
                    continue;
                }

                // Determine which cell the consumer is in
                var cell = new Vector2Int(
                    Mathf.FloorToInt(consumer.transform.position.x / cellSize),
                    Mathf.FloorToInt(consumer.transform.position.z / cellSize)
                );

                // Get candidates from this specific cell
                // (Producers have already "painted" themselves into this cell if they reach it)
                if (!_spatialGrid.TryGetValue(cell, out var candidates) || candidates.Count == 0)
                {
                    consumer.SetPoweredState(false);
                    continue;
                }

                Dictionary<EnergyProducer, int> currentAllocation = new();
                var fulfilledAmount = 0;

                // Iterate the pre-sorted candidate list for this cell
                foreach (var producer in candidates)
                {
                    // Double check exact distance (Circle vs Square grid accuracy)
                    var distSqr = (producer.transform.position - consumer.transform.position).sqrMagnitude;
                    var range = producer.BroadcastRadius.Value;
                    if (distSqr > range * range) continue;

                    var available = producer.GetAvailable();
                    if (available <= 0) continue;

                    var stillNeed = totalNeeded - fulfilledAmount;
                    var take = Mathf.Min(available, stillNeed);

                    producer.AddLoad(take);

                    currentAllocation.TryAdd(producer, 0);
                    currentAllocation[producer] += take;

                    fulfilledAmount += take;
                    if (fulfilledAmount >= totalNeeded) break;
                }

                // 5. COMMIT OR ROLLBACK
                if (fulfilledAmount >= totalNeeded)
                {
                    ConnectionGraph[consumer] = currentAllocation;
                    consumer.SetPoweredState(true);
                }
                else
                {
                    // Rollback load
                    foreach (var kvp in currentAllocation) kvp.Key.RemoveLoad(kvp.Value);
                    consumer.SetPoweredState(false);
                }
            }

            OnGridResolved?.Invoke();
        }

        // --- CUSTOM COMPARERS ---
        // Avoiding boxing/allocations

        private class ProducerComparer : IComparer<EnergyProducer>
        {
            public int Compare(EnergyProducer x, EnergyProducer y)
            {
                // 1. Mobile Generators First (y vs x for Descending)
                var mobileComp = (y is { isMobileGenerator: true }).CompareTo(x is { isMobileGenerator: true });
                return mobileComp != 0
                    ? mobileComp
                    :
                    // 2. Higher Capacity First
                    y.MaxCapacity.Value.CompareTo(x.MaxCapacity.Value);
            }
        }

        private class ConsumerComparer : IComparer<EnergyConsumer>
        {
            public int Compare(EnergyConsumer x, EnergyConsumer y)
            {
                // 1. Priority First (Critical > Low)
                var priorityComp = y.Priority.CompareTo(x.Priority);
                return priorityComp != 0 ? priorityComp :
                    // 2. Deterministic Tie-Breaker (Position X)
                    // This ensures that if priority is equal, the order doesn't jitter
                    x.transform.position.x.CompareTo(y.transform.position.x);
            }
        }
    }
}