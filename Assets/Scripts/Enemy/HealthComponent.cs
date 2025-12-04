using System;
using UnityEngine;

namespace Enemy
{
    public class HealthComponent : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private ReactiveInt currentHealth = new(100);

        [SerializeField] private int maxHealth = 100;

        public IReadOnlyReactiveProperty<int> CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;

        public event Action<GameObject> OnDeath;

        public bool TakeDamage(int amount)
        {
            currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);

            if (currentHealth.Value <= 0)
            {
                OnDeath?.Invoke(gameObject);
                Destroy(gameObject);
                return true;
            }
            
            return false;
        }

        public void Heal(int amount)
        {
            currentHealth.Value = Mathf.Min(maxHealth, currentHealth.Value + amount);
        }
    }
}