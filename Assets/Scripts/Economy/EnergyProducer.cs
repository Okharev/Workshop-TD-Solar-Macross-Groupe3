using System;
using System.Collections.Generic;
using System.Linq;
using ObservableCollections;
using R3;
using UnityEngine;

namespace Economy
{
    public interface IReactiveEnergyProducer
    {
        // State
        ReadOnlyReactiveProperty<int> CurrentLoad { get; }
        ReadOnlyReactiveProperty<int> MaxCapacity { get; }
        ReadOnlyReactiveProperty<int> AvailableEnergy { get; } // Computed
        
        // Capabilities
        float BroadcastRadius { get; }
        bool IsLocalGenerator { get; }
        Vector3 Position { get; }

        // Logic
        int ConsumeUpTo(int amount, IReactiveEnergyConsumer consumer);
        void ReleaseEnergy(int amount, IReactiveEnergyConsumer consumer);
        
        // For Visuals (Observed by Visualizer)
        ObservableList<(IReactiveEnergyConsumer consumer, int amount)> ActiveConnections { get; }
    }
    

        public class EnergyProducer : MonoBehaviour, IReactiveEnergyProducer
    {
        [Header("Configuration")]
        [SerializeField] private bool _isMobileGenerator = true;
        [SerializeField] private float _broadcastRadius = 15f;
        
        [Header("Capacity")]
        [SerializeField] private int _initialCapacity = 100;

        // Reactive State
        public ReactiveProperty<int> Capacity { get; private set; }
        public ReactiveProperty<int> Load { get; private set; }
        public ReadOnlyReactiveProperty<int> CurrentLoad => Load;
        public ReadOnlyReactiveProperty<int> MaxCapacity => Capacity;
        
        // Computed Property (The "Stream" of availability)
        public ReadOnlyReactiveProperty<int> AvailableEnergy { get; private set; }

        // Connection Tracking (Visualizer observes this directly)
        public ObservableList<(IReactiveEnergyConsumer consumer, int amount)> ActiveConnections { get; } 
            = new();

        // Interface Properties
        public float BroadcastRadius => _broadcastRadius;
        public bool IsLocalGenerator => _isMobileGenerator;
        public Vector3 Position => transform.position;

        private void Awake()
        {
            Capacity = new ReactiveProperty<int>(_initialCapacity);
            Load = new ReactiveProperty<int>(0);

            // Combine Capacity and Load to auto-calculate Availability
            AvailableEnergy = Capacity
                .CombineLatest(Load, (cap, load) => cap - load)
                .ToReadOnlyReactiveProperty();

            // Register self to static registry (if you keep a global manager)
            ElectricityVisualizer.Instance?.RegisterProducer(this);
        }

        private void OnDestroy()
        {
            // Disconnect everyone gracefully
            foreach (var (consumer, amount) in ActiveConnections.ToArray())
            {
                consumer.ForceRefresh(); // Tell them to find power elsewhere
            }
            
            ActiveConnections.Clear();
            ElectricityVisualizer.Instance?.UnregisterProducer(this);
            
            Capacity.Dispose();
            Load.Dispose();
        }

        public int ConsumeUpTo(int amount, IReactiveEnergyConsumer consumer)
        {
            if (!isActiveAndEnabled) return 0;

            int currentAvailable = AvailableEnergy.CurrentValue;
            int take = Mathf.Min(currentAvailable, amount);

            if (take > 0)
            {
                // Update internal load
                Load.Value += take;

                // Update connection list for Visualizer
                // Check if connection exists to update amount, or add new
                var existingIndex = -1;
                for(int i=0; i<ActiveConnections.Count; i++) {
                    if (ActiveConnections[i].consumer == consumer) { existingIndex = i; break; }
                }

                if (existingIndex >= 0)
                {
                    var oldAmt = ActiveConnections[existingIndex].amount;
                    ActiveConnections[existingIndex] = (consumer, oldAmt + take);
                }
                else
                {
                    ActiveConnections.Add((consumer, take));
                }
            }

            return take;
        }

        public void ReleaseEnergy(int amount, IReactiveEnergyConsumer consumer)
        {
            Load.Value = Mathf.Max(0, Load.Value - amount);

            // Update list
            var existingIndex = -1;
            for(int i=0; i<ActiveConnections.Count; i++) {
                if (ActiveConnections[i].consumer == consumer) { existingIndex = i; break; }
            }

            if (existingIndex >= 0)
            {
                var oldAmt = ActiveConnections[existingIndex].amount;
                int newAmt = oldAmt - amount;

                if (newAmt <= 0)
                {
                    ActiveConnections.RemoveAt(existingIndex);
                }
                else
                {
                    ActiveConnections[existingIndex] = (consumer, newAmt);
                }
            }
        }
        
        // Helper for Visualizer/Gizmos
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _broadcastRadius);
        }
    }
    
}