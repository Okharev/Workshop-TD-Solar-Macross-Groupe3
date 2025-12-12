using System.Collections.Generic;
using Economy; // Assure-toi d'avoir accès au CurrencyManager
using UnityEngine;

namespace Towers
{
    [RequireComponent(typeof(BaseTower))]
    public class TowerLevelingManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TowerUpgradeTreeSo upgradeTree;

        private BaseTower _tower;
        private int _currentTierIndex = 0; // 0 = Base, 1 = Premier Upgrade acquis, etc.
        
        // Stocke les instances actives pour pouvoir les désactiver si besoin
        private List<IUpgradeInstance> _activeInstances = new();

        private void Awake()
        {
            _tower = GetComponent<BaseTower>();
        }

        /// <summary>
        /// Cette fonction génère les actions pour l'UI en fonction de l'état actuel de l'arbre.
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
                if(option.upgradeDefinition == null) continue;

                string btnLabel = string.IsNullOrEmpty(option.labelOverride) 
                    ? option.upgradeDefinition.upgradeName 
                    : option.labelOverride;

                actions.Add(new InteractionAction
                {
                    Label = $"{btnLabel} ({option.cost})",
                    Icon = option.upgradeDefinition.icon,
            
                    // --- NOUVEAU : On passe la description du SO ---
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
    
            Debug.Log($"Upgrade applied: {option.upgradeDefinition.upgradeName}");
        }
        
        private void OnDestroy()
        {
            // Nettoyage propre
            foreach (var instance in _activeInstances)
            {
                instance.Disable();
            }
        }
    }
}