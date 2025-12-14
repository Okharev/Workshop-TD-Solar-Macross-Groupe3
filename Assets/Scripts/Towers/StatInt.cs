using System;
using System.Collections.Generic;
using UnityEngine;

namespace Towers
{
    [Serializable]
    public sealed class StatInt
    {
        // La valeur que tu configures (Base)
        [Tooltip("Valeur de base (sans upgrades)")] [SerializeField]
        private int _baseValue;

        [Header("Debug Info")] [Tooltip("Valeur finale actuelle (Lecture seule)")] [SerializeField]
        private int _currentValueDisplay;

        [SerializeReference] private List<StatModifier> _modifiers = new();

        private readonly ReactiveInt _value = new(0);

        public StatInt(int initialBaseValue = 0)
        {
            _baseValue = initialBaseValue;
            _value.Value = initialBaseValue;
            Recalculate();
        }

        public int Value => _value.Value;

        public int BaseValue
        {
            get => _baseValue;
            set
            {
                _baseValue = value;
                Recalculate();
            }
        }

        public IReadOnlyReactiveProperty<int> Observable => _value;

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

        /// <summary>
        ///     Retire tous les modificateurs liés à une source spécifique (ex: un Upgrade précis).
        /// </summary>
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

        private void Recalculate()
        {
            float finalValue = _baseValue;
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

            finalValue *= 1 + sumPercentAdd;
            finalValue *= totalPercentMult;

            var result = Mathf.RoundToInt(finalValue);

            _value.Value = result;
            _currentValueDisplay = result;
        }

        public static implicit operator int(StatInt s)
        {
            return s.Value;
        }
    }
}