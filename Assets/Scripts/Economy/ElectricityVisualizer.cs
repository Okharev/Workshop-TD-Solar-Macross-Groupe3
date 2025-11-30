using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    public class ElectricityVisualizer : MonoBehaviour
    {
        [Header("Configuration")] [Tooltip("If false, lines are hidden.")] [SerializeField]
        private bool showConnections = true;

        [Tooltip("Height offset so lines don't clip through the ground.")] [SerializeField]
        private float verticalOffset = 2.0f;

        [Header("Line Renderer Settings")] [SerializeField]
        private LineRenderer linePrefab;

        [Header("Dynamic Visuals")] [Tooltip("Width when providing 0% (or very low) of needs.")] [SerializeField]
        private float minWidth = 0.05f;

        [Tooltip("Width when providing 100% of needs.")] [SerializeField]
        private float maxWidth = 0.4f;

        [Tooltip("Color for low power transfer (e.g., dark/transparent).")] [SerializeField]
        private Color weakColor = new(0, 1, 1, 0.2f);

        [Tooltip("Color for high power transfer (e.g., bright/glowing).")] [SerializeField]
        private Color strongColor = new(0, 1, 1, 1.0f);

        // Object Pool
        private readonly List<LineRenderer> _linePool = new();

        // Tracking Data
        private readonly List<EnergyProducer> _registeredProducers = new();

        // Singleton Instance
        public static ElectricityVisualizer Instance { get; private set; }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void LateUpdate()
        {
            if (!showConnections) return;
            DrawConnections();
        }

        public void RegisterProducer(EnergyProducer producer)
        {
            if (!_registeredProducers.Contains(producer)) _registeredProducers.Add(producer);
        }

        public void UnregisterProducer(EnergyProducer producer)
        {
            if (_registeredProducers.Contains(producer)) _registeredProducers.Remove(producer);
        }

        public void ToggleVisibility(bool state)
        {
            showConnections = state;
            if (!showConnections) HideAllLines();
        }

        private void DrawConnections()
        {
            var lineIndex = 0;

            // Iterate through every producer 
            // We use a backwards loop or Copy if we were modifying, but here we just read.
            // However, we must handle the case where the list changes during iteration if threaded (unlikely here)
            foreach (var producer in _registeredProducers)
            {
                if (!producer || !producer.isActiveAndEnabled) continue;

                var outputs = producer.GetOutputMap();

                foreach (var (consumer, amountProvided) in outputs)
                {
                    var consumerMono = consumer as MonoBehaviour;

                    // If it is null (destroyed) OR it is simply disabled/inactive...
                    if (!consumerMono || !consumerMono.isActiveAndEnabled)
                        // Skip drawing this line. 
                        // The Logic in EnergyProducer should remove this entry shortly, 
                        // but the visualizer shouldn't wait for that.
                        continue;

                    float totalNeeded = consumer.GetEnergyRequirement();
                    var ratio = 0f;

                    if (totalNeeded > 0) ratio = Mathf.Clamp01(amountProvided / totalNeeded);

                    var line = GetLineFromPool(lineIndex);
                    lineIndex++;

                    var startPos = producer.GetPosition() + Vector3.up * verticalOffset;
                    var endPos = consumer.GetPosition() + Vector3.up * verticalOffset;

                    line.gameObject.SetActive(true);
                    line.SetPosition(0, startPos);
                    line.SetPosition(1, endPos);

                    UpdateLineVisuals(line, ratio);
                }
            }

            for (var i = lineIndex; i < _linePool.Count; i++) _linePool[i].gameObject.SetActive(false);
        }

        private void UpdateLineVisuals(LineRenderer line, float ratio)
        {
            var width = Mathf.Lerp(minWidth, maxWidth, ratio);
            line.startWidth = width;
            line.endWidth = width;

            var c = Color.Lerp(weakColor, strongColor, ratio);

            line.startColor = c;
            line.endColor = c;
        }

        private LineRenderer GetLineFromPool(int index)
        {
            if (index >= _linePool.Count)
            {
                var newLine = Instantiate(linePrefab, transform);
                newLine.gameObject.SetActive(false);
                _linePool.Add(newLine);
            }

            return _linePool[index];
        }

        private void HideAllLines()
        {
            foreach (var line in _linePool) line.gameObject.SetActive(false);
        }
    }
}