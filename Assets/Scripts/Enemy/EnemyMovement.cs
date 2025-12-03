using Pathing.Gameplay;
using Placement;
using UnityEngine;
using UnityEngine.AI;

namespace Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyObjectiveTracker))]
    public class EnemyMovement : MonoBehaviour
    {
        private NavMeshAgent _agent;
        private EnemyObjectiveTracker _tracker;
    
        // CACHED VARIABLES
        private Transform _currentTargetTransform;
        private Collider _currentTargetCollider; 

        [Header("Movement Settings")]
        [SerializeField] private float defaultStoppingDistance = 2.0f; 
    
        [Header("Flanking (Anti-Clumping)")]
        [Tooltip("How wide (in degrees) the enemies should spread out.")]
        [Range(0, 90)] public float spreadAngle = 45f; // 45 degrees left or right
    
        private float _myFlankBias; // Unique random value for this specific unit
        private Vector3 _lastKnownTargetPosition;
        private float _repathThreshold = 1.0f;

        public NavMeshAgent Agent => _agent;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _tracker = GetComponent<EnemyObjectiveTracker>();
        }

        private void Start()
        {
            _agent.autoBraking = false;
            _agent.stoppingDistance = defaultStoppingDistance;
            _myFlankBias = Random.Range(-spreadAngle, spreadAngle);

            _tracker.CurrentTarget.Subscribe(newTarget => 
            {
                _currentTargetTransform = newTarget;
                _currentTargetCollider = null; 

                if (newTarget != null)
                {
                    // USE THE NEW HELPER FUNCTION
                    _currentTargetCollider = GetValidCollider(newTarget);

                    UpdatePathImmediate(newTarget.position);
                }
                else
                {
                    _agent.ResetPath();
                }
            }).AddTo(this);
        }

        // --- ADD THIS HELPER METHOD ---
        private Collider GetValidCollider(Transform target)
        {
            // 1. Try to get the manually assigned "MainBody" from DestructibleObjective
            var objective = target.GetComponent<DestructibleObjective>();
            if (objective != null && objective.MainCollider != null)
            {
                return objective.MainCollider;
            }

            // 2. If no script, look for ALL colliders on the object
            Collider[] allColliders = target.GetComponents<Collider>();

            // 3. Priority: Return the first NON-TRIGGER collider (Physical wall)
            foreach (var col in allColliders)
            {
                if (!col.isTrigger && col.enabled) return col;
            }

            // 4. Fallback: If only triggers exist, return the first one (better than nothing)
            if (allColliders.Length > 0) return allColliders[0];

            return null;
        }

        private void Update()
        {
            if (_currentTargetTransform)
            {
                if (Vector3.SqrMagnitude(_currentTargetTransform.position - _lastKnownTargetPosition) > _repathThreshold * _repathThreshold)
                {
                    UpdatePathImmediate(_currentTargetTransform.position);
                }
            }
        }

        private void UpdatePathImmediate(Vector3 targetPos)
        {
            Vector3 finalDestination = targetPos;

            if (_currentTargetCollider != null)
            {
                // 1. Calculate direction from Target TO Me (The approach angle)
                Vector3 directionFromTarget = (transform.position - targetPos).normalized;

                // 2. Rotate this direction by my personal Flank Bias
                // This creates a "preferred angle" of attack relative to the target
                Quaternion rotation = Quaternion.Euler(0, _myFlankBias, 0);
                Vector3 flankedDirection = rotation * directionFromTarget;

                // 3. Project a point onto the collider surface using this angled direction
                // We ask: "If I were standing at this angle, where is the closest point?"
                // We simulate a position 20 units away in that direction to find the edge.
                Vector3 searchPos = targetPos + (flankedDirection * 20f);
            
                finalDestination = _currentTargetCollider.ClosestPoint(searchPos);
            }

            _lastKnownTargetPosition = targetPos;
            _agent.SetDestination(finalDestination);
        }
    
        public void SetStoppingDistance(float distance)
        {
            if(_agent != null) _agent.stoppingDistance = distance;
        }
    }
}