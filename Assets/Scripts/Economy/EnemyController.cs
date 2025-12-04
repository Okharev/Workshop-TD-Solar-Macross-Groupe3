using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Towers;
using UI;
using UnityEngine;
using UnityEngine.AI;

namespace Economy
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour, ISelectable
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

        [SerializeField] private bool isKnockedBack;

        private NavMeshAgent _agent;

        private void Awake()
        {
            InitializeReactiveStats();

            _agent = GetComponent<NavMeshAgent>();

            speed.Observable.Subscribe(newSpeed =>
                {
                    _agent.speed = Mathf.Max(0, newSpeed);
                    Debug.Log($"Enemy Speed Updated: {_agent.speed}");
                })
                .AddTo(this);
        }

        public string DisplayName => "Yaa";
        public string Description => "SDFSDF";

        public Dictionary<string, string> GetStats()
        {
            return new Dictionary<string, string>
            {
                { "Range", range.Value.ToString(CultureInfo.InvariantCulture) },
                { "Speed", fireRate.Value.ToString(CultureInfo.InvariantCulture) },
                { "Damage", damage.Value.ToString(CultureInfo.InvariantCulture) }
            };
        }

        public List<InteractionButton> GetInteractions()
        {
            return null;
        }

        public void OnSelect()
        {
        }

        public void OnDeselect()
        {
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

        public void ApplyKnockback(Vector3 sourcePosition, float force, float duration)
        {
            if (isKnockedBack) return;

            StartCoroutine(KnockbackRoutine(sourcePosition, force, duration));
        }

        private IEnumerator KnockbackRoutine(Vector3 sourcePosition, float force, float duration)
        {
            isKnockedBack = true;

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;

            var direction = (transform.position - sourcePosition).normalized;
            direction.y = 0;

            float timer = 0;

            while (timer < duration)
            {
                _agent.Move(direction * (force * Time.deltaTime));

                timer += Time.deltaTime;
                yield return null;
            }

            _agent.isStopped = false;
            isKnockedBack = false;
        }
    }
}