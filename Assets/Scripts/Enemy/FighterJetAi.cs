using System.Collections.Generic;
using System.Linq;
using Enemy;
using Placement;
using UnityEngine;

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
    [Range(0, 5)] public float weightTarget = 1.2f; // Increased slightly for responsiveness
    [Range(0, 30)] public float weightSeparation = 25f; 

    [Header("3. Flight Aerodynamics")]
    public float maxThrustSpeed = 30f;
    public float minStallSpeed = 10f;
    public float acceleration = 10f;
    
    [Header("Turn Performance")]
    [Tooltip("How fast the plane rolls to enter a turn.")]
    public float rollSpeed = 180f; 
    [Tooltip("How tight the plane can turn (Pitch authority).")]
    public float maxTurnRate = 50f; 
    [Tooltip("0 = Arcade (turn in place), 1 = Simulation (drifts like a real jet).")]
    [Range(0f, 1f)] public float driftFactor = 0.95f;

    [Header("4. Obstacle Avoidance")]
    public LayerMask obstacleLayer;
    public float whiskerLength = 40f; // Increased for higher speeds
    [Range(0, 100)] public float weightAvoidance = 100f;

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

    private void Awake()
    {
        _tracker = GetComponent<EnemyObjectiveTracker>();
        _currentSpeed = maxThrustSpeed * 0.8f;
        _velocity = transform.forward * _currentSpeed;
    }

    private void Start()
    {
        orbitClockwise = Random.value > 0.5f;
        if (currentMissionTarget == Vector3.zero) 
            currentMissionTarget = transform.position + transform.forward * 100f;
    }

    private void Update()
    {
        UpdateMissionLogic();
        CalculateAerodynamics();
        UpdateVisualModel();
    }

    // --- PUBLIC METHODS ---
    public void Initialize(List<Transform> pathPoints)
    {
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

    // --- LOGIC ---
    private void UpdateMissionLogic()
    {
        float dist = Vector3.Distance(transform.position, currentMissionTarget);

        switch (currentState)
        {
            case AIState.Traveling:
                // Loose waypoint switching for smoother curves
                if (dist < 30f) 
                {
                    waypointIndex = (waypointIndex + 1) % waypoints.Count;
                    currentMissionTarget = waypoints[waypointIndex].position;
                }
                break;

            case AIState.Attacking:
                var targetTransform = _tracker.CurrentTarget.Value;
                
                if (!targetTransform)
                {
                    TryFindBackupTarget();
                    targetTransform = _tracker.CurrentTarget.Value;
                }

                if (targetTransform)
                {
                    // Predictive Aiming: Don't aim at the target, aim where it's going (basic lead)
                    Vector3 targetPos = targetTransform.position;
                    
                    // Simple Orbit Logic
                    Vector3 orbitCenter = targetPos;
                    Vector3 dirFromCenter = (transform.position - orbitCenter).normalized;
                    dirFromCenter.y = 0; 
                    
                    Vector3 tangent = Vector3.Cross(dirFromCenter, Vector3.up);
                    if (!orbitClockwise) tangent = -tangent;

                    // Lead the turn
                    Vector3 attackPoint = orbitCenter + (tangent * 50f) + (Vector3.up * 10f);
                    currentMissionTarget = Vector3.Lerp(currentMissionTarget, attackPoint, Time.deltaTime * 3f);
                }
                else
                {
                    currentMissionTarget = transform.position + transform.forward * 100f;
                }
                break;
        }
    }

    private void TryFindBackupTarget()
    {
        var randomObjective = FindFirstObjectByType<DestructibleObjective>();
        if (randomObjective) _tracker.Initialize(randomObjective, randomObjective);
    }

    // --- PHYSICS CORE (The "Juice") ---
    private void CalculateAerodynamics()
    {
        // 1. Determine Desired Heading Vector
        Vector3 flockingDir = CalculateFlockingVector();
        
        // 2. Flight Control System (Bank-to-Turn Logic)
        
        // Calculate the rotation needed to face the desired vector
        Quaternion currentRot = transform.rotation;
        Quaternion targetRot = Quaternion.LookRotation(flockingDir);
        
        // Convert target direction to local space
        // x = Yaw/Roll demand, y = Pitch demand
        Vector3 localTargetDir = transform.InverseTransformDirection(flockingDir);

        // ROLL: If we need to go Left (-x), we Roll Left.
        float targetRollAngle = -localTargetDir.x * 60f; // Limit max bank to 60 degrees relative to turn strength
        
        // PITCH: We pull up if target is above, OR if we are banked and need to turn tight.
        // The more we are banked, the more "Pitch" acts as "Turn".
        float yawToPitchTransfer = Mathf.Abs(localTargetDir.x); 
        float targetPitchInput = localTargetDir.y + yawToPitchTransfer; 

        // Apply Rotations over time (Inertia)
        // Rotate towards the target rotation, but constrained by roll speed and turn rate
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, maxTurnRate * Time.deltaTime);

        // BANKING override: 
        // We fundamentally want to orient the Z-axis (roll) to "lean" into the turn
        // This calculates a rotation that looks forward but leans correctly
        Vector3 flatForward = transform.forward; flatForward.y = 0;
        if(flatForward.sqrMagnitude > 0.01f)
        {
             // Smoothly apply the bank angle calculated above
             float currentRoll = NormalizeAngle(transform.eulerAngles.z);
             float newRoll = Mathf.LerpAngle(currentRoll, targetRollAngle * 1.5f, Time.deltaTime * 2f);
             
             // Re-apply rotation with calculated roll
             Vector3 euler = transform.rotation.eulerAngles;
             transform.rotation = Quaternion.Euler(euler.x, euler.y, newRoll);
        }

        // 3. Throttle & Energy Management
        // Gravity boost: Diving (+Y down) increases speed. Climbing decreases it.
        float gravityBoost = -transform.forward.y * 10f;
        
        // Drag from turning (Induced Drag): High Angle of Attack kills speed
        float turnDrag = Vector3.Angle(transform.forward, _velocity.normalized) * 0.1f;
        
        float targetSpeed = maxThrustSpeed + gravityBoost - turnDrag;
        targetSpeed = Mathf.Clamp(targetSpeed, minStallSpeed, maxThrustSpeed * 1.5f);

        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * acceleration);

        // 4. Velocity vector application (The "Drift")
        // We blend the actual physical velocity towards the nose direction based on 'driftFactor'
        // High drift factor = lots of sliding (space ship). Low drift factor = rails.
        Vector3 noseVelocity = transform.forward * _currentSpeed;
        _velocity = Vector3.Lerp(_velocity, noseVelocity, Time.deltaTime * (1f - driftFactor) * 5f);
        
        // Apply Move
        transform.position += _velocity * Time.deltaTime;
    }

    private Vector3 CalculateFlockingVector()
    {
        Vector3 targetDir = (currentMissionTarget - transform.position).normalized * weightTarget;
        Vector3 separation = Vector3.zero;
        Vector3 avoidance = GetObstacleAvoidanceVector();

        // 1. Critical Avoidance (Overrides everything)
        if (avoidance != Vector3.zero) return avoidance;

        // 2. Swarm Separation
        int count = 0;
        Collider[] neighbors = Physics.OverlapSphere(transform.position, neighborRadius, allyLayer);
        foreach (var c in neighbors)
        {
            if (c.gameObject == gameObject) continue;
            Vector3 diff = transform.position - c.transform.position;
            float distSqr = diff.sqrMagnitude;
            if (distSqr < separationRadius * separationRadius)
            {
                separation += diff.normalized * (separationRadius / Mathf.Sqrt(distSqr));
                count++;
            }
        }
        if (count > 0) separation /= count;

        // 3. Floor Avoidance (Don't crash into ground)
        Vector3 floorPush = Vector3.zero;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 10f, obstacleLayer))
        {
             floorPush = Vector3.up * ((10f - hit.distance) * 2f);
        }

        return (targetDir + (separation * weightSeparation) + floorPush).normalized;
    }

    private Vector3 GetObstacleAvoidanceVector()
    {
        if (Physics.SphereCast(transform.position, 3f, transform.forward, out RaycastHit hit, whiskerLength, obstacleLayer))
        {
            // Debug.DrawLine(transform.position, hit.point, Color.red);
            return Vector3.Reflect(transform.forward, hit.normal).normalized * weightAvoidance;
        }
        return Vector3.zero;
    }

    private void UpdateVisualModel()
    {
        // Visual Model Correction
        // We add a tiny bit of noise for turbulence, but the main banking is now handled by the physics transform
        float time = Time.time * 1.5f;
        float noiseX = (Mathf.PerlinNoise(time, 0) - 0.5f) * 2f;
        float noiseY = (Mathf.PerlinNoise(0, time) - 0.5f) * 2f;

        Quaternion noiseRot = Quaternion.Euler(noiseX, noiseY, 0);
        visualModel.localRotation = Quaternion.Lerp(visualModel.localRotation, Quaternion.Euler(modelCorrection) * noiseRot, Time.deltaTime * 10f);
    }
    
    private float NormalizeAngle(float a) => (a + 180) % 360 - 180;

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentMissionTarget);
            
            Gizmos.color = Color.green; // Velocity (Path)
            Gizmos.DrawRay(transform.position, _velocity.normalized * 10f);
            
            Gizmos.color = Color.red; // Nose (Aim)
            Gizmos.DrawRay(transform.position, transform.forward * 8f);
        }
    }
}