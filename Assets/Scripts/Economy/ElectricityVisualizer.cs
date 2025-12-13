using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    public sealed class ElectricityVisualizer : MonoBehaviour
    {
        [SerializeField] private LineRenderer linePrefab;
        [SerializeField] private float verticalOffset = 2.0f;
        [SerializeField] private float minWidth = 0.05f;
        [SerializeField] private float maxWidth = 0.3f;

        private readonly List<LineRenderer> _linePool = new();
        private readonly List<LineRenderer> _previewLinePool = new();
        
        private void Start()
        {
            if (EnergyGridManager.Instance)
                EnergyGridManager.Instance.OnGridResolved += RefreshVisuals;
        }

        private void OnDestroy()
        {
            if (EnergyGridManager.Instance)
                EnergyGridManager.Instance.OnGridResolved -= RefreshVisuals;
        }

        private void RefreshVisuals()
        {
            HideAllLines();

            var graph = EnergyGridManager.Instance.ConnectionGraph;
            var lineIndex = 0;

            // Iterate: Consumer -> [Producers]
            foreach (var (consumer, sources) in graph)
            {
                // Safety check if object was destroyed but graph not yet rebuilt (rare race condition)
                if (!consumer) continue;

                var totalReq = consumer.totalRequirement.Value;

                foreach (var (producer, amountProvided) in sources)
                {
                    if (!producer) continue;

                    // Calculate thickness based on contribution percentage
                    var contributionRatio = totalReq > 0 ? (float)amountProvided / totalReq : 0;

                    DrawLine(lineIndex++, producer.transform.position, consumer.transform.position, contributionRatio);
                }
            }
        }
        
        public void PreviewConnections(Vector3 start, List<Vector3> targets)
        {
            // 1. On cache tout d'abord
            foreach (var line in _previewLinePool) line.gameObject.SetActive(false);

            // 2. On affiche les nouvelles
            for (int i = 0; i < targets.Count; i++)
            {
                // Agrandissement du pool si nécessaire
                if (i >= _previewLinePool.Count)
                {
                    var newItem = Instantiate(linePrefab, transform);
                    newItem.name = $"PreviewLine_{i}";
                    _previewLinePool.Add(newItem);
                }

                var line = _previewLinePool[i];
                line.gameObject.SetActive(true);
        
                // Configuration visuelle (peut-être plus fine ou transparente pour le preview)
                line.SetPosition(0, start + Vector3.up * verticalOffset);
                line.SetPosition(1, targets[i] + Vector3.up * verticalOffset);
                line.startWidth = minWidth;
                line.endWidth = minWidth;
        
                // Optionnel : Changer la couleur du matériau pour indiquer "Preview"
                // line.material.color = Color.cyan; 
            }
        }

        public void ClearPreview()
        {
            foreach (var line in _previewLinePool)
            {
                line.gameObject.SetActive(false);
            }
        }

        private void DrawLine(int index, Vector3 start, Vector3 end, float ratio)
        {
            if (index >= _linePool.Count)
            {
                var newItem = Instantiate(linePrefab, transform);
                _linePool.Add(newItem);
            }

            var line = _linePool[index];
            line.gameObject.SetActive(true);

            var offset = Vector3.up * verticalOffset;
            line.SetPosition(0, start + offset);
            line.SetPosition(1, end + offset);

            var width = Mathf.Lerp(minWidth, maxWidth, ratio);
            line.startWidth = width;
            line.endWidth = width;
        }

        private void HideAllLines()
        {
            foreach (var line in _linePool) line.gameObject.SetActive(false);
        }
    }
}