using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Economy
{
    public interface IEnergyConsumer
    {
        int GetEnergyRequirement();
        void OnPowerLost(); 
        void OnPowerRestored();
        Vector3 GetPosition();
        void RefreshConnection(); 
    }

    public class EnergyConsumer : MonoBehaviour, IEnergyConsumer
    {
        [SerializeField] private int totalRequirement = 100;
        public LayerMask energylayer;

        private readonly Dictionary<IEnergyProducer, int> energySources = new();
        private bool isPowered;
        public bool IsPowered => isPowered; 
        
        private void OnEnable()
        {
            if (totalRequirement > 0) 
            {
                // We delay slightly to allow the Generators to Initialize 
                // if this is the very first frame of the scene.
                StartCoroutine(InitialConnectRoutine());
            }
        }

        private void OnDisable()
        {
            // CRITICAL FIX:
            // When this object is turned off, we must release the energy 
            // back to the grid immediately.
            Disconnect();
        }

        private IEnumerator InitialConnectRoutine()
        {
            yield return new WaitForFixedUpdate();
            if(enabled) AttemptConnection();
        }

        public void RefreshConnection()
        {
            if (!enabled || !gameObject.activeInHierarchy) return;

            Disconnect();
            AttemptConnection();
        }
        
        public void OnPowerLost()
        {
            isPowered = false;
            Disconnect();

            // Only try to reconnect if we are actually active
            if (gameObject.activeInHierarchy && enabled)
            {
                StopAllCoroutines();
                StartCoroutine(ReconnectRoutine());
            }
        }

        private void Disconnect()
        {
            foreach (var kvp in energySources)
            {
                // Check for null in case the Producer was actually destroyed
                if (kvp.Key != null && !kvp.Key.Equals(null))
                {
                    kvp.Key.ReleaseEnergy(kvp.Value, this);
                }
            }
            
            energySources.Clear();
            isPowered = false;
        }

        public int GetEnergyRequirement()
        {
            return totalRequirement;
        }
        
        private IEnumerator ReconnectRoutine()
        {
            yield return new WaitForFixedUpdate();
            AttemptConnection();
        }

        public void OnPowerRestored() { }

        public Vector3 GetPosition()
        {
            return transform.position;
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

        public void AttemptConnection()
        {
            if (isPowered) Disconnect();

            Vector3 center = transform.position + Vector3.up * 0.5f; 
            Collider[] hits = Physics.OverlapSphere(center, 0.5f, energylayer);

            var sortedProducers = hits
                .Select(h => GetProducerFromCollider(h))
                // Ensure we don't try to pull from a disabled producer
                .Where(p => p != null && !p.Equals(null)) 
                .Distinct()
                // Extra check: ensure the producer object is actually active
                .Where(p => (p as MonoBehaviour).isActiveAndEnabled && p.GetAvailableEnergy() > 0)
                .OrderByDescending(p => p.IsLocalGenerator()) 
                .ThenByDescending(p => p.GetAvailableEnergy())
                .ToList();

            int needed = totalRequirement;
            var proposedConnections = new Dictionary<IEnergyProducer, int>();

            foreach (var producer in sortedProducers)
            {
                int available = producer.GetAvailableEnergy();
                int take = Mathf.Min(available, needed);

                if (take > 0)
                {
                    proposedConnections.Add(producer, take);
                    needed -= take;
                }

                if (needed <= 0) break;
            }

            if (needed <= 0)
            {
                foreach (var kvp in proposedConnections)
                {
                    kvp.Key.ConsumeUpTo(kvp.Value, this);
                    energySources.Add(kvp.Key, kvp.Value);
                }
                isPowered = true;
                OnPowerRestored();
            }
            else
            {
                isPowered = false;
            }
        }
    }
}