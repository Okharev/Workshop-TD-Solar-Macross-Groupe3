using System.Collections.Generic;
using System.Linq;
using Enemy;
using Placement;
using UnityEngine;
// Pour EnemyObjectiveTracker

// Pour gérer les listes proprement

[RequireComponent(typeof(EnemyObjectiveTracker))]
public class FighterJetAi : MonoBehaviour
{
    public enum AIState
    {
        Traveling,
        Attacking
    }

    [Header("1. Configuration")] public Transform visualModel;

    public LayerMask allyLayer;

    [Header("2. Boids (The Swarm)")] public float neighborRadius = 10f;

    public float separationRadius = 4f;

    [Range(0, 10)] public float weightTarget = 1f;
    [Range(0, 20)] public float weightSeparation = 12f;

    [Header("3. Flight Physics (LATENCY FIX)")]
    public float baseSpeed = 20f;

    public float turnSpeed = 90f;

    public float maxBankAngle = 60f;

    [Tooltip("Temps de réaction. 0.1 = Rapide/Nerveux. 0.5 = Lent/Paquebot.")]
    public float directionSmoothing = 0.1f;

    [Header("4. Obstacle Avoidance (Whiskers)")]
    public LayerMask obstacleLayer;

    public float whiskerLength = 20f;
    public float whiskerAngle = 35f;
    [Range(0, 100)] public float weightAvoidance = 60f;

    [Header("5. Navigation")] public List<Transform> waypoints;

    private bool _hasBeenInitialized;
    private Vector3 _smoothDampVelocity;

    private EnemyObjectiveTracker _tracker;
    private Vector3 currentMissionTarget;

    // --- Internal State ---
    private AIState currentState = AIState.Traveling;
    private bool orbitClockwise = true;
    private int waypointIndex;

    private void Awake()
    {
        _tracker = GetComponent<EnemyObjectiveTracker>();
    }

    private void Start()
    {
        orbitClockwise = Random.value > 0.5f;

        if (_hasBeenInitialized) return;

        currentMissionTarget = transform.position + transform.forward * 100f;
    }

    private void Update()
    {
        UpdateMissionLogic();
        MoveWithFlocking();
        ApplyVisuals();
    }

    // --- DEBUG ---
    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            // Cible actuelle (Jaune)
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentMissionTarget);

            // Cible finale (Rouge)
            if (currentState == AIState.Attacking && _tracker != null && _tracker.CurrentTarget.Value != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, _tracker.CurrentTarget.Value.position);
            }

            // Whiskers (Cyan)
            Gizmos.color = Color.cyan;
            var rightDir = Quaternion.AngleAxis(whiskerAngle, transform.up) * transform.forward;
            var leftDir = Quaternion.AngleAxis(-whiskerAngle, transform.up) * transform.forward;
            Gizmos.DrawRay(transform.position, transform.forward * whiskerLength);
            Gizmos.DrawRay(transform.position, rightDir * whiskerLength);
            Gizmos.DrawRay(transform.position, leftDir * whiskerLength);
        }
    }

    public void Initialize(List<Transform> pathPoints)
    {
        _hasBeenInitialized = true;

        waypoints = new List<Transform>(pathPoints).Where(t => t != null).ToList();

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

    // --- STEP 1: LOGIQUE DE MISSION ---
    private void UpdateMissionLogic()
    {
        var dist = Vector3.Distance(transform.position, currentMissionTarget);

        switch (currentState)
        {
            case AIState.Traveling:
                // Passage au point suivant
                if (dist < 15f)
                {
                    waypointIndex++;
                    if (waypointIndex < waypoints.Count)
                        currentMissionTarget = waypoints[waypointIndex].position;
                    else
                        currentState = AIState.Attacking;
                }

                break;

            case AIState.Attacking:
                // Récupération de la cible via le système réactif
                var targetTransform = _tracker.CurrentTarget.Value;

                if (targetTransform == null)
                {
                    // Tentative de trouver une cible de secours si le tracker est vide
                    TryFindBackupTarget();
                    targetTransform = _tracker.CurrentTarget.Value;
                }

                if (targetTransform != null)
                {
                    // Orbite d'attaque
                    var dirFromTarget = (transform.position - targetTransform.position).normalized;
                    dirFromTarget.y = 0;
                    var angleOffset = orbitClockwise ? 20f : -20f;
                    var futurePos = Quaternion.Euler(0, angleOffset, 0) * dirFromTarget;

                    // On vise un point autour de la cible
                    currentMissionTarget = targetTransform.position + futurePos * 25f + Vector3.up * 15f;
                }
                else
                {
                    // Voler tout droit si aucune cible n'existe dans le monde
                    currentMissionTarget = transform.position + transform.forward * 100f;
                }

                break;
        }
    }

    private void TryFindBackupTarget()
    {
        var randomObjective = FindFirstObjectByType<DestructibleObjective>();
        if (randomObjective != null) _tracker.Initialize(randomObjective, randomObjective);
    }

    // --- STEP 2: PHYSIQUE & BOIDS ---
    private void MoveWithFlocking()
    {
        // 1. Vecteur de Mission (Où je veux aller)
        var directionToTarget = (currentMissionTarget - transform.position).normalized;
        var finalMoveDirection = directionToTarget * weightTarget;

        // 2. Séparation (Ne pas toucher les copains)
        var separationMove = Vector3.zero;
        var count = 0;
        var neighbors = Physics.OverlapSphere(transform.position, neighborRadius, allyLayer);

        foreach (var c in neighbors)
        {
            if (c.gameObject == gameObject) continue;
            var t = c.transform;
            var pushDir = transform.position - t.position;
            var d = pushDir.magnitude;

            if (d < separationRadius)
            {
                // Force exponentielle quand on est très proche
                var force = Mathf.Clamp(1.0f / (d * d + 0.001f), 0f, 10f);
                separationMove += pushDir.normalized * force;
            }

            count++;
        }

        if (count > 0) finalMoveDirection += separationMove * weightSeparation;

        // 3. Évitement d'Obstacle (Whiskers) - PRIORITÉ ABSOLUE
        var avoidanceMove = GetObstacleAvoidanceVector();
        if (avoidanceMove != Vector3.zero)
            // On ajoute une force massive pour contrer tout le reste
            finalMoveDirection += avoidanceMove * weightAvoidance;

        // 4. Application du mouvement
        // On lisse l'entrée pour éviter les tremblements, mais avec un temps très court (0.1s)
        var smoothedDirection = Vector3.SmoothDamp(transform.forward, finalMoveDirection.normalized,
            ref _smoothDampVelocity, directionSmoothing);

        if (smoothedDirection != Vector3.zero)
        {
            var targetRot = Quaternion.LookRotation(smoothedDirection);
            // Rotation rapide vers la nouvelle direction calculée
            transform.rotation =
                Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime * 50f);
        }

        // Vitesse variable (plus lent si on monte, plus vite si on descend)
        var speedMod = baseSpeed * (1f - transform.forward.y * 0.4f);
        transform.Translate(Vector3.forward * speedMod * Time.deltaTime);

        // Pas de côté d'urgence (Strafe) si on est collé à un autre avion
        if (separationMove.magnitude > 0.5f)
        {
            var strafe = Vector3.ClampMagnitude(separationMove, 5f);
            transform.position += strafe * Time.deltaTime * 3f;
        }
    }

    // --- STEP 2.5: WHISKERS LOGIC ---
    private Vector3 GetObstacleAvoidanceVector()
    {
        var avoidance = Vector3.zero;
        RaycastHit hit;

        // Centre
        if (Physics.Raycast(transform.position, transform.forward, out hit, whiskerLength, obstacleLayer))
            avoidance += hit.normal;

        // Droite
        var rightDir = Quaternion.AngleAxis(whiskerAngle, transform.up) * transform.forward;
        if (Physics.Raycast(transform.position, rightDir, out hit, whiskerLength, obstacleLayer))
            avoidance += hit.normal;

        // Gauche
        var leftDir = Quaternion.AngleAxis(-whiskerAngle, transform.up) * transform.forward;
        if (Physics.Raycast(transform.position, leftDir, out hit, whiskerLength, obstacleLayer))
            avoidance += hit.normal;

        return avoidance; // Retourne la normale moyenne des obstacles touchés
    }

    // --- STEP 3: VISUALS ---
    private void ApplyVisuals()
    {
        if (visualModel == null) return;

        // Turbulences légères
        var nX = (Mathf.PerlinNoise(Time.time * 2f, 0) - 0.5f) * 0.5f;
        var nY = (Mathf.PerlinNoise(0, Time.time * 2f) - 0.5f) * 0.5f;
        visualModel.localPosition = new Vector3(nX, nY, 0);

        // Inclinaison (Roll) dans les virages
        var localTarget = transform.InverseTransformPoint(currentMissionTarget);
        var targetRoll = Mathf.Clamp(-localTarget.x * maxBankAngle, -maxBankAngle, maxBankAngle);

        visualModel.localRotation = Quaternion.Slerp(visualModel.localRotation, Quaternion.Euler(0, 0, targetRoll),
            Time.deltaTime * 3f);
    }
}