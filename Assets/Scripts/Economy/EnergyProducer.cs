using System;
using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    public interface IEnergyProducer
    {
        int ConsumeUpTo(int amount, IEnergyConsumer consumer);
        void ReleaseEnergy(int amount, IEnergyConsumer consumer);
        int GetAvailableEnergy();
        bool IsLocalGenerator();
        Vector3 GetPosition();
        float GetBroadcastRadius(); 
    }

    [RequireComponent(typeof(Collider))]
    public class EnergyProducer : MonoBehaviour, IEnergyProducer
    {
        [Header("Configuration")]
        [Tooltip("Is this a placed building (Generator) or a map sector (District)?")]
        [SerializeField]
        public bool isMobileGenerator = true;

        [SerializeField] public int maxCapacity = 100;

        [Header("Generator Settings")] 
        [Tooltip("Radius of power provided.")] 
        [SerializeField]
        public float broadcastRadius = 15f;
        
        [SerializeField] private LayerMask consumerLayer;

        private readonly Dictionary<IEnergyConsumer, int> outputMap = new();

        private int powerGridLayerIndex;
        [SerializeField] public int currentLoad;

        public event Action OnStateChanged;
        
        public float GetBroadcastRadius() => broadcastRadius;

        private void Awake()
        {
            powerGridLayerIndex = LayerMask.NameToLayer("PowerGrid");
            if (powerGridLayerIndex == -1) powerGridLayerIndex = 0; 

            int blockerIndex = LayerMask.NameToLayer("PlacementBlockers");
            if (blockerIndex != -1) consumerLayer = 1 << blockerIndex; 
        }
        
        private void Start()
        {
            if (isMobileGenerator)
            {
                GenerateRangeTrigger();
            }
            // Ensure visualizer knows about us if Start runs after OnEnable
            ElectricityVisualizer.Instance?.RegisterProducer(this);
            
            EnergyHeatmapSystem.Instance?.Register(this);
        }

        private void OnEnable()
        {
            ElectricityVisualizer.Instance?.RegisterProducer(this);
            
            EnergyHeatmapSystem.Instance?.Register(this);

            // When a District is re-enabled, it MUST tell the consumers 
            // inside it to try and connect again.
            NotifyNearbyConsumers();
        }

        private void NotifyNearbyConsumers()
        {
            // For Districts, this radius must be set to the Map Radius 
            // (handled in DistrictGenerator.cs now)
            Collider[] hits = Physics.OverlapSphere(transform.position, broadcastRadius, consumerLayer);

            foreach (var hit in hits)
            {
                var consumer = hit.GetComponent<IEnergyConsumer>();
                if (consumer != null)
                {
                    consumer.RefreshConnection();
                }
            }
        }

        private void OnDisable()
        {
            ElectricityVisualizer.Instance?.UnregisterProducer(this);
            
            EnergyHeatmapSystem.Instance?.Unregister(this);

            var consumers = new List<IEnergyConsumer>(outputMap.Keys);
            foreach (var consumer in consumers)
            {
                consumer.OnPowerLost();
            }

            outputMap.Clear();
            currentLoad = 0;
            OnStateChanged?.Invoke();
        }
        
        private void OnDestroy()
        {
            ElectricityVisualizer.Instance?.UnregisterProducer(this);
            outputMap.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw gizmo for both types to debug radius issues
            Gizmos.color = new Color(0, 1, 1, 0.3f); 
            Gizmos.DrawWireSphere(transform.position, broadcastRadius);
        }

        public int ConsumeUpTo(int amount, IEnergyConsumer consumer)
        {
            if (!isActiveAndEnabled) return 0;

            var available = maxCapacity - currentLoad;
            var amountToGive = Mathf.Min(available, amount);

            if (amountToGive > 0)
            {
                currentLoad += amountToGive;
                if (!outputMap.TryAdd(consumer, amountToGive))
                    outputMap[consumer] += amountToGive;

                OnStateChanged?.Invoke();
            }

            return amountToGive;
        }

        public void ReleaseEnergy(int amount, IEnergyConsumer consumer)
        {
            currentLoad = Mathf.Max(0, currentLoad - amount);

            if (outputMap.ContainsKey(consumer))
            {
                outputMap[consumer] -= amount;
                if (outputMap[consumer] <= 0) outputMap.Remove(consumer);
            }

            OnStateChanged?.Invoke();
        }

        public int GetAvailableEnergy() => maxCapacity - currentLoad;
        public bool IsLocalGenerator() => isMobileGenerator;
        public Vector3 GetPosition() => transform.position;
        public IReadOnlyDictionary<IEnergyConsumer, int> GetOutputMap() => outputMap;

        private void GenerateRangeTrigger()
        {
            if(transform.Find("EnergyField_Generated") != null) return;

            var fieldObj = new GameObject("EnergyField_Generated");
            fieldObj.transform.SetParent(transform);
            fieldObj.transform.localPosition = Vector3.zero;
            fieldObj.layer = powerGridLayerIndex;

            var col = fieldObj.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = broadcastRadius;

            var link = fieldObj.AddComponent<EnergyFieldLink>();
            link.Initialize(this);
        }
    }
}