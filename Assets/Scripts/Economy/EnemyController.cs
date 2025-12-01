using System;
using R3;
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

        [SerializeField] public ReactiveStat damage;
        [SerializeField] public ReactiveStat range;
        [SerializeField] public ReactiveStat fireRate;
        [SerializeField] public ReactiveStat health;
        [SerializeField] public ReactiveStat speed;

        private NavMeshAgent _agent;

        private void Awake()
        {
            InitializeReactiveStats();

            _agent = GetComponent<NavMeshAgent>();
            
            
            speed.Value.Subscribe(newSpeed =>
                {
                    _agent.speed = Mathf.Max(0, newSpeed);
                    Debug.Log($"Enemy Speed Updated: {_agent.speed}");
                })
                .AddTo(this);
        }
        
        public void InitializeReactiveStats()
        {
            damage = new ReactiveStat(baseDamage);
            range = new ReactiveStat(baseRange);
            fireRate = new ReactiveStat(baseFireRate);
            health = new ReactiveStat(baseHealth);
            speed = new ReactiveStat(baseSpeed);
            
            damage.Initialize();
            range.Initialize();
            fireRate.Initialize();
            health.Initialize();
            speed.Initialize();
        }
        
        private void OnDestroy()
        {
            speed.Dispose();
        }
    }
}