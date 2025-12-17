using System.Collections.Generic;
using Economy;
using UnityEngine;
// Assure-toi d'avoir accès au CurrencyManager

namespace Towers
{
    [RequireComponent(typeof(BaseTower))]
    public class TowerLevelingManager : MonoBehaviour
    {
        [Header("Configuration")] [SerializeField]
        private TowerUpgradeTreeSo upgradeTree;

        // Stocke les instances actives pour pouvoir les désactiver si besoin
        private readonly List<IUpgradeInstance> _activeInstances = new();
        private int _currentTierIndex; // 0 = Base, 1 = Premier Upgrade acquis, etc.

        private BaseTower _tower;

        private void Awake()
        {
            _tower = GetComponent<BaseTower>();
        }

        private void OnDestroy()
        {
            // Nettoyage propre
            foreach (var instance in _activeInstances) instance.Disable();
        }

        /// <summary>
        ///     Cette fonction génère les actions pour l'UI en fonction de l'état actuel de l'arbre.
        /// </summary>
        public List<InteractionAction> GetUpgradeInteractions()
        {
            var actions = new List<InteractionAction>();

            if (upgradeTree == null || _currentTierIndex >= upgradeTree.tiers.Count)
                return actions;

            // On regarde le prochain palier (Tier)
            var nextTier = upgradeTree.tiers[_currentTierIndex];

            // Pour chaque option dans ce palier, on crée un bouton
            foreach (var option in nextTier.options)
            {
                if (option.upgradeDefinition == null) continue;

                var btnLabel = string.IsNullOrEmpty(option.labelOverride)
                    ? option.upgradeDefinition.upgradeName
                    : option.labelOverride;

                actions.Add(new InteractionAction
                {
                    Label = $"{btnLabel} ({option.cost})",
                    Icon = option.upgradeDefinition.icon,

                    Description = option.upgradeDefinition.description,

                    CanExecute = () => CurrencyManager.Instance.CanAfford(option.cost),
                    OnExecute = () => ApplyUpgrade(option)
                });
            }

            return actions;
        }


        private void ApplyUpgrade(UpgradeOption option)
        {
            if (!CurrencyManager.Instance.TrySpend(option.cost)) return;

            // 1. Création de l'instance
            var instance = option.upgradeDefinition.CreateInstance(_tower);

            // 2. Activation
            instance.Enable();

            // --- CHANGEMENT ICI ---
            // Au lieu de l'ajouter à une liste locale _activeInstances, 
            // on la donne directement à la tour.
            _tower.RegisterUpgrade(instance, option.upgradeDefinition);
            // ----------------------

            _currentTierIndex++;

            _tower.AddInvestment(option.cost);

            GameObject instanced = Instantiate(_tower.upgradeVFX, _tower.transform.position, Quaternion.identity);
            
            Destroy(instanced, 2.0f );
        }
    }
}