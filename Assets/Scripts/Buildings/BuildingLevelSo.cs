using UnityEngine;

namespace Buildings
{
    [CreateAssetMenu(fileName = "Building1", menuName = "Buildinga2")]
    public sealed class BuildingLevelSo : ScriptableObject
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

    }
}