using Towers.ProjectileDerived;
using UnityEngine;
using UnityEngine.AI;
// Nécessaire pour NavMeshAgent
using Random = UnityEngine.Random;

namespace Towers.TowerDerived
{
    public class TowerMortar : BaseTower
    {
        [Header("Targeting Settings")]
        [Tooltip("The radius used to determine how many enemies are in a cluster.")]
        [SerializeField]
        private float packRadius = 5f;

        [Header("Ballistics")] [Tooltip("Minimum flight time (close range).")] [SerializeField]
        private float minProjectileTravelTime = 0.5f;

        [Tooltip("Maximum flight time (max range).")] [SerializeField]
        private float maxProjectileTravelTime = 2.0f;

        [SerializeField] private MortarBomb mortarProjectilePrefab;

        [SerializeField] private float radiusOfImpact = 6f;

        // Cache for checking density around a specific target
        private readonly Collider[] _densityCheckCache = new Collider[30];

        // --- Cache (Optimization to avoid GC) ---
        // Cache for the initial wide scan
        private readonly Collider[] _potentialTargetsCache = new Collider[100];

        // --- State ---
        private float _currentProjectileTravelTime;
        private bool _hasValidTarget;
        private Vector3 _predictedAimPoint;

        protected override void Update()
        {
            if (!powerSource.IsPowered) return;

            // Recalculate aiming if we have a target
            if (_hasValidTarget)
            {
                // Note: In this logic, we aim at a static point calculated during AcquireTarget.
                // If you want continuous tracking, call AcquireTarget() every frame or use a timer.
                // For Mortars, usually updating aim only on AcquireTarget or periodically is better for arcs.
            }

            // Aim Logic
            var isAligned = AimAtTarget(Vector3.zero); // aimPoint ignored by override

            fireCountdown -= Time.deltaTime;

            if (isAligned && fireCountdown <= 0f)
                // Re-check path before firing
                if (IsPathClear(firePoint.position, _predictedAimPoint, _currentProjectileTravelTime))
                {
                    Fire();
                    fireCountdown = 1f / fireRate.Value;
                }
        }

        protected override void AcquireTarget()
        {
            // 1. Scan for ALL enemies within the tower's range
            var totalEnemiesInRange =
                Physics.OverlapSphereNonAlloc(transform.position, range.Value, _potentialTargetsCache, targetLayer);

            if (totalEnemiesInRange == 0)
            {
                ResetTargeting();
                return;
            }

            var bestClusterCenter = Vector3.zero;
            var maxDensity = -1;
            var foundCluster = false;

            // 2. BRUTE FORCE SCAN: Check density around EVERY enemy found
            // This is the "worse complexity" part (O(N^2)), but independent of managers.
            for (var i = 0; i < totalEnemiesInRange; i++)
            {
                var potentialCenter = _potentialTargetsCache[i];
                if (potentialCenter == null) continue;

                // Count neighbors within packRadius for this specific enemy
                var currentDensity = Physics.OverlapSphereNonAlloc(potentialCenter.transform.position, packRadius,
                    _densityCheckCache, targetLayer);

                if (currentDensity > maxDensity)
                {
                    maxDensity = currentDensity;
                    bestClusterCenter = potentialCenter.transform.position;
                    foundCluster = true;
                }
            }

            if (!foundCluster)
            {
                ResetTargeting();
                return;
            }

            // 3. Calculate Physics/Prediction based on the Best Cluster found
            CalculateClusterProperties(bestClusterCenter);

            // 4. Verify line of fire
            _hasValidTarget = IsPathClear(firePoint.position, _predictedAimPoint, _currentProjectileTravelTime);
        }

        private Vector3 PredictPositionOnPath(NavMeshAgent agent, float timeAhead)
        {
            // Fallback if agent is invalid or has no path
            if (agent == null || !agent.hasPath || agent.path.corners.Length < 2)
            {
                // Default to linear prediction if path is unavailable
                return agent != null 
                    ? agent.transform.position + agent.velocity * timeAhead 
                    : transform.position;
            }

            float speed = agent.velocity.magnitude;
            // If agent is stopped, aim at current position
            if (speed < 0.1f) return agent.transform.position;

            float distanceToTravel = speed * timeAhead;
            Vector3 currentPos = agent.transform.position;
    
            // Iterate through the path corners
            var corners = agent.path.corners;
    
            // Start moving from current position towards corners[1]
            // (corners[0] is the agent's current approximate location on NavMesh)
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Vector3 startSegment = (i == 0) ? currentPos : corners[i];
                Vector3 endSegment = corners[i + 1];
        
                float segmentDist = Vector3.Distance(startSegment, endSegment);

                // If the remaining distance is within this segment, find the exact point
                if (distanceToTravel <= segmentDist)
                {
                    Vector3 direction = (endSegment - startSegment).normalized;
                    return startSegment + direction * distanceToTravel;
                }

                // Otherwise, subtract this segment and continue to the next
                distanceToTravel -= segmentDist;
            }

            // If we run out of path, aim at the final destination
            return corners[corners.Length - 1];
        }
        
        private void CalculateClusterProperties(Vector3 clusterCenter)
        {
            var totalPosition = Vector3.zero;
            var validCount = 0;
    
            // We need to find the agent closest to the centroid to use as our "Pathfinder"
            NavMeshAgent bestAgent = null;
            float closestDistSqr = float.MaxValue;

            // Get the actual members of this best cluster
            var hitCount = Physics.OverlapSphereNonAlloc(clusterCenter, packRadius, _densityCheckCache, targetLayer);

            for (var i = 0; i < hitCount; i++)
            {
                var member = _densityCheckCache[i];
                if (member == null) continue;

                totalPosition += member.transform.position;
                validCount++;

                // Check if this member is a valid NavMeshAgent
                if (member.TryGetComponent<NavMeshAgent>(out var agent))
                {
                    float dSqr = (member.transform.position - clusterCenter).sqrMagnitude;
                    if (dSqr < closestDistSqr)
                    {
                        closestDistSqr = dSqr;
                        bestAgent = agent;
                    }
                }
            }

            if (validCount == 0)
            {
                ResetTargeting();
                return;
            }

            var centroid = totalPosition / validCount;
    
            // Determine Flight Time based on the centroid
            _currentProjectileTravelTime = GetDynamicTravelTime(centroid);

            // --- PREDICTION LOGIC ---
            if (bestAgent != null)
            {
                // Use the NavMesh path of the representative agent
                _predictedAimPoint = PredictPositionOnPath(bestAgent, _currentProjectileTravelTime);
            }
            else
            {
                // Fallback for non-NavMesh enemies (e.g. Rigidbodies)
                // Re-calculate average velocity manually or just use zero if strictly NavMesh is expected
                Vector3 averageVelocity = Vector3.zero; 
                // (You could loop again to get RB velocity if needed, but for this context, let's keep it safe)
        
                _predictedAimPoint = centroid + averageVelocity * _currentProjectileTravelTime;
            }
        }

        private void ResetTargeting()
        {
            _hasValidTarget = false;
            _predictedAimPoint = Vector3.zero;
        }

        protected override void Fire()
        {
            if (!mortarProjectilePrefab) return;

            PlayShootVFX();
            var shell = Instantiate(mortarProjectilePrefab, firePoint.position, firePoint.rotation);
            shell.Initialize(this, radiusOfImpact);

            // Physics Setup
            shell.rigidbody.linearDamping = 0f;
            shell.rigidbody.angularDamping = 0.05f;
            shell.rigidbody.useGravity = true;

            // Calculate Velocity
            shell.rigidbody.linearVelocity = CalculateLaunchVelocity(_predictedAimPoint, _currentProjectileTravelTime);

            // Add Torques for visual effect
            const float torqueStrength = 4f;
            var randomTorque = Random.insideUnitSphere * torqueStrength;
            shell.rigidbody.AddTorque(randomTorque, ForceMode.Impulse);
        }

        // --- Helper Calculation Methods ---

        private float GetDynamicTravelTime(Vector3 targetPosition)
        {
            var distance = Vector3.Distance(transform.position, targetPosition);
            var travelTimeFactor = Mathf.Clamp01(distance / range.Value);
            return Mathf.Lerp(minProjectileTravelTime, maxProjectileTravelTime, travelTimeFactor);
        }

        private Vector3 CalculateLaunchVelocity(Vector3 targetPoint, float time)
        {
            if (time <= 0.001f) return Vector3.zero;
            var displacement = targetPoint - firePoint.position;
            var velocityY = (displacement.y - 0.5f * Physics.gravity.y * (time * time)) / time;
            var velocityX = displacement.x / time;
            var velocityZ = displacement.z / time;
            return new Vector3(velocityX, velocityY, velocityZ);
        }

        private bool IsPathClear(Vector3 startPoint, Vector3 endPoint, float time)
        {
            var launchVelocity = CalculateLaunchVelocity(endPoint, time);
            if (launchVelocity == Vector3.zero) return false;

            var previousPoint = startPoint;
            const int trajectorySteps = 10; // Slightly reduced steps for optimization
            for (var i = 1; i <= trajectorySteps; i++)
            {
                var t = (float)i / trajectorySteps * time;
                var currentPoint = startPoint + launchVelocity * t + Physics.gravity * (0.5f * t * t);

                // Check if blocked by terrain/walls (visionBlockerLayer)
                if (Physics.Linecast(previousPoint, currentPoint, visionBlockerLayer)) return false;

                previousPoint = currentPoint;
            }

            return true;
        }

        protected override bool AimAtTarget(Vector3 aimPoint)
        {
            if (!yPivot || !xPivot) return true;

            // Always aim at _predictedAimPoint calculated in AcquireTarget
            var launchVelocity = CalculateLaunchVelocity(_predictedAimPoint, _currentProjectileTravelTime);
            if (launchVelocity == Vector3.zero) return false;

            // Horizontal Rotation (Y-Pivot)
            var horizontalDirection = new Vector3(launchVelocity.x, 0, launchVelocity.z);
            if (horizontalDirection.sqrMagnitude < 0.001f) horizontalDirection = Vector3.forward;

            var yLookRotation = Quaternion.LookRotation(horizontalDirection);
            yPivot.rotation = Quaternion.RotateTowards(yPivot.rotation, yLookRotation, yPivotSpeed * Time.deltaTime);

            // Vertical Rotation (X-Pivot)
            var localLaunchDirection = yPivot.InverseTransformDirection(launchVelocity);
            if (localLaunchDirection.sqrMagnitude < 0.001f) localLaunchDirection = Vector3.forward;

            var xLookRotation = Quaternion.LookRotation(localLaunchDirection);
            xPivot.localRotation =
                Quaternion.RotateTowards(xPivot.localRotation, xLookRotation, xPivotSpeed * Time.deltaTime);

            // Check alignment
            var yAligned = Quaternion.Angle(yPivot.rotation, yLookRotation) < rotationThreshold;
            var xAligned = Quaternion.Angle(xPivot.localRotation, xLookRotation) < rotationThreshold;
            return yAligned && xAligned;
        }

        protected override void OnDrawGizmosTower()
        {
            if (_hasValidTarget)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_predictedAimPoint, 0.5f);
                Gizmos.DrawLine(firePoint.position, _predictedAimPoint);
            }
        }
    }
}