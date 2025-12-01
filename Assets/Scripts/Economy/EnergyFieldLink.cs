using UnityEngine;

namespace Economy
{
    // Used on child objects to help Raycasts find the parent Producer
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