using UnityEngine;

namespace Economy
{
    // Add this script automatically in EnergyProducer.GenerateRangeTrigger
    public class EnergyFieldLink : MonoBehaviour
    {
        private IReactiveEnergyProducer _mainProducer;

        public void Initialize(IReactiveEnergyProducer producer)
        {
            _mainProducer = producer;
        }

        public IReactiveEnergyProducer GetProducer()
        {
            return _mainProducer;
        }
    }
}