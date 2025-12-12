using System;
using System.Collections.Generic;
using UnityEngine;

namespace Towers
{
    [Serializable]
    public sealed class StatInt
    {
        // La valeur que tu configures (Base)
        [Tooltip("Valeur de base (sans upgrades)")]
        [SerializeField] private int _baseValue;
        
        // --- NOUVEAU : Champ de visualisation ---
        // On utilise [SerializeField] pour le voir dans l'inspecteur
        // On le garde privé pour ne pas qu'on puisse l'utiliser par erreur dans le code
        [Header("Debug Info")]
        [Tooltip("Valeur finale actuelle (Lecture seule)")]
        [SerializeField] private int _currentValueDisplay;
        // ----------------------------------------

        private readonly ReactiveInt _value = new(0);
        
        [SerializeReference] // Permet de voir la liste des modificateurs dans l'inspecteur (optionnel mais utile)
        private List<StatModifier> _modifiers = new();

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

        private void Recalculate()
        {
            float finalValue = _baseValue;
            float sumPercentAdd = 0;
            float totalPercentMult = 1f;

            foreach (var mod in _modifiers)
            {
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
            }

            finalValue *= 1 + sumPercentAdd;
            finalValue *= totalPercentMult;

            int result = Mathf.RoundToInt(finalValue);
            
            _value.Value = result;
            
            // --- MISE À JOUR VISUELLE ---
            _currentValueDisplay = result;
            // ----------------------------
        }
        
        public static implicit operator int(StatInt s) => s.Value;
    }
}