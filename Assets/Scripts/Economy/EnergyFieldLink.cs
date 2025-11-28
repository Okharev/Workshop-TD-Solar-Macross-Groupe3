using UnityEngine;

namespace Economy
{
    // Add this script automatically in EnergyProducer.GenerateRangeTrigger
    public class EnergyFieldLink : MonoBehaviour
    {
        private IEnergyProducer mainProducer;

        public void Initialize(IEnergyProducer producer)
        {
            mainProducer = producer;
        }

        public IEnergyProducer GetProducer()
        {
            return mainProducer;
        }
    }
}