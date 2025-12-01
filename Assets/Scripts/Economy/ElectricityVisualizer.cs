using System.Collections.Generic;
using ObservableCollections;
using R3;
using R3.Collections;
using UnityEngine;

namespace Economy
{
    public class ElectricityVisualizer : MonoBehaviour
    {
        [SerializeField] private LineRenderer _linePrefab;
        [SerializeField] private float _verticalOffset = 2.0f;
        
        // Singleton
        public static ElectricityVisualizer Instance { get; private set; }

        private readonly ObservableList<IReactiveEnergyProducer> _producers = new();
        
        // Map a specific connection (Producer -> Consumer) to a LineRenderer
        private readonly Dictionary<(IReactiveEnergyProducer, IReactiveEnergyConsumer), LineRenderer> _activeLines = new();
        private readonly Queue<LineRenderer> _linePool = new();

        private void Awake()
        {
            Instance = this;

            // Reactive Logic: When a producer is added...
            _producers.ObserveAdd()
                .Subscribe(e =>
                {
                    // ... Subscribe to THAT producer's connection list
                    // We use a CompositeDisposable to clean up this inner subscription if the producer is removed
                    var disposable = new CompositeDisposable();
                    
                    // Listen to connections added
                    e.Value.ActiveConnections.ObserveAdd()
                        .Subscribe(conn => CreateLine(e.Value, conn.Value.consumer, conn.Value.amount))
                        .AddTo(disposable);

                    // Listen to connections removed
                    e.Value.ActiveConnections.ObserveRemove()
                        .Subscribe(conn => RemoveLine(e.Value, conn.Value.consumer))
                        .AddTo(disposable);
                    
                    // Listen to connections updated (amount change) - visual width update
                    e.Value.ActiveConnections.ObserveReplace()
                        .Subscribe(conn => UpdateLineVisuals(e.Value, conn.NewValue.consumer, conn.NewValue.amount))
                        .AddTo(disposable);

                    // If producer is removed from global list, dispose these listeners
                    _producers.ObserveRemove()
                        .Where(x => x.Value == e.Value)
                        .Subscribe(_ => disposable.Dispose())
                        .AddTo(this);
                })
                .AddTo(this);
        }

        private void LateUpdate()
        {
            // Only update positions of active lines
            foreach (var kvp in _activeLines)
            {
                var (producer, consumer) = kvp.Key;
                var line = kvp.Value;

                // Safety check
                if (producer == null || consumer == null || line == null) continue;

                line.SetPosition(0, producer.Position + Vector3.up * _verticalOffset);
                line.SetPosition(1, consumer.Position + Vector3.up * _verticalOffset);
            }
        }

        public void RegisterProducer(EnergyProducer p) => _producers.Add(p);
        public void UnregisterProducer(EnergyProducer p) => _producers.Remove(p);

        private void CreateLine(IReactiveEnergyProducer p, IReactiveEnergyConsumer c, int amount)
        {
            var key = (p, c);
            if (_activeLines.ContainsKey(key)) return;

            LineRenderer line;
            if (_linePool.Count > 0)
            {
                line = _linePool.Dequeue();
                line.gameObject.SetActive(true);
            }
            else
            {
                line = Instantiate(_linePrefab, transform);
            }

            _activeLines.Add(key, line);
            UpdateLineVisuals(p, c, amount);
        }

        private void RemoveLine(IReactiveEnergyProducer p, IReactiveEnergyConsumer c)
        {
            var key = (p, c);
            if (_activeLines.TryGetValue(key, out var line))
            {
                line.gameObject.SetActive(false);
                _linePool.Enqueue(line);
                _activeLines.Remove(key);
            }
        }

        private void UpdateLineVisuals(IReactiveEnergyProducer p, IReactiveEnergyConsumer c, int amount)
        {
            var key = (p, c);
            if (_activeLines.TryGetValue(key, out var line))
            {
                float ratio = Mathf.Clamp01((float)amount / c.EnergyRequirement.Value);
                float width = Mathf.Lerp(0.05f, 0.4f, ratio);
                line.startWidth = width;
                line.endWidth = width;
                
                // You could also animate colors here based on ratio
            }
        }
    }
}