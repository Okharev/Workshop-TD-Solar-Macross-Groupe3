using UnityEngine;

namespace Economy
{
    public class EnergyFieldLink : MonoBehaviour
    {
        private EnergyProducer _mainProducer;

        public void Initialize(EnergyProducer producer)
        {
            _mainProducer = producer;
        }

        public EnergyProducer GetProducer()
        {
            return _mainProducer;
        }
    }
}