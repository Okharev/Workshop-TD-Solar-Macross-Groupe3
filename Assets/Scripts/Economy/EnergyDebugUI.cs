using ObservableCollections;
using R3;
using R3.Collections;
using UnityEngine;

namespace Economy
{
    public class EnergyDebugUI : MonoBehaviour
    {
        [Header("Settings")] 
        public bool showDebug = true;
        public KeyCode toggleKey = KeyCode.F3;
        public float verticalOffset = 2.5f;

        // Singleton for easy access
        public static EnergyDebugUI Instance { get; private set; }

        private readonly ObservableList<IReactiveEnergyProducer> _producers = new();
        private readonly ObservableList<IReactiveEnergyConsumer> _consumers = new();
        
        private UnityEngine.Camera _mainCam;
        private GUIStyle _styleConsumer;
        private GUIStyle _styleProducer;
        private CompositeDisposable _disposables = new();

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            
            _mainCam = UnityEngine.Camera.main;
            
            // Toggle Logic using R3
            Observable.EveryUpdate()
                .Where(_ => Input.GetKeyDown(toggleKey))
                .Subscribe(_ => showDebug = !showDebug)
                .AddTo(_disposables);
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }

        // Registration Methods called by entities
        public void RegisterProducer(IReactiveEnergyProducer p) => _producers.Add(p);
        public void UnregisterProducer(IReactiveEnergyProducer p) => _producers.Remove(p);
        public void RegisterConsumer(IReactiveEnergyConsumer c) => _consumers.Add(c);
        public void UnregisterConsumer(IReactiveEnergyConsumer c) => _consumers.Remove(c);

        private void OnGUI()
        {
            if (!showDebug || _mainCam == null) return;
            if (_styleProducer == null) SetupStyles();

            DrawProducers();
            DrawConsumers();
        }

        private void DrawProducers()
        {
            // Iterate ObservableList directly
            foreach (var p in _producers)
            {
                if (p == null || p.Equals(null)) continue;
                if (p is MonoBehaviour mono && !mono.isActiveAndEnabled) continue;

                var screenPos = GetScreenPosition(p.Position);
                if (screenPos.z < 0) continue;

                // Reactive Access: Get CurrentValue directly
                var current = p.CurrentLoad.CurrentValue;
                var total = p.MaxCapacity.CurrentValue;
                var available = p.AvailableEnergy.CurrentValue;

                var text = p.IsLocalGenerator ? "GENERATOR" : "DISTRICT";
                text += $"\nLoad: {current}/{total}";

                GUI.color = available <= 0 ? Color.red : Color.cyan;
                DrawLabel(screenPos, text, _styleProducer);
            }
        }

        private void DrawConsumers()
        {
            foreach (var c in _consumers)
            {
                if (c == null || c.Equals(null)) continue;
                if (c is MonoBehaviour mono && !mono.isActiveAndEnabled) continue;

                var screenPos = GetScreenPosition(c.Position);
                if (screenPos.z < 0) continue;

                var isPowered = c.IsPowered.CurrentValue;
                var req = c.EnergyRequirement.CurrentValue;

                var text = isPowered ? "POWERED" : "NO POWER";
                text += $"\nReq: {req}";

                GUI.color = isPowered ? Color.green : Color.red;
                DrawLabel(screenPos, text, _styleConsumer);
            }
        }

        private void DrawLabel(Vector3 screenPos, string text, GUIStyle style)
        {
            var width = 100f;
            var height = 50f;
            // Invert Y for GUI coordinates
            var rect = new Rect(screenPos.x - width / 2, Screen.height - screenPos.y - height / 2, width, height);

            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, text, style);
        }

        private Vector3 GetScreenPosition(Vector3 worldPos)
        {
            return _mainCam.WorldToScreenPoint(worldPos + Vector3.up * verticalOffset);
        }

        private void SetupStyles()
        {
            _styleProducer = new GUIStyle(GUI.skin.label);
            _styleProducer.alignment = TextAnchor.MiddleCenter;
            _styleProducer.fontStyle = FontStyle.Bold;
            _styleProducer.fontSize = 12;

            _styleConsumer = new GUIStyle(_styleProducer);
        }
    }
}