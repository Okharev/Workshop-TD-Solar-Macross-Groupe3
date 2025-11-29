using Placement;
using UnityEngine;

namespace Towers
{
    // Reference the namespace above

    [CreateAssetMenu(fileName = "New Tower", menuName = "Tower Defense/Blueprints/Tower")]
    public class TowerBlueprintSo : BuildingSo
    {
        public float baseDamage = 10f;
        public float baseRange = 15f;
        public float baseFireRate = 1f;

        public TowerBlueprintSo nextUpgrade;
    }
}