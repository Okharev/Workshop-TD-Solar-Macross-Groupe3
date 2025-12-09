using System.Collections.Generic;
using System.Linq;
using Placement;
using UnityEngine;

namespace Enemy
{
    [RequireComponent(typeof(EnemyObjectiveTracker))]
    public sealed class FighterJetAi : MonoBehaviour
    {
        public enum AIState { Traveling, Attacking }

        [Header("1. Visual Setup")] 
        public Transform visualModel;
        public Vector3 modelCorrection = new Vector3(0, 0, 0); 
        public LayerMask allyLayer;

        [Header("2. Boids (The Swarm)")] 
        public float neighborRadius = 15f; 
        public float separationRadius = 8f;
        [Range(0, 5)] public float weightTarget = 1.2f; 
        [Range(0, 30)] public float weightSeparation = 25f; 

        [Header("3. Flight Aerodynamics")]
        public float maxThrustSpeed = 30f;
        public float minStallSpeed = 10f;
        public float acceleration = 10f;
    
        [Header("Turn Performance")]
        public float rollSpeed = 180f; 
        public float maxTurnRate = 50f; 
        [Range(0f, 1f)] public float driftFactor = 0.95f;

        [Header("4. Obstacle Avoidance")]
        public LayerMask obstacleLayer;
        public float whiskerLength = 40f; 
        [Range(0, 100)] public float weightAvoidance = 100f;

        [Header("5. Performance Settings")]
        [Tooltip("How many seconds between logic updates (Boids/Pathfinding). 0.1 = 10 times/sec.")]
        public float logicTickRate = 0.1f;
        private const int MaxNeighbors = 15;

        // --- Internal State ---
        private EnemyObjectiveTracker _tracker;
        private Vector3 currentMissionTarget;
        private AIState currentState = AIState.Traveling;
    
        // Navigation
        private List<Transform> waypoints;
        private int waypointIndex;
        private bool orbitClockwise = true;

        // Physics State
        private Vector3 _velocity;
        private float _currentSpeed;
        
        // Optimization Caches
        private float _logicTimer;
        private Vector3 _cachedFlockingDirection;
        private Collider[] _neighborBuffer; // Reusable buffer for physics
        private Transform _myTransform;     // Cached transform access

        private void Awake()
        {
            _tracker = GetComponent<EnemyObjectiveTracker>();
            _myTransform = transform;
            _neighborBuffer = new Collider[MaxNeighbors];
            
            _currentSpeed = maxThrustSpeed * 0.8f;
            _velocity = _myTransform.forward * _currentSpeed;
            _cachedFlockingDirection = _myTransform.forward;
        }

        private void Start()
        {
            orbitClockwise = Random.value > 0.5f;
            if (currentMissionTarget == Vector3.zero) 
                currentMissionTarget = _myTransform.position + _myTransform.forward * 100f;
            
            // Randomize timer slightly so all jets don't spike CPU on the exact same frame
            _logicTimer = Random.Range(0f, logicTickRate);
        }

        private void Update()
        {
            // 1. Heavy Logic (Throttled)
            _logicTimer += Time.deltaTime;
            if (_logicTimer >= logicTickRate)
            {
                RunHeavyLogic();
                _logicTimer = 0f;
            }

            // 2. Movement & Visuals (Every Frame for smoothness)
            RunAerodynamics();
            UpdateVisualModel();
        }

        // --- PUBLIC METHODS ---
        public void Initialize(List<Transform> pathPoints)
        {
            // Optimization: Remove LINQ allocation if possible, but valid here for one-time init
            waypoints = new List<Transform>(pathPoints).Where(t => t).ToList();
            if (waypoints.Count > 0)
            {
                waypointIndex = 0;
                currentMissionTarget = waypoints[0].position;
                currentState = AIState.Traveling;
            }
            else
            {
                currentState = AIState.Attacking;
            }
        }

        // --- HEAVY LOGIC (Throttled) ---
        private void RunHeavyLogic()
        {
            UpdateMissionLogic();
            _cachedFlockingDirection = CalculateFlockingVector();
        }

        private void UpdateMissionLogic()
        {
            float distSqr = (currentMissionTarget - _myTransform.position).sqrMagnitude;

            switch (currentState)
            {
                case AIState.Traveling:
                    // Distance check optimized with sqrMagnitude (30*30 = 900)
                    if (distSqr < 100f && waypoints is { Count: > 0 }) 
                    {
                        waypointIndex = (waypointIndex + 1) % waypoints.Count;
                        currentMissionTarget = waypoints[waypointIndex].position;
                    }
                    break;

                case AIState.Attacking:
                    // PERFORMANCE FIX: 
                    // We do NOT search for targets here using FindFirstObjectByType.
                    // We strictly rely on the Tracker. If Tracker is null, we fly straight/loiter.
                    var targetTransform = _tracker.CurrentTarget.Value;
                
                    if (targetTransform)
                    {
                        Vector3 targetPos = targetTransform.position;
                    
                        // Orbit Logic
                        Vector3 dirFromCenter = (_myTransform.position - targetPos).normalized;
                        dirFromCenter.y = 0; 
                    
                        Vector3 tangent = Vector3.Cross(dirFromCenter, Vector3.up);
                        if (!orbitClockwise) tangent = -tangent;

                        // Lead the turn
                        Vector3 attackPoint = targetPos + (tangent * 50f) + (Vector3.up * 10f);
                        currentMissionTarget = Vector3.Lerp(currentMissionTarget, attackPoint, logicTickRate * 3f);
                    }
                    else
                    {
                        // No target? Just fly forward to avoid spinning
                        currentMissionTarget = _myTransform.position + _myTransform.forward * 100f;
                    }
                    break;
            }
        }

        // --- PHYSICS CORE (Optimized) ---
        private void RunAerodynamics()
        {
            // Use the cached direction calculated in the slow loop
            Vector3 flockingDir = _cachedFlockingDirection;
        
            // Calculate rotation to face desired vector
            Quaternion targetRot = Quaternion.LookRotation(flockingDir);
        
            Vector3 localTargetDir = _myTransform.InverseTransformDirection(flockingDir);

            // Roll / Pitch Logic
            float targetRollAngle = -localTargetDir.x * 60f; 
            float yawToPitchTransfer = Mathf.Abs(localTargetDir.x); 
            float targetPitchInput = localTargetDir.y + yawToPitchTransfer; // Unused variable kept for logic clarity if needed later

            // Rotate towards target
            _myTransform.rotation = Quaternion.RotateTowards(_myTransform.rotation, targetRot, maxTurnRate * Time.deltaTime);

            // BANKING Visuals
            Vector3 flatForward = _myTransform.forward; flatForward.y = 0;
            if(flatForward.sqrMagnitude > 0.01f)
            {
                float currentRoll = NormalizeAngle(_myTransform.eulerAngles.z);
                float newRoll = Mathf.LerpAngle(currentRoll, targetRollAngle * 1.5f, Time.deltaTime * 2f);
                Vector3 euler = _myTransform.rotation.eulerAngles;
                _myTransform.rotation = Quaternion.Euler(euler.x, euler.y, newRoll);
            }

            // Speed Logic
            float gravityBoost = -_myTransform.forward.y * 10f;
            float turnDrag = Vector3.Angle(_myTransform.forward, _velocity.normalized) * 0.1f;
        
            float targetSpeed = maxThrustSpeed + gravityBoost - turnDrag;
            targetSpeed = Mathf.Clamp(targetSpeed, minStallSpeed, maxThrustSpeed * 1.5f);

            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * acceleration);

            // Apply Velocity (Drift)
            Vector3 noseVelocity = _myTransform.forward * _currentSpeed;
            _velocity = Vector3.Lerp(_velocity, noseVelocity, Time.deltaTime * (1f - driftFactor) * 20f);
        
            _myTransform.position += _velocity * Time.deltaTime;
        }

        private Vector3 CalculateFlockingVector()
        {
            Vector3 targetDir = (currentMissionTarget - _myTransform.position).normalized * weightTarget;
            Vector3 separation = Vector3.zero;
            
            // 1. Critical Avoidance 
            // (We keep this heavy check, but it only runs 10 times/sec now)
            Vector3 avoidance = GetObstacleAvoidanceVector();
            if (avoidance != Vector3.zero) return avoidance;

            // 2. Swarm Separation (OPTIMIZED: NonAlloc)
            int count = 0;
            // Use the pre-allocated buffer instead of creating a new array
            int foundNeighbors = Physics.OverlapSphereNonAlloc(_myTransform.position, neighborRadius, _neighborBuffer, allyLayer);
            
            float sepRadiusSqr = separationRadius * separationRadius;

            for(int i = 0; i < foundNeighbors; i++)
            {
                var c = _neighborBuffer[i];
                if (!c || c.gameObject == gameObject) continue;

                Vector3 diff = _myTransform.position - c.transform.position;
                float distSqr = diff.sqrMagnitude;
                
                // Compare squared distance to avoid Sqrt calls
                if (distSqr < sepRadiusSqr)
                {
                    // Only do Sqrt if we are actually too close and need the precise vector
                    separation += diff.normalized * (separationRadius / Mathf.Sqrt(distSqr));
                    count++;
                }
            }
            
            if (count > 0) separation /= count;

            // 3. Floor Avoidance
            Vector3 floorPush = Vector3.zero;
            if (Physics.Raycast(_myTransform.position, Vector3.down, out RaycastHit hit, 10f, obstacleLayer))
            {
                floorPush = Vector3.up * ((10f - hit.distance) * 2f);
            }

            return (targetDir + (separation * weightSeparation) + floorPush).normalized;
        }

        private Vector3 GetObstacleAvoidanceVector()
        {
            // SphereCast is expensive, but throttling makes it acceptable
            if (Physics.SphereCast(_myTransform.position, 3f, _myTransform.forward, out RaycastHit hit, whiskerLength, obstacleLayer))
            {
                return Vector3.Reflect(_myTransform.forward, hit.normal).normalized * weightAvoidance;
            }
            return Vector3.zero;
        }

        private void UpdateVisualModel()
        {
            float time = Time.time * 1.5f;
            float noiseX = (Mathf.PerlinNoise(time, 0) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0, time) - 0.5f) * 2f;

            Quaternion noiseRot = Quaternion.Euler(noiseX, noiseY, 0);
            visualModel.localRotation = Quaternion.Lerp(visualModel.localRotation, Quaternion.Euler(modelCorrection) * noiseRot, Time.deltaTime * 10f);
        }
    
        private static float NormalizeAngle(float a) => (a + 180) % 360 - 180;

        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, currentMissionTarget);
            
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, _velocity.normalized * 10f);
            }
        }
    }
}