using System;
using System.Collections.Generic;
using Buildings;
using Economy;
using Placement;
using UI;
using UnityEngine;

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
        [Header("Identity")] public string displayName;

        [TextArea] public string description;
        public Sprite icon;
        public GameObject currentLevelPrefab;
        public GameObject nextLevelPrefab;

        [Header("Economy")] public int upgradeCost;

        public int cost;
        public int energyDrain;

        public BuildingLevelSo nextUpgrade;

        [Range(0.0f, 1.0f)] public float refundRatio;

        [Header("Feedback Visuals")] 
        [Tooltip("Effet joué lors de la construction/apparition")]
        [SerializeField] protected GameObject buildVFX;
        
        [Tooltip("Effet joué lors de la vente/destruction")]
        [SerializeField] protected GameObject sellVFX;
        
        [Tooltip("Effet joué lors de l'amélioration")]
        [SerializeField] protected GameObject upgradeVFX;

        private int _totalInvested;

        public int RefundCost => Mathf.RoundToInt(_totalInvested * refundRatio);

        protected virtual void Awake()
        {
            _totalInvested = cost;
        }

        protected virtual void Start()
        {
            // Joue l'effet de construction au démarrage
            PlayVFX(buildVFX, transform.position);
        }

        public event Action OnDataChanged;

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
                actions.Add(new InteractionAction
                {
                    Label = $"Améliorer ({nextUpgrade.upgradeCost})",
                    OnExecute = Upgrade,
                    CanExecute = () => CurrencyManager.Instance.CanAfford(nextUpgrade.upgradeCost)
                });

            return actions;
        }

        public string DisplayName => displayName;
        public string Description => description;

        public virtual void OnSelect()
        {
            if (EnergyHeatmapSystem.Instance && UsesEnergy()) EnergyHeatmapSystem.Instance.ToggleHeatmap(true);
        }

        public virtual void OnDeselect()
        {
            if (EnergyHeatmapSystem.Instance) EnergyHeatmapSystem.Instance.ToggleHeatmap(false);
        }

        public void AddInvestment(int amount)
        {
            _totalInvested += amount;
            NotifyChange();
        }

        public void NotifyChange()
        {
            OnDataChanged?.Invoke();
        }

        protected virtual void Sell()
        {
            // Joue l'effet de vente avant de détruire
            PlayVFX(sellVFX, transform.position);

            SelectionManager.Deselect();
            BuildingManager.SellBuilding(this);
        }

        protected virtual void Upgrade()
        {
            // Joue l'effet d'upgrade
            PlayVFX(upgradeVFX, transform.position);
            
            BuildingManager.Instance.UpgradeBuilding(this);
        }

        /// <summary>
        /// Méthode utilitaire pour instancier un VFX et le détruire après un délai.
        /// </summary>
        protected void PlayVFX(GameObject vfxPrefab, Vector3 position, Quaternion rotation = default)
        {
            if (vfxPrefab == null) return;

            if (rotation.Equals(default(Quaternion))) rotation = Quaternion.identity;

            GameObject instance = Instantiate(vfxPrefab, position, rotation);
            
            // Nettoyage automatique après 2 secondes (ajustable selon vos VFX)
            Destroy(instance, 2.0f); 
        }

        private bool UsesEnergy()
        {
            return GetComponent<EnergyConsumer>() || GetComponent<EnergyProducer>();
        }
    }
}