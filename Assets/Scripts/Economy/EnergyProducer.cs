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

    public class EnergyProducer : MonoBehaviour, IEnergyProducer
    {
        [Header("Configuration")]
        [Tooltip("Is this a placed building (Generator) or a map sector (District)?")]
        [SerializeField]
        public bool isMobileGenerator = true;

        [SerializeField] public int maxCapacity = 100;

        [Header("Generator Settings")] [Tooltip("Radius of power provided.")] [SerializeField]
        public float broadcastRadius = 15f;

        [SerializeField] private LayerMask consumerLayer;
        [SerializeField] public int currentLoad;

        // State Tracking
        private readonly Dictionary<IEnergyConsumer, int> _outputMap = new();

        private Vector3 _lastPos;

        // Helpers
        private int _powerGridLayerIndex;

        private void Awake()
        {
            _powerGridLayerIndex = LayerMask.NameToLayer("PowerGrid");
            if (_powerGridLayerIndex == -1) _powerGridLayerIndex = 0;

            // Auto-detect layer if not set
            if (consumerLayer == 0)
            {
                var blockerIndex = LayerMask.NameToLayer("PlacementBlockers");
                if (blockerIndex != -1) consumerLayer = 1 << blockerIndex;
            }
        }

        private void Start()
        {
            _lastPos = transform.position;
            if (isMobileGenerator) GenerateRangeTrigger();
            ElectricityVisualizer.Instance?.RegisterProducer(this);
        }

        private void Update()
        {
            // If we havent moved, do nothing
            if (!transform.hasChanged) return;

            // if we moved beyond a threshold we launch check
            if (!((transform.position - _lastPos).sqrMagnitude > 0.01f)) return;

            NotifyNearbyConsumers();
            _lastPos = transform.position;

            // If we want to avoid delay
            transform.hasChanged = false;
        }

        private void OnEnable()
        {
            ElectricityVisualizer.Instance?.RegisterProducer(this);
            NotifyNearbyConsumers();
        }

        private void OnDisable()
        {
            ElectricityVisualizer.Instance?.UnregisterProducer(this);

            // Force disconnect everyone gracefully
            var consumers = new List<IEnergyConsumer>(_outputMap.Keys);
            foreach (var consumer in consumers)
                // This triggers the Consumer to look for power elsewhere
                consumer.OnPowerLost();

            _outputMap.Clear();
            currentLoad = 0;
            OnStateChanged?.Invoke();
        }

        private void OnDestroy()
        {
            ElectricityVisualizer.Instance?.UnregisterProducer(this);
            _outputMap.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawWireSphere(transform.position, broadcastRadius);
        }

        public float GetBroadcastRadius()
        {
            return broadcastRadius;
        }

        public int GetAvailableEnergy()
        {
            return maxCapacity - currentLoad;
        }

        public bool IsLocalGenerator()
        {
            return isMobileGenerator;
        }

        public Vector3 GetPosition()
        {
            return transform.position;
        }

        // Logic to give energy
        public int ConsumeUpTo(int amount, IEnergyConsumer consumer)
        {
            if (!isActiveAndEnabled) return 0;

            var available = maxCapacity - currentLoad;
            var amountToGive = Mathf.Min(available, amount);

            if (amountToGive > 0)
            {
                currentLoad += amountToGive;
                if (!_outputMap.TryAdd(consumer, amountToGive))
                    _outputMap[consumer] += amountToGive;

                OnStateChanged?.Invoke();
            }

            return amountToGive;
        }

        // Logic to take back energy
        public void ReleaseEnergy(int amount, IEnergyConsumer consumer)
        {
            currentLoad = Mathf.Max(0, currentLoad - amount);

            if (_outputMap.ContainsKey(consumer))
            {
                _outputMap[consumer] -= amount;
                if (_outputMap[consumer] <= 0) _outputMap.Remove(consumer);
            }

            OnStateChanged?.Invoke();
        }

        public event Action OnStateChanged;

        public IReadOnlyDictionary<IEnergyConsumer, int> GetOutputMap()
        {
            return _outputMap;
        }

        private void NotifyNearbyConsumers()
        {
            var hits = Physics.OverlapSphere(transform.position, broadcastRadius, consumerLayer);

            foreach (var hit in hits)
            {
                var consumer = hit.GetComponent<IEnergyConsumer>();

                consumer?.RefreshConnection();
            }
        }

        private void GenerateRangeTrigger()
        {
            if (transform.Find("EnergyField_Generated")) return;

            var fieldObj = new GameObject("EnergyField_Generated");
            fieldObj.transform.SetParent(transform);
            fieldObj.transform.localPosition = Vector3.zero;
            fieldObj.layer = _powerGridLayerIndex;

            var col = fieldObj.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = broadcastRadius;

            var link = fieldObj.AddComponent<EnergyFieldLink>();
            link.Initialize(this);
        }
    }
}