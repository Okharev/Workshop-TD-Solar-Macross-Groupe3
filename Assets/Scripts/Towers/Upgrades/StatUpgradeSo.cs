using System;
using System.Collections.Generic;
using UnityEngine;

namespace Towers.Upgrades
{
    // OCP : On étend le système sans modifier BaseTower ou UpgradeSo
    [CreateAssetMenu(menuName = "Upgrades/Stat Bundle")]
    public sealed class StatUpgradeSo : UpgradeSo
    {
        [Serializable]
        public struct StatConfig
        {
            public StatModType modifierType;
            public float value;
            public StatTarget targetStat; // Enum défini ci-dessous pour savoir quelle stat viser
        }

        public enum StatTarget { Damage, Range, FireRate }

        public List<StatConfig> modifiers = new List<StatConfig>();

        public override IUpgradeInstance CreateInstance(BaseTower tower)
        {
            return new StatUpgradeInstance(this, tower);
        }

        private class StatUpgradeInstance : IUpgradeInstance
        {
            private readonly StatUpgradeSo _config;
            private readonly BaseTower _tower;
            private readonly List<StatModifier> _appliedModifiers = new();

            public StatUpgradeInstance(StatUpgradeSo config, BaseTower tower)
            {
                _config = config;
                _tower = tower;
            }

            public void Enable()
            {
                foreach (var modConfig in _config.modifiers)
                {
                    // On choisit la stat cible sur la tour
                    Stat statToMod = modConfig.targetStat switch
                    {
                        StatTarget.Damage => _tower.damage,
                        StatTarget.Range => _tower.range,
                        StatTarget.FireRate => _tower.fireRate,
                        _ => null
                    };

                    if (statToMod != null)
                    {
                        // On crée le modificateur en utilisant cette instance comme "Source"
                        var mod = new StatModifier(modConfig.value, modConfig.modifierType, this);
                        statToMod.AddModifier(mod);
                        _appliedModifiers.Add(mod);
                    }
                }
            }

            public void Disable()
            {
                // Nettoyage propre (utile si on vend ou reset la tour)
                _tower.damage.RemoveAllModifiersFromSource(this);
                _tower.range.RemoveAllModifiersFromSource(this);
                _tower.fireRate.RemoveAllModifiersFromSource(this);
                _appliedModifiers.Clear();
            }
        }
    }
}