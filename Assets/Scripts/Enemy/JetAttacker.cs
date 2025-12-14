using System.Collections;
using UnityEngine;

namespace Enemy.Combat
{
    [RequireComponent(typeof(FighterJetAi))]
    [RequireComponent(typeof(EnemyObjectiveTracker))]
    public class JetAttacker : MonoBehaviour
    {
        [Header("Attack Settings")] [Tooltip("Damage per shot")]
        public int damage = 10;

        [Tooltip("Time between shots in seconds")]
        public float fireRate = 0.1f;

        [Tooltip("Max distance to start firing")]
        public float attackRange = 100f;

        [Header("Visuals")] public Transform firePoint;

        public LineRenderer tracerPrefab;

        [Tooltip("Color of the laser/tracer")] public Color tracerColor = Color.red;

        // State
        private float _fireCountdown;

        // References
        private FighterJetAi _jetAi;
        private EnemyObjectiveTracker _tracker;

        private void Awake()
        {
            _jetAi = GetComponent<FighterJetAi>();
            _tracker = GetComponent<EnemyObjectiveTracker>();
        }

        private void Update()
        {
            // 1. Logic Check: Only attack if we are in the Attack State (Orbiting)
            if (_jetAi.CurrentState != FighterJetAi.AIState.Attacking) return;

            // 2. Target Check
            var target = _tracker.CurrentTarget.Value;
            if (target == null) return;

            // 3. Range Check
            var distSqr = (target.position - transform.position).sqrMagnitude;
            if (distSqr > attackRange * attackRange) return;

            // 4. Rate of Fire
            _fireCountdown -= Time.deltaTime;
            if (_fireCountdown <= 0f)
            {
                Shoot(target);
                _fireCountdown = fireRate;
            }
        }

        private void Shoot(Transform target)
        {
            // --- A. Logic: Deal Damage ---

            // Try to find health on the target (assuming generic HealthComponent or similar)
            // You might need to adjust this depending on your specific Health script name
            var hp = target.GetComponent<HealthComponent>();
            if (hp != null)
            {
                hp.TakeDamage(damage);
            }
            else
            {
                // Fallback: If it's a DestructibleObjective without a separate health component
                // We try to find a damage handler on it
                var objective = target.GetComponent<HealthComponent>();
                if (objective != null) objective.TakeDamage(damage);
            }

            // --- B. Visuals: Draw Tracer ---
            if (tracerPrefab && firePoint) StartCoroutine(ShowTracer(target.position));
        }

        private IEnumerator ShowTracer(Vector3 targetPos)
        {
            // Simple visual effect: Line from Jet to Objective
            var tracer = Instantiate(tracerPrefab, firePoint.position, Quaternion.identity);

            tracer.startColor = tracerColor;
            tracer.endColor = tracerColor;

            // Randomize end point slightly to simulate machine gun spread
            var spread = Random.insideUnitSphere * 2.0f;

            tracer.SetPosition(0, firePoint.position);
            tracer.SetPosition(1, targetPos + spread);

            // Fade out quickly
            var alpha = 1.0f;
            while (alpha > 0)
            {
                alpha -= Time.deltaTime * 10f; // Disappear in 0.1s
                tracer.startColor = new Color(tracerColor.r, tracerColor.g, tracerColor.b, alpha);
                tracer.endColor = new Color(tracerColor.r, tracerColor.g, tracerColor.b, alpha);
                yield return null;
            }

            Destroy(tracer.gameObject);
        }
    }
}