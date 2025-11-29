using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Economy
{
    public interface IEnergyConsumer
    {
        bool IsPowered { get; }
        int GetEnergyRequirement();
        Vector3 GetPosition();

        event Action<bool> OnPowerChanged;

        void RefreshConnection();
        void OnPowerLost();
    }

    public class EnergyConsumer : MonoBehaviour, IEnergyConsumer
    {
        [Header("Settings")] [SerializeField] private int totalRequirement = 100;

        [SerializeField] private float checkInterval = 0.25f;
        public LayerMask energylayer;

        // Intern
        private readonly Dictionary<IEnergyProducer, int> energySources = new();
        private bool _isPowered;
        private Vector3 lastPosition;

        private void OnEnable()
        {
            lastPosition = transform.position;
            if (totalRequirement > 0) StartCoroutine(InitialConnectRoutine());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            DisconnectAll();
        }

        // EVENTS & STATE
        public event Action<bool> OnPowerChanged;

        public bool IsPowered
        {
            get => _isPowered;
            private set
            {
                if (_isPowered != value)
                {
                    _isPowered = value;
                    OnPowerChanged?.Invoke(_isPowered);
                }
            }
        }

        public void OnPowerLost()
        {
            DisconnectAll();
        }

        public void RefreshConnection()
        {
            if (!enabled || !gameObject.activeInHierarchy) return;
            AttemptConnection();
        }

        public int GetEnergyRequirement()
        {
            return totalRequirement;
        }

        public Vector3 GetPosition()
        {
            return transform.position;
        }

        private IEnumerator InitialConnectRoutine()
        {
            yield return new WaitForFixedUpdate();
            if (enabled) AttemptConnection();
            StartCoroutine(MonitorConnectionsRoutine());
        }

        private IEnumerator MonitorConnectionsRoutine()
        {
            var wait = new WaitForSeconds(checkInterval);
            while (enabled)
            {
                yield return wait;

                // 1. Movement detection
                var hasMoved = (transform.position - lastPosition).sqrMagnitude > 0.01f;
                if (hasMoved) lastPosition = transform.position;

                // 2. Check valid sources (destroyed/disabled)
                var connectionLost = ValidateExistingConnections();

                // 3. if we moved, lost a source, or not enough power -> Full scan
                if (hasMoved || connectionLost || !IsPowered) AttemptConnection();
            }
        }

        private void DisconnectAll()
        {
            foreach (var kvp in energySources)
                if (kvp.Key != null && !kvp.Key.Equals(null))
                    kvp.Key.ReleaseEnergy(kvp.Value, this);

            energySources.Clear();
            IsPowered = false;
        }

        private bool ValidateExistingConnections()
        {
            var changed = false;
            var producers = new List<IEnergyProducer>(energySources.Keys);

            foreach (var producer in producers)
                // If producer is null or disabled
                if (producer == null || producer.Equals(null) || !(producer as MonoBehaviour).isActiveAndEnabled)
                {
                    energySources.Remove(producer);
                    changed = true;
                }

            // Let attempt connection handle checks
            if (changed && energySources.Count == 0) IsPowered = false;
            return changed;
        }

        public void AttemptConnection()
        {
            // 1. SCAN : search surrounding
            var center = transform.position + Vector3.up * 0.5f;
            var hits = Physics.OverlapSphere(center, 0.5f, energylayer);

            // 2. take a unique
            var allCandidates = hits
                .Select(h => GetProducerFromCollider(h))
                .Where(p => p != null && !p.Equals(null))
                .Distinct()
                .Where(p => (p as MonoBehaviour).isActiveAndEnabled)
                .ToList();

            // 3. Sort by priority: first local then by distance
            var sortedProducers = allCandidates
                .OrderByDescending(p => p.IsLocalGenerator())
                .ThenBy(p => Vector3.Distance(transform.position, p.GetPosition()))
                .ToList();

            // 4. plan
            var stillNeeded = totalRequirement;
            var newPlan = new Dictionary<IEnergyProducer, int>();

            // count how many we found
            var totalFound = 0;

            foreach (var producer in sortedProducers)
            {
                if (Vector3.Distance(transform.position, producer.GetPosition()) > producer.GetBroadcastRadius() + 1.0f)
                    continue;

                var currentConsumption = energySources.ContainsKey(producer) ? energySources[producer] : 0;
                var realAvailable = producer.GetAvailableEnergy() + currentConsumption;

                if (realAvailable <= 0) continue;

                var take = Mathf.Min(realAvailable, stillNeeded);

                if (take > 0)
                {
                    newPlan.Add(producer, take);
                    stillNeeded -= take;
                    totalFound += take; // Cumulation of what we found
                }

                if (stillNeeded <= 0) break;
            }

            // All or nothing
            if (totalFound < totalRequirement)
                newPlan.Clear();

            // 5. if new plan is empty (no sources, or not enough power)
            // ApplyConnectionPlan disconnects everything
            ApplyConnectionPlan(newPlan);
        }

        private void ApplyConnectionPlan(Dictionary<IEnergyProducer, int> newPlan)
        {
            // A. Cleanup of old resources missing from old plan
            var oldProducers = new List<IEnergyProducer>(energySources.Keys);
            foreach (var p in oldProducers)
                if (!newPlan.ContainsKey(p))
                {
                    // case 1 : Disconnets totally if we exited zone or there is better generator
                    p.ReleaseEnergy(energySources[p], this);
                    energySources.Remove(p);
                }
                else if (newPlan[p] < energySources[p])
                {
                    // Case 2 : Reduce charge
                    var diff = energySources[p] - newPlan[p];
                    p.ReleaseEnergy(diff, this);
                    energySources[p] = newPlan[p];
                }

            // B. add or augmentation of sources
            foreach (var kvp in newPlan)
            {
                var p = kvp.Key;
                var targetAmount = kvp.Value;

                if (!energySources.ContainsKey(p))
                {
                    // Case 3 : new connection
                    p.ConsumeUpTo(targetAmount, this);
                    energySources.Add(p, targetAmount);
                }
                else if (energySources[p] < targetAmount)
                {
                    // Case 4 : increase demand
                    var diff = targetAmount - energySources[p];
                    p.ConsumeUpTo(diff, this);
                    energySources[p] = targetAmount;
                }
            }

            // C. Update final state
            var total = 0;
            foreach (var v in energySources.Values) total += v;

            IsPowered = total >= totalRequirement;
        }

        private IEnergyProducer GetProducerFromCollider(Collider h)
        {
            var p = h.GetComponent<IEnergyProducer>();
            if (p == null)
            {
                var link = h.GetComponent<EnergyFieldLink>();
                if (link != null) p = link.GetProducer();
            }

            return p;
        }
    }
}