using System;
using System.Collections.Generic;
using Economy;
using UnityEngine;
// Nécessaire pour accéder à EnergyConsumer

namespace Towers.Upgrades
{
    // OCP : On étend le système pour inclure l'énergie sans casser l'existant
    [CreateAssetMenu(menuName = "Upgrades/Stat Bundle")]
    public sealed class StatUpgradeSo : UpgradeSo
    {
        // On ajoute EnergyConsumption à la liste des cibles
        public enum StatTarget
        {
            Damage,
            Range,
            FireRate,
            EnergyConsumption
        }

        public List<StatConfig> modifiers = new();

        public override IUpgradeInstance CreateInstance(BaseTower tower)
        {
            return new StatUpgradeInstance(this, tower);
        }

        [Serializable]
        public struct StatConfig
        {
            public StatModType modifierType;
            public float value;
            public StatTarget targetStat;
        }

        private class StatUpgradeInstance : IUpgradeInstance
        {
            // On garde une trace pour le debug, même si on utilise RemoveAllModifiersFromSource pour le nettoyage
            private readonly List<StatModifier> _appliedModifiers = new();
            private readonly StatUpgradeSo _config;
            private readonly BaseTower _tower;

            public StatUpgradeInstance(StatUpgradeSo config, BaseTower tower)
            {
                _config = config;
                _tower = tower;
            }

            public void Enable()
            {
                foreach (var modConfig in _config.modifiers)
                {
                    // CAS SPECIAL : Consommation d'énergie (StatInt sur un autre composant)
                    if (modConfig.targetStat == StatTarget.EnergyConsumption)
                    {
                        var consumer = _tower.GetComponent<EnergyConsumer>();
                        if (consumer != null)
                        {
                            var mod = new StatModifier(modConfig.value, modConfig.modifierType, this);
                            consumer.totalRequirement.AddModifier(mod);
                            _appliedModifiers.Add(mod);
                        }

                        continue; // On passe au modifier suivant
                    }

                    // CAS STANDARD : Stats de la tour (StatFloat)
                    var statToMod = modConfig.targetStat switch
                    {
                        StatTarget.Damage => _tower.damage,
                        StatTarget.Range => _tower.range,
                        StatTarget.FireRate => _tower.fireRate,
                        _ => null
                    };

                    if (statToMod != null)
                    {
                        var mod = new StatModifier(modConfig.value, modConfig.modifierType, this);
                        statToMod.AddModifier(mod);
                        _appliedModifiers.Add(mod);
                    }
                }
            }

            public void Disable()
            {
                // 1. Nettoyage des stats de la tour
                _tower.damage.RemoveAllModifiersFromSource(this);
                _tower.range.RemoveAllModifiersFromSource(this);
                _tower.fireRate.RemoveAllModifiersFromSource(this);

                // 2. Nettoyage de la consommation d'énergie
                var consumer = _tower.GetComponent<EnergyConsumer>();
                if (consumer != null) consumer.totalRequirement.RemoveAllModifiersFromSource(this);

                _appliedModifiers.Clear();
            }
        }
    }
}