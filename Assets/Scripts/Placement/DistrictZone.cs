using System;
using UnityEngine;

namespace Placement
{
    public class DistrictZone : MonoBehaviour
    {
        [Header("Settings")] public string districtName = "District A";

        public int maxEnergy = 100;

        [Header("Debug/Read Only")] [SerializeField]
        private int currentLoad;

        public int AvailableEnergy => maxEnergy - currentLoad;

        public event Action<int, int> OnEnergyChanged; // Current, Max

        public bool CanAccommodate(int amount)
        {
            return currentLoad + amount <= maxEnergy;
        }

        public void ConsumeEnergy(int amount)
        {
            currentLoad += amount;
            currentLoad = Mathf.Clamp(currentLoad, 0, maxEnergy);
            OnEnergyChanged?.Invoke(currentLoad, maxEnergy);
        }

        public void ReleaseEnergy(int amount)
        {
            currentLoad -= amount;
            currentLoad = Mathf.Clamp(currentLoad, 0, maxEnergy);
            OnEnergyChanged?.Invoke(currentLoad, maxEnergy);
        }
    }
}