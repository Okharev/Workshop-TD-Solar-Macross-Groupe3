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
        public Action OnExecute;
        public Func<bool> CanExecute;
    }
    
    public interface ISelectable
    {
        string DisplayName { get; }
        string Description { get; }
        
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
        
        public int RefundCost => Mathf.RoundToInt(cost * refundRatio);
        
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
            // Do fancy shader & audio stuff
        }

        public virtual void OnDeselect()
        {
            // Remove fancy shader stuff
        }
    }
}