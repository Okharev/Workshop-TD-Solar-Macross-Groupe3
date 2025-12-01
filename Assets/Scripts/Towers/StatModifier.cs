using System;
using System.Linq;
using UnityEngine;
using R3;
using ObservableCollections; // Required for ObservableList

namespace Towers
{
    public enum StatModType
    {
        Flat = 100,
        PercentAdd = 200,
        PercentMult = 300
    }

    [Serializable]
    public class StatModifier
    {
        public readonly float Value;
        public readonly StatModType Type;
        public readonly object Source;

        public StatModifier(float value, StatModType type, object source = null)
        {
            this.Value = value;
            this.Type = type;
            this.Source = source;
        }
    }

    [Serializable]
    public class ReactiveStat : IDisposable
    {
        // 1. Base Value (Editable in Inspector if wrapper used, otherwise set in code)
        [SerializeField] private SerializableReactiveProperty<float> baseValue;

        // 2. The High-Performance Observable Collection
        // We use ObservableList from Cysharp/ObservableCollections
        public ObservableList<StatModifier> Modifiers { get; } = new();

        // 3. The Output Stream
        public ReadOnlyReactiveProperty<float> Value { get; private set; }

        private CompositeDisposable _disposables = new();

        public ReactiveStat(float initialBaseValue = 0)
        {
            baseValue = new SerializableReactiveProperty<float>(initialBaseValue);
        }

        public void Initialize()
        {
            _disposables.Dispose(); 
            _disposables = new CompositeDisposable();

            // Monitor Base Value
            var baseStream = baseValue.AsUnitObservable();

            // Monitor Collection Changes using ObservableCollections.R3 extensions
            // We merge all relevant events into a single "Something Changed" signal
            var listStream = Observable.Merge(
                Modifiers.ObserveAdd().AsUnitObservable(),
                Modifiers.ObserveRemove().AsUnitObservable(),
                Modifiers.ObserveReplace().AsUnitObservable(),
                Modifiers.ObserveReset().AsUnitObservable()
            );

            // Merge triggers -> Recalculate -> Cache result
            Value = Observable.Merge(baseStream, listStream)
                .Select(_ => CalculateFinalValue())
                .ToReadOnlyReactiveProperty(CalculateFinalValue())
                .AddTo(_disposables);
        }

        // --- Standard Accessors ---

        public float BaseValue
        {
            get => baseValue.Value;
            set => baseValue.Value = value;
        }

        public void AddModifier(StatModifier mod) => Modifiers.Add(mod);

        public bool RemoveModifier(StatModifier mod) => Modifiers.Remove(mod);

        public void RemoveAllModifiersFromSource(object source)
        {
            // Iterate backwards to safely remove
            for (int i = Modifiers.Count - 1; i >= 0; i--)
            {
                if (Modifiers[i].Source == source)
                {
                    Modifiers.RemoveAt(i);
                }
            }
        }

        private float CalculateFinalValue()
        {
            float finalValue = baseValue.Value;
            float sumPercentAdd = 0;

            // Direct iteration over ObservableList is zero-allocation and fast
            foreach (var mod in Modifiers)
            {
                switch (mod.Type)
                {
                    case StatModType.Flat:
                        finalValue += mod.Value;
                        break;
                    case StatModType.PercentAdd:
                        sumPercentAdd += mod.Value;
                        break;
                }
            }

            finalValue *= 1 + sumPercentAdd;

            foreach (var mod in Modifiers)
            {
                if (mod.Type == StatModType.PercentMult)
                    finalValue *= mod.Value;
            }

            return (float)Math.Round(finalValue, 4);
        }

        public void Dispose()
        {
            baseValue?.Dispose();
            _disposables?.Dispose();
            // ObservableList does not strictly require Dispose unless you use Views, 
            // but clearing it handles references.
            Modifiers.Clear(); 
        }
    }
}