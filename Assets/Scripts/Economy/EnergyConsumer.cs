using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Economy
{
    public enum EnergyPriority
    {
        Critical = 100,
        Standard = 50,
        Low = 10,
        Background = 0
    }

    public sealed class EnergyConsumer : MonoBehaviour
    {
        [Header("Settings")] [SerializeField] private EnergyPriority priority = EnergyPriority.Standard;

        [SerializeField] public ReactiveInt totalRequirement = new(100);

        private Vector3 _lastPos;
        public EnergyPriority Priority => priority;

        public bool IsPowered { get; private set; }

        private void Start()
        {
            _lastPos = transform.position;
        }

        private void Update()
        {
            if ((transform.position - _lastPos).sqrMagnitude > 0.01f)
            {
                _lastPos = transform.position;
                EnergyGridManager.Instance.MarkDirty();
            }
        }

        private void OnDestroy()
        {
            EnergyGridManager.Instance?.Unregister(this);
        }

        private void OnEnable()
        {
            EnergyGridManager.Instance?.Register(this);
            totalRequirement.Subscribe(OnRequirementChanged).AddTo(this);
        }

        private void OnDisable()
        {
            EnergyGridManager.Instance?.Unregister(this);
        }

        public event Action<bool> OnPowerStateChanged;

        private static void OnRequirementChanged(int _)
        {
            EnergyGridManager.Instance?.MarkDirty();
        }

        public void SetPoweredState(bool state)
        {
            if (IsPowered == state) return;
            IsPowered = state;
            OnPowerStateChanged?.Invoke(IsPowered);
        }
    }
}