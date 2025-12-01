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
    public class StatModifier
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
    public class Stat
    {
        // 1. Base Value: Editable in Inspector, triggers updates via ReactivePropertyDrawer
        [SerializeField] private ReactiveFloat _baseValue;

        // 2. Modifiers: Standard list, accessed via methods to ensure dirty flagging
        private readonly List<StatModifier> _modifiers = new();

        // 3. Output: Private ReactiveFloat to handle caching and event firing
        // We do not Serialize this, as it is a runtime computed value
        private ReactiveFloat _value = new(0);

        public Stat(float initialBaseValue = 0)
        {
            _baseValue = new ReactiveFloat(initialBaseValue);
            _value = new ReactiveFloat(initialBaseValue);
            Initialize();
        }

        // --- Public API ---

        // Public Read-Only access to the calculated value
        public float Value => _value.Value;

        public float BaseValue
        {
            get => _baseValue.Value;
            set => _baseValue.Value = value;
        }

        // Event for when the FINAL calculated value changes
        public event Action<float> OnValueChanged
        {
            add => _value.OnValueChanged += value;
            remove => _value.OnValueChanged -= value;
        }

        /// <summary>
        ///     Call this in Awake() or Start() of the owning MonoBehaviour.
        ///     Ensures internal listeners are hooked up.
        /// </summary>
        public void Initialize()
        {
            // Ensure we don't double subscribe if Initialize is called multiple times
            _baseValue.OnValueChanged -= OnBaseValueChanged;
            _baseValue.OnValueChanged += OnBaseValueChanged;

            // Initial Calculation
            Recalculate();
        }

        private void OnBaseValueChanged(float newVal)
        {
            Recalculate();
        }

        // --- Modifier Management ---

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

        // --- Calculation Logic ---

        private void Recalculate()
        {
            var finalValue = _baseValue.Value;
            float sumPercentAdd = 0;
            var totalPercentMult = 1f;

            // Iterate list once? Or multiple times? 
            // Splitting loops usually easier to read and statistically insignificant performance hit here.

            // 1. Flat & Percent Add
            for (var i = 0; i < _modifiers.Count; i++)
            {
                var mod = _modifiers[i];
                if (mod.Type == StatModType.Flat)
                    finalValue += mod.Value;
                else if (mod.Type == StatModType.PercentAdd)
                    sumPercentAdd += mod.Value;
                else if (mod.Type == StatModType.PercentMult) totalPercentMult *= mod.Value;
            }

            // 2. Apply Percent Add
            finalValue *= 1 + sumPercentAdd;

            // 3. Apply Percent Mult
            finalValue *= totalPercentMult;

            // 4. Update the Output ReactiveProperty
            // The ReactiveProperty internal check ensures OnValueChanged only fires if the result is actually different
            _value.Value = (float)Math.Round(finalValue, 4);
        }
    }
}