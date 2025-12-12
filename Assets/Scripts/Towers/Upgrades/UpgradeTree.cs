using System;
using System.Collections.Generic;
using UnityEngine;

namespace Towers
{
    [CreateAssetMenu(menuName = "Towers/Upgrade Tree")]
    public class TowerUpgradeTreeSo : ScriptableObject
    {
        // Liste des étapes (Tier 1, Tier 2, etc.)
        public List<UpgradeTier> tiers = new List<UpgradeTier>();
    }

    [Serializable]
    public class UpgradeTier
    {
        public string tierName; // Ex: "Module Tactique"
        
        // Liste des choix possibles pour ce niveau.
        // Si la liste a 1 élément = Amélioration linéaire (ex: Lvl 2).
        // Si la liste a >1 éléments = Choix (ex: Lvl 3).
        public List<UpgradeOption> options = new List<UpgradeOption>();
    }

    [Serializable]
    public class UpgradeOption
    {
        public string labelOverride; // Optionnel, sinon prend le nom de l'upgrade
        public int cost;
        public UpgradeSo upgradeDefinition; // Le SO (Stat ou Effet)
    }
}