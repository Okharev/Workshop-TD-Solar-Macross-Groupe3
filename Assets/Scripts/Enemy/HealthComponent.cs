using System;
using UnityEngine;

namespace Enemy
{
    public sealed class HealthComponent : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private ReactiveInt currentHealth = new(100);

        [SerializeField] private int maxHealth = 100;

        public IReadOnlyReactiveProperty<int> CurrentHealth => currentHealth;
        public int CurrentHealthRaw => CurrentHealth.Value;
        public int MaxHealth => maxHealth;

        public event Action<GameObject> OnDeath;

        private void Start()
        {
            var waveManager = FindAnyObjectByType<WaveManager>();
            if (waveManager)
            {
                waveManager.RegisterEnemy(this);
            }
        }

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