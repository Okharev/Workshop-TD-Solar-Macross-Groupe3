using UnityEngine;

namespace Placement
{
    [CreateAssetMenu(fileName = "Buildings", menuName = "TowerDefense/Building Config")]
    public class BuildingSo : ScriptableObject
    {
        public string name;
        public string description;
        public Texture2D icon;
        public int cost;
        public int energyDrain = 10;
        public GameObject prefab;
    }
}