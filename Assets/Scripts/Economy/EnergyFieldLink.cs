using UnityEngine;

namespace Economy
{
    // Add this script automatically in EnergyProducer.GenerateRangeTrigger
    public class EnergyFieldLink : MonoBehaviour
    {
        private IEnergyProducer _mainProducer;

        public void Initialize(IEnergyProducer producer)
        {
            _mainProducer = producer;
        }

        public IEnergyProducer GetProducer()
        {
            return _mainProducer;
        }
    }
}