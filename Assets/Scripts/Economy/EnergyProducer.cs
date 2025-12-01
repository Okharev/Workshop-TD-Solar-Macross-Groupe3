using UnityEngine;

namespace Economy
{
    public class EnergyProducer : MonoBehaviour
    {
        [Header("Configuration")] public bool isMobileGenerator = true;

        // Reactive Properties
        [SerializeField] private ReactiveInt maxCapacity = new(100);
        [SerializeField] private ReactiveFloat broadcastRadius = new(15f);

        // Internal
        private Vector3 _lastPos;
        private SphereCollider _rangeCollider; // Reference to update size dynamically

        public ReactiveInt MaxCapacity => maxCapacity;
        public ReactiveFloat BroadcastRadius => broadcastRadius;

        // Runtime State (Read Only for public, Writable by Manager)
        public int CurrentLoad { get; private set; }

        private void Start()
        {
            // Physics/Manager registration setup
            _lastPos = transform.position;
            if (isMobileGenerator) GenerateRangeTrigger();
            // Registering with Manager is handled in OnEnable now
        }

        private void Update()
        {
            // Detect movement to dirty the grid
            if ((transform.position - _lastPos).sqrMagnitude > 0.01f)
            {
                _lastPos = transform.position;
                EnergyGridManager.Instance.MarkDirty();
            }
        }

        private void OnEnable()
        {
            EnergyGridManager.Instance?.Register(this);

            BroadcastRadius.Subscribe(OnRadiusChanged).AddTo(this);
            MaxCapacity.Subscribe(OnStatsChanged_Int).AddTo(this);
        }

        private void OnDisable()
        {
            EnergyGridManager.Instance?.Unregister(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 1, 0.4f);
            Gizmos.DrawWireSphere(transform.position, BroadcastRadius.Value);
        }

        // Named methods make debugging easier than Lambdas
        private void OnStatsChanged_Int(int _)
        {
            EnergyGridManager.Instance?.MarkDirty();
        }

        private void OnRadiusChanged(float newRadius)
        {
            UpdateRangeCollider(newRadius);
            EnergyGridManager.Instance?.MarkDirty();
        }

        // --- Logic Helper ---
        public int GetAvailable()
        {
            return Mathf.Max(0, MaxCapacity.Value - CurrentLoad);
        }

        // --- Manager Interface ---
        public void ResetLoad()
        {
            CurrentLoad = 0;
        }

        public void AddLoad(int amount)
        {
            CurrentLoad += amount;
        }

        public void RemoveLoad(int amount)
        {
            CurrentLoad -= amount;
        }

        // --- Physics / Placement Helpers ---
        private void GenerateRangeTrigger()
        {
            // Check if it already exists (e.g. from prefab)
            var existing = transform.Find("EnergyField_Generated");
            if (existing)
            {
                _rangeCollider = existing.GetComponent<SphereCollider>();
                return;
            }

            var fieldObj = new GameObject("EnergyField_Generated");
            fieldObj.transform.SetParent(transform);
            fieldObj.transform.localPosition = Vector3.zero;

            // Set Layer to "PowerGrid" or fallback to Default
            var powerLayer = LayerMask.NameToLayer("PowerGrid");
            if (powerLayer != -1) fieldObj.layer = powerLayer;

            _rangeCollider = fieldObj.AddComponent<SphereCollider>();
            _rangeCollider.isTrigger = true;
            _rangeCollider.radius = BroadcastRadius.Value;

            // Add the Link script so Raycasts know who owns this trigger
            var link = fieldObj.AddComponent<EnergyFieldLink>();
            link.Initialize(this);
        }

        private void UpdateRangeCollider(float newRadius)
        {
            if (_rangeCollider != null)
                _rangeCollider.radius = newRadius;
        }
    }
}