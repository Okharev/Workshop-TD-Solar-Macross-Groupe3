using Towers;
using UnityEngine;
using UnityEngine.AI;

namespace Economy
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] public float baseDamage = 10f;
        [SerializeField] public float baseRange = 15f;
        [SerializeField] public float baseFireRate = 1f;
        [SerializeField] public float baseHealth = 100f;
        [SerializeField] public float baseSpeed = 2f;

        [SerializeField] public Stat damage;
        [SerializeField] public Stat range;
        [SerializeField] public Stat fireRate;
        [SerializeField] public Stat health;
        [SerializeField] public Stat speed;

        private NavMeshAgent _agent;

        private void Awake()
        {
            InitializeReactiveStats();

            _agent = GetComponent<NavMeshAgent>();

            speed.Subscribe(newSpeed =>
                {
                    _agent.speed = Mathf.Max(0, newSpeed);
                    Debug.Log($"Enemy Speed Updated: {_agent.speed}");
                })
                .AddTo(this);
        }

        public void InitializeReactiveStats()
        {
            damage = new Stat(baseDamage);
            range = new Stat(baseRange);
            fireRate = new Stat(baseFireRate);
            health = new Stat(baseHealth);
            speed = new Stat(baseSpeed);

            damage.Initialize();
            range.Initialize();
            fireRate.Initialize();
            health.Initialize();
            speed.Initialize();
        }
    }
}