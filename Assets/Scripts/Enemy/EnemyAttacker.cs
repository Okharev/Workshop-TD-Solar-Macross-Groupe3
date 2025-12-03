using Placement;
using UnityEngine;

namespace Enemy.Combat
{
    [RequireComponent(typeof(EnemyObjectiveTracker))]
    public abstract class EnemyAttacker : MonoBehaviour
    {
        [Header("General Stats")] public float attackRate = 1.0f;

        public float attackRange = 2.0f;

        private Collider _targetCollider;
        private Transform _targetTransform;

        protected float lastAttackTime;
        protected EnemyMovement movement;
        protected EnemyObjectiveTracker tracker;

        protected virtual void Awake()
        {
            tracker = GetComponent<EnemyObjectiveTracker>();
            movement = GetComponent<EnemyMovement>();
        }

// Inside EnemyAttacker.cs

        protected virtual void Start()
        {
            if (movement != null) movement.SetStoppingDistance(attackRange);

            tracker.CurrentTarget.Subscribe(newTarget =>
            {
                _targetTransform = newTarget;
                _targetCollider = null;

                if (newTarget != null)
                    // Use the same logic here
                    _targetCollider = GetValidCollider(newTarget);
            }).AddTo(this);
        }

        protected virtual void Update()
        {
            if (_targetTransform == null) return;

            float effectiveDistance;

            if (_targetCollider != null)
            {
                var closestPoint = _targetCollider.ClosestPoint(transform.position);
                effectiveDistance = Vector3.Distance(transform.position, closestPoint);
            }
            else
            {
                effectiveDistance = Vector3.Distance(transform.position, _targetTransform.position);
            }

            if (effectiveDistance <= attackRange)
                if (Time.time >= lastAttackTime + attackRate)
                {
                    PerformAttack(_targetTransform.gameObject);
                    lastAttackTime = Time.time;
                }
        }

        // Copy the same helper method here
        private Collider GetValidCollider(Transform target)
        {
            var objective = target.GetComponent<DestructibleObjective>();
            if (objective != null && objective.MainCollider != null) return objective.MainCollider;

            var allColliders = target.GetComponents<Collider>();
            foreach (var col in allColliders)
                if (!col.isTrigger && col.enabled)
                    return col;

            if (allColliders.Length > 0) return allColliders[0];
            return null;
        }

        protected abstract void PerformAttack(GameObject target);
    }
}