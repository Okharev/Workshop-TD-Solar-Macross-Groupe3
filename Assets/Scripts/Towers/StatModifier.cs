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
        [SerializeField] private ReactiveFloat _baseValue;

        // On garde _value privé ou protégé
        [SerializeField] private ReactiveFloat _value = new(0);
        private readonly List<StatModifier> _modifiers = new();

        public Stat(float initialBaseValue = 0)
        {
            _baseValue = new ReactiveFloat(initialBaseValue);
            _value = new ReactiveFloat(initialBaseValue);
            Initialize();
        }

        // --- CORRECTION SYNTAXE ---

        // 1. Accès direct : permet d'écrire "myStat.Value" (float)
        public float Value => _value.Value;

        // 3. On expose l'observable pour ceux qui veulent s'abonner aux changements
        public IReadOnlyReactiveProperty<float> Observable => _value;

        // (Le reste de tes méthodes BaseValue, AddModifier, Recalculate restent identiques...)

        public float BaseValue
        {
            get => _baseValue.Value;
            set => _baseValue.Value = value;
        }

        // 2. Opérateur Implicite : permet d'écrire "float x = myStat;" ou "if(myStat > 10)"
        public static implicit operator float(Stat s)
        {
            return s.Value;
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
            // The ReactiveProperty internal check ensures OnValueChanged only fires if the result is actually different
            _value.Value = (float)Math.Round(finalValue, 4);
        }
    }
}