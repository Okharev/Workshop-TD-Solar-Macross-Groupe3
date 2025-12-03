using Enemy;
using UnityEngine;
using UnityEngine.Events;

namespace Placement
{
    [RequireComponent(typeof(HealthComponent))]
    public class DestructibleObjective : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Drag the tight physical collider here. Enemies will aim for this.")]
        [SerializeField]
        private Collider _mainBodyCollider;

        [Header("Events")] public UnityEvent OnDestroyed;

        private HealthComponent _health;

        // Public property so enemies can read it safely
        public Collider MainCollider => _mainBodyCollider;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();

            // Fallback: If you forget to drag it, try to find one, but warn us
            if (_mainBodyCollider == null)
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
            Debug.Log("Objective destroyed!");
            OnDestroyed?.Invoke();

            gameObject.SetActive(false);
            Destroy(gameObject, 0.1f);
        }
    }
}