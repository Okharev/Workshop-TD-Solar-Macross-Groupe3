using Enemy;
using UnityEngine;
using UnityEngine.Events;

namespace Pathing.Gameplay
{
    // Nécessite obligatoirement le composant de santé
    [RequireComponent(typeof(HealthComponent))]
    public class DestructibleObjective : MonoBehaviour
    {
        [Header("Events")]
        public UnityEvent OnDestroyed;

        private HealthComponent _health;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
        }

        private void Start()
        {
            _health.CurrentHealth.Subscribe(health => 
            {
                if (health <= 0)
                {
                    HandleDestruction();
                }
            }, fireImmediately: false).AddTo(this);
        }

        private void HandleDestruction()
        {
            Debug.Log("L'objectif a été détruit !");
            OnDestroyed?.Invoke();
            
        }
    }
}