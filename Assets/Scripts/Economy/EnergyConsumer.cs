using System;
using UnityEngine;

namespace Economy
{
    public enum EnergyPriority
    {
        Critical = 100,
        Standard = 50,
        Low = 10,
        Background = 0
    }

    public class EnergyConsumer : MonoBehaviour
    {
        public event Action<bool> OnPowerStateChanged;

        
        [Header("Settings")] [SerializeField] private EnergyPriority priority = EnergyPriority.Standard;

        [SerializeField] private ReactiveInt totalRequirement = new(100);

        private Vector3 _lastPos;
        public IReadOnlyReactiveProperty<int> TotalRequirement => totalRequirement;
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

        private void OnEnable()
        {
            EnergyGridManager.Instance?.Register(this);
            totalRequirement.Subscribe(OnRequirementChanged).AddTo(this);
        }

        private void OnDisable()
        {
            EnergyGridManager.Instance?.Unregister(this);
        }
        
        private void OnRequirementChanged(int _)
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