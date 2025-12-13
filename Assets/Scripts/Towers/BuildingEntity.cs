using System;
using System.Collections.Generic;
using Buildings;
using Economy;
using Placement;
using UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Towers
{
    public struct InteractionAction
    {
        public string Label;
        public Sprite Icon;
        public string Description;
        public Action OnExecute;
        public Func<bool> CanExecute;
    }
    
    public interface ISelectable
    {
        string DisplayName { get; }
        string Description { get; }
        
        event Action OnDataChanged;
        
        void OnSelect();
        void OnDeselect();
        
        List<InteractionAction> GetInteractions();
    }

    
    public abstract class BuildingEntity : MonoBehaviour, ISelectable
    {
        [Header("Identity")]
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public GameObject currentLevelPrefab;
        public GameObject nextLevelPrefab;

        [Header("Economy")]
        public int upgradeCost;
        public int cost;
        public int energyDrain;

        public BuildingLevelSo nextUpgrade;
        
        [Range(0.0f, 1.0f)]
        public float refundRatio;
        
        private int _totalInvested;
        
        
        public event Action OnDataChanged;
        
        public int RefundCost => Mathf.RoundToInt(_totalInvested * refundRatio);

        // Méthode pour ajouter de la valeur (appelée par le système d'upgrade)
        public void AddInvestment(int amount)
        {
            _totalInvested += amount;
            NotifyChange(); // On prévient que la valeur a changé
        }
        
        

        // Méthode utilitaire pour déclencher l'événement
        public void NotifyChange()
        {
            OnDataChanged?.Invoke();
        }
        
        
        protected virtual void Awake()
        {
            _totalInvested = cost;
        }
        
        public virtual List<InteractionAction> GetInteractions()
        {
            var actions = new List<InteractionAction>
            {
                new()
                {
                    Label = $"Vendre (+{RefundCost})",
                    OnExecute = Sell,
                    CanExecute = () => true
                }
            };

            if (nextUpgrade)
            {
                actions.Add(new InteractionAction
                {
                    Label = $"Améliorer ({nextUpgrade.upgradeCost})",
                    OnExecute = Upgrade,
                    CanExecute = () => CurrencyManager.Instance.CanAfford(nextUpgrade.upgradeCost)
                });
            }

            return actions;
        }

        protected virtual void Sell()
        {
            // 1. On force la désélection. 
            // Cela va déclencher l'événement OnDeselected que l'InfoPanel écoute.
            SelectionManager.Deselect(); 

            // 2. Ensuite, on vend/détruit le bâtiment
            BuildingManager.SellBuilding(this);
        }

        protected virtual void Upgrade()
        {
            BuildingManager.Instance.UpgradeBuilding(this);
        }

        public string DisplayName => displayName;
        public string Description => description;

        public virtual void OnSelect()
        {
            // 1. Existing visual selection logic (shaders, outlines, etc.)
            // base.OnSelect() if you have logic in a parent, but here this IS the parent.

            // 2. Turn on Heatmap
            // We only show it if the system exists AND the building uses energy
            if (EnergyHeatmapSystem.Instance && UsesEnergy())
            {
                EnergyHeatmapSystem.Instance.ToggleHeatmap(true);
            }
        }

        public virtual void OnDeselect()
        {

            // 2. Turn off Heatmap
            if (EnergyHeatmapSystem.Instance)
            {
                EnergyHeatmapSystem.Instance.ToggleHeatmap(false);
            }
        }
        
        private bool UsesEnergy()
        {
            // Returns true if we have a Consumer (Tower) or Producer (Generator)
            return GetComponent<EnergyConsumer>() || GetComponent<EnergyProducer>();
        }
    }
}