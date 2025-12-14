using Economy;
using UnityEngine;

namespace Enemy
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class LootMoneyOnDeath : MonoBehaviour
    {
        public int amountToLoot;
        private HealthComponent _healthComponent;

        private void Awake()
        {
            if (!TryGetComponent(out _healthComponent)) Debug.LogError("failed to get health component");
        }

        private void OnEnable()
        {
            _healthComponent.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            _healthComponent.OnDeath -= HandleDeath;
        }

        private void HandleDeath(GameObject victim)
        {
            CurrencyManager.Instance.Gain(amountToLoot);
        }
    }
}