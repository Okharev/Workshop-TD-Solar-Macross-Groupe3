using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    public class ElectricityVisualizer : MonoBehaviour
    {
        [SerializeField] private LineRenderer linePrefab;
        [SerializeField] private float verticalOffset = 2.0f;
        [SerializeField] private float minWidth = 0.05f;
        [SerializeField] private float maxWidth = 0.3f;

        private readonly List<LineRenderer> _linePool = new();

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

                var totalReq = consumer.TotalRequirement.Value;

                foreach (var (producer, amountProvided) in sources)
                {
                    if (!producer) continue;

                    // Calculate thickness based on contribution percentage
                    var contributionRatio = totalReq > 0 ? (float)amountProvided / totalReq : 0;

                    DrawLine(lineIndex++, producer.transform.position, consumer.transform.position, contributionRatio);
                }
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