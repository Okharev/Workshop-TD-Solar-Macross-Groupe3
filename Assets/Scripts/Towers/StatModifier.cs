using System;
using System.Collections.Generic;
using UnityEngine;

namespace Towers
{
    public enum StatModType
    {
        Flat = 100,
        PercentAdd = 200,
        PercentMult = 300
    }

    [Serializable]
    public sealed class StatModifier
    {
        public readonly object Source;
        public readonly StatModType Type;
        public readonly float Value;

        public StatModifier(float value, StatModType type, object source = null)
        {
            Value = value;
            Type = type;
            Source = source;
        }
    }

    [Serializable]
    public sealed class Stat
    {
        [SerializeField] private ReactiveFloat _baseValue;

        [SerializeField] private ReactiveFloat _value = new(0);
        private readonly List<StatModifier> _modifiers = new();

        public Stat(float initialBaseValue = 0)
        {
            _baseValue = new ReactiveFloat(initialBaseValue);
            _value = new ReactiveFloat(initialBaseValue);
            Initialize();
        }

        public float Value => _value.Value;

        public IReadOnlyReactiveProperty<float> Observable => _value;


        public float BaseValue
        {
            get => _baseValue.Value;
            set => _baseValue.Value = value;
        }

        public static implicit operator float(Stat s)
        {
            return s.Value;
        }

        public event Action<float> OnValueChanged
        {
            add => _value.OnValueChanged += value;
            remove => _value.OnValueChanged -= value;
        }


        public void Initialize()
        {
            _baseValue.OnValueChanged -= OnBaseValueChanged;
            _baseValue.OnValueChanged += OnBaseValueChanged;

            Recalculate();
        }

        private void OnBaseValueChanged(float newVal)
        {
            Recalculate();
        }


        public void AddModifier(StatModifier mod)
        {
            _modifiers.Add(mod);
            Recalculate();
        }

        public bool RemoveModifier(StatModifier mod)
        {
            var removed = _modifiers.Remove(mod);
            if (removed) Recalculate();
            return removed;
        }

        public void RemoveAllModifiersFromSource(object source)
        {
            var changed = false;
            for (var i = _modifiers.Count - 1; i >= 0; i--)
                if (_modifiers[i].Source == source)
                {
                    _modifiers.RemoveAt(i);
                    changed = true;
                }

            if (changed) Recalculate();
        }

        public void ClearModifiers()
        {
            if (_modifiers.Count > 0)
            {
                _modifiers.Clear();
                Recalculate();
            }
        }


        private void Recalculate()
        {
            var finalValue = _baseValue.Value;
            float sumPercentAdd = 0;
            var totalPercentMult = 1f;


            foreach (var mod in _modifiers)
                switch (mod.Type)
                {
                    case StatModType.Flat:
                        finalValue += mod.Value;
                        break;
                    case StatModType.PercentAdd:
                        sumPercentAdd += mod.Value;
                        break;
                    case StatModType.PercentMult:
                        totalPercentMult *= mod.Value;
                        break;
                }

            // 2. Apply Percent Add
            finalValue *= 1 + sumPercentAdd;

            // 3. Apply Percent Mult
            finalValue *= totalPercentMult;

            // 4. Update the Output ReactiveProperty
            _value.Value = (float)Math.Round(finalValue, 4);
        }
    }
}