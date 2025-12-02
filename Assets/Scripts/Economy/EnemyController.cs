using System.Collections;
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

        [SerializeField] private bool isKnockedBack;
        
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
            if (isKnockedBack) return; // Évite le spam de knockback

            StartCoroutine(KnockbackRoutine(sourcePosition, force, duration));
        }

        private IEnumerator KnockbackRoutine(Vector3 sourcePosition, float force, float duration)
        {
            isKnockedBack = true;

            // 1. Désactiver le contrôle du NavMesh
            // On garde l'agent actif pour l'évitement, mais on coupe sa vélocité
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;

            // 2. Calculer la direction du recul (de la tour vers l'ennemi)
            Vector3 direction = (transform.position - sourcePosition).normalized;
            direction.y = 0; // On garde le recul à l'horizontale pour ne pas qu'il s'envole

            float timer = 0;

            while (timer < duration)
            {
                // 3. Déplacer manuellement l'ennemi
                // On utilise Move de l'agent pour respecter les collisions du NavMesh
                _agent.Move(direction * (force * Time.deltaTime));
            
                timer += Time.deltaTime;
                yield return null; // Attendre la prochaine frame
            }

            // 4. Réactiver le mouvement normal
            _agent.isStopped = false;
            isKnockedBack = false;
        }
    }
}