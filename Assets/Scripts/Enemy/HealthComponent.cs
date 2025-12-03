using UnityEngine;
using UnityEngine.Serialization;

namespace Enemy
{

    
    
    public class HealthComponent : MonoBehaviour
    {
        [Header("Configuration")]
        // Utilisation de votre ReactiveInt pour la sérialisation et les événements
        [SerializeField] private ReactiveInt _currentHealth = new ReactiveInt(100);
        [SerializeField] private int _maxHealth = 100;

        // Exposition en lecture seule pour la sécurité
        public IReadOnlyReactiveProperty<int> CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;

        public bool IsDead => _currentHealth.Value <= 0;

        public void TakeDamage(int amount)
        {
            if (IsDead) return;

            // Modification simple de la valeur, le système réactif préviendra les abonnés
            _currentHealth.Value = Mathf.Max(0, _currentHealth.Value - amount);
        }

        public void Heal(int amount)
        {
            if (IsDead) return;
            _currentHealth.Value = Mathf.Min(_maxHealth, _currentHealth.Value + amount);
        }

        // Une méthode pratique pour réinitialiser (utile pour le pooling d'ennemis)
        public void ResetHealth()
        {
            _currentHealth.Value = _maxHealth;
        }
    }
}