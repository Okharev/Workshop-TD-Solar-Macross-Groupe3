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
    public class Stat
    {
        [SerializeField] private float baseValue;

        [SerializeField] private bool isDirty = true;

        [SerializeField] private float value;

        [SerializeField] private List<StatModifier> modifiers = new(4);

        public Stat()
        {
        }

        public Stat(float baseValue)
        {
            this.baseValue = baseValue;
            isDirty = true;
        }

        public float Value
        {
            get
            {
                if (!isDirty && (value != 0 || baseValue == 0)) return value;

                value = CalculateFinalValue();
                isDirty = false;
                return value;
            }
        }

        public float BaseValue
        {
            get => baseValue;
            set
            {
                baseValue = value;
                SetDirty();
            }
        }

        public void AddModifier(StatModifier mod)
        {
            modifiers ??= new List<StatModifier>();

            isDirty = true;
            modifiers.Add(mod);
        }

        public bool RemoveModifier(StatModifier mod)
        {
            if (modifiers == null) return false;

            if (!modifiers.Remove(mod)) return false;

            isDirty = true;
            return true;
        }

        public void RemoveAllModifiersFromSource(object source)
        {
            if (modifiers == null) return;

            if (modifiers.RemoveAll(mod => mod.source == source) > 0) isDirty = true;
        }

        private void SetDirty()
        {
            isDirty = true;
        }

        private float CalculateFinalValue()
        {
            modifiers ??= new List<StatModifier>();

            var finalValue = baseValue;
            float sumPercentAdd = 0;

            foreach (var mod in modifiers)
                switch (mod.type)
                {
                    case StatModType.Flat:
                        finalValue += mod.value;
                        break;
                    case StatModType.PercentAdd:
                        sumPercentAdd += mod.value;
                        break;
                }

            finalValue *= 1 + sumPercentAdd;

            foreach (var mod in modifiers)
                if (mod.type == StatModType.PercentMult)
                    finalValue *= mod.value;

            return (float)Math.Round(finalValue, 4);
        }
    }

    [Serializable]
    public class StatModifier
    {
        [SerializeField] public float value;
        [SerializeField] public StatModType type;
        public object source;

        public StatModifier(float value, StatModType type, object source = null)
        {
            this.value = value;
            this.type = type;
            this.source = source;
        }
    }
}