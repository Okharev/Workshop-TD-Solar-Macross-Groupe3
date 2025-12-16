using System; // Important pour 'Action'
using Enemy;
using UnityEngine;

namespace Placement
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class DestructibleObjective : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Drag the tight physical collider here. Enemies will aim for this.")]
        [SerializeField]
        private Collider _mainBodyCollider;

        public event Action OnDestroyed;

        private HealthComponent _health;

        public Collider MainCollider => _mainBodyCollider;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();

            if (!_mainBodyCollider)
            {
                _mainBodyCollider = GetComponent<Collider>();
                Debug.LogWarning($"{name}: MainBodyCollider is missing! Enemies might aim at the wrong part.");
            }
        }

        private void Start()
        {
            _health.CurrentHealth.Subscribe(health =>
            {
                if (health <= 0) HandleDestruction();
            }, false).AddTo(this);
        }

        private void HandleDestruction()
        {
            if (!gameObject.activeSelf) return;

            Debug.Log("Objective destroyed!");
            
            // On déclenche l'événement pour le GameResultController
            OnDestroyed?.Invoke();

            gameObject.SetActive(false);
            Destroy(gameObject, 0.1f);
        }
        
        // Optionnel : On s'assure de vider les abonnés à la destruction de l'objet
        private void OnDestroy()
        {
            OnDestroyed = null;
        }
    }
}