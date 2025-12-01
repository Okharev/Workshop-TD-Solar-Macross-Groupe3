using System;
using System.Collections.Generic;
using System.Linq;
using R3; // Core R3
using UnityEngine;

namespace Economy
{
    public interface IReactiveEnergyConsumer
    {
        ReadOnlyReactiveProperty<bool> IsPowered { get; }
        ReactiveProperty<int> EnergyRequirement { get; }
        Vector3 Position { get; }
        
        // Signals that this consumer needs a grid refresh (e.g. source died)
        void ForceRefresh();
    }
    
        public class EnergyConsumer : MonoBehaviour, IReactiveEnergyConsumer
    {
        [Header("Settings")] 
        [SerializeField] private int _initialRequirement = 100;
        [SerializeField] private LayerMask _producerLayer;
        
        public ReactiveProperty<int> EnergyRequirement { get; private set; }
        
        // IsPowered is ReadOnly for the outside, but writable internally
        private readonly ReactiveProperty<bool> _isPowered = new(false);
        public ReadOnlyReactiveProperty<bool> IsPowered => _isPowered;

        public Vector3 Position => transform.position;

        // Internal State
        private readonly Dictionary<IReactiveEnergyProducer, int> _currentSources = new();
        private readonly Subject<Unit> _refreshTrigger = new();

        
        private void Awake()
        {
            EnergyRequirement = new ReactiveProperty<int>(_initialRequirement);
        }

        private void Start()
        {
            Vector3 lastPos = transform.position;
            
            // ---------------------------------------------------------
            // 1. MOVEMENT DETECTION (The R3 Way)
            // ---------------------------------------------------------
            var moveTrigger = Observable
                // Use EveryUpdate with the FixedUpdate provider
                .EveryUpdate(UnityFrameProvider.FixedUpdate)
                .Select(_ => transform.position)
                // DistinctUntilChanged with predicate: returns true if "equal" (no change)
                .Where(currentPos => 
                {
                    // Calculate distance
                    float dist = (currentPos - lastPos).sqrMagnitude;
            
                    // If moved significantly
                    if (dist > 0.01f) 
                    {
                        lastPos = currentPos; // Update local state
                        return true; // Allow event to pass
                    }
                    return false; // Block event
                })
                .ThrottleFirst(TimeSpan.FromSeconds(0.2f))
                    .Select(_ => Unit.Default);

            // ---------------------------------------------------------
            // 2. REQUIREMENT CHANGES
            // ---------------------------------------------------------
            var requirementChanged = EnergyRequirement
                .Skip(1) // Skip the initial value set in Awake
                .Select(_ => Unit.Default);

            // ---------------------------------------------------------
            // 3. MERGE & SUBSCRIBE
            // ---------------------------------------------------------
            Observable
                .Merge(moveTrigger, _refreshTrigger, requirementChanged)
                .Subscribe(_ => AttemptConnection())
                .AddTo(this);
            
            // Initial attempt
            AttemptConnection();
        }

        private void OnDisable()
        {
            DisconnectAll();
        }

        public void ForceRefresh()
        {
            _refreshTrigger.OnNext(Unit.Default);
        }

        private void AttemptConnection()
        {
            CleanupInvalidSources();

            int currentPower = _currentSources.Values.Sum();
            int needed = EnergyRequirement.Value - currentPower;

            // If we are fully powered, do nothing (or add logic to release excess)
            if (needed <= 0)
            {
                _isPowered.Value = true;
                return;
            }

            // 1. Scan for candidates
            var hits = Physics.OverlapSphere(transform.position, 15f, _producerLayer);
            
            var candidates = hits
                .Select(h => h.GetComponent<IReactiveEnergyProducer>() ?? h.GetComponentInParent<IReactiveEnergyProducer>())
                .Where(p => p != null && !p.Equals(null))
                .Distinct()
                .OrderBy(p => Vector3.Distance(transform.position, p.Position))
                .ToList();

            // 2. Consume
            foreach (var p in candidates)
            {
                if (Vector3.Distance(transform.position, p.Position) > p.BroadcastRadius) continue;

                int available = p.AvailableEnergy.CurrentValue;
                if (available <= 0) continue;

                int take = Mathf.Min(needed, available);
                int actuallyTaken = p.ConsumeUpTo(take, this);

                if (actuallyTaken > 0)
                {
                    if (!_currentSources.TryAdd(p, actuallyTaken)) _currentSources[p] += actuallyTaken;

                    needed -= actuallyTaken;
                }

                if (needed <= 0) break;
            }

            _isPowered.Value = (EnergyRequirement.Value - needed) <= 0;
        }

        private void CleanupInvalidSources()
        {
            var invalid = _currentSources.Keys
                .Where(k => k == null || k.Equals(null) || (k as MonoBehaviour) == null || !(k as MonoBehaviour).isActiveAndEnabled)
                .ToList();

            foreach (var p in invalid) _currentSources.Remove(p);
            
            // Re-check power status after cleanup
            int currentTotal = _currentSources.Values.Sum();
            if (currentTotal < EnergyRequirement.Value) _isPowered.Value = false;
        }

        private void DisconnectAll()
        {
            foreach (var kvp in _currentSources)
            {
                if (kvp.Key != null && !kvp.Key.Equals(null))
                {
                    kvp.Key.ReleaseEnergy(kvp.Value, this);
                }
            }
            _currentSources.Clear();
            _isPowered.Value = false;
        }
    }
}