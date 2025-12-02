using UnityEngine;
using UnityEngine.Serialization;

namespace Enemy
{
    public interface IDamageable
    {
        IReadOnlyReactiveProperty<float> Health { get; }
    
        float MaxHealth { get; }
        bool IsDead { get; }

        void TakeDamage(float amount);
        void Heal(float amount);
    }
    
    
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Configuration")]
        [SerializeField] private float maxHealth = 100f;

        [SerializeField] private ReactiveFloat currentHealth = new(100f);

        public IReadOnlyReactiveProperty<float> Health => currentHealth;
        public float MaxHealth => maxHealth;
    
        public bool IsDead => currentHealth.Value <= 0;

        private void Start()
        {
            currentHealth.Value = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            float newValue = currentHealth.Value - amount;
        
            currentHealth.Value = Mathf.Clamp(newValue, 0, maxHealth);

            Debug.Log($"{name} took {amount} damage. Current: {currentHealth.Value}");
        }

        public void Heal(float amount)
        {
            if (IsDead) return;

            float newValue = currentHealth.Value + amount;
            currentHealth.Value = Mathf.Clamp(newValue, 0, maxHealth);
        }
    }
}