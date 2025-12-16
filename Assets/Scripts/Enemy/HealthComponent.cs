using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Enemy
{
    public sealed class HealthComponent : MonoBehaviour
    {
        [Header("Configuration")] [SerializeField]
        private ReactiveInt currentHealth = new(100);

        [SerializeField] private int maxHealth = 100;

        [SerializeField] public GameObject onDeathVfx;

        public IReadOnlyReactiveProperty<int> CurrentHealth => currentHealth;
        public int CurrentHealthRaw => CurrentHealth.Value;
        public int MaxHealth => maxHealth;

        private void Start()
        {
            var waveManager = FindAnyObjectByType<WaveManager>();
            if (waveManager) waveManager.RegisterEnemy(this);
        }

        public event Action<GameObject> OnDeath;

        public bool TakeDamage(int amount)
        {
            
            
            currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);

            if (currentHealth.Value <= 0)
            {
                OnDeath?.Invoke(gameObject);
                
                if (onDeathVfx)
                {
                    Instantiate(onDeathVfx,
                        new Vector3(transform.position.x, transform.position.y + 1.0f, transform.position.z),
                        Quaternion.identity);
                }

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