using UnityEngine;
using System.Collections.Generic;
using Pathing.Gameplay; // Pour EnemyObjectiveTracker
using System.Linq;      // Pour gérer les listes proprement

[RequireComponent(typeof(EnemyObjectiveTracker))]
public class FighterJetAi : MonoBehaviour
{
    public enum AIState { Traveling, Attacking }

    [Header("1. Configuration")]
    public Transform visualModel; 
    public LayerMask allyLayer;   

    [Header("2. Boids (The Swarm)")]
    public float neighborRadius = 10f;
    public float separationRadius = 4f;
    
    [Range(0, 10)] public float weightTarget = 1f;     
    [Range(0, 20)] public float weightSeparation = 12f; // Augmenté pour plus de réactivité entre eux

    [Header("3. Flight Physics (LATENCY FIX)")]
    public float baseSpeed = 20f;
    
    // AUGMENTÉ : 90 permet des virages serrés. Si c'est trop bas (ex: 20), l'avion voit le mur mais ne tourne pas assez vite.
    public float turnSpeed = 90f;       
    
    public float maxBankAngle = 60f;

    // RÉDUIT : 0.1f rend l'avion "nerveux" et réactif. 0.3f ou plus le rend "lourd" et en retard.
    [Tooltip("Temps de réaction. 0.1 = Rapide/Nerveux. 0.5 = Lent/Paquebot.")]
    public float directionSmoothing = 0.1f; 

    [Header("4. Obstacle Avoidance (Whiskers)")]
    public LayerMask obstacleLayer; // Assurez-vous que ce n'est PAS le layer des ennemis !
    public float whiskerLength = 20f; // Assez long pour voir venir le mur à haute vitesse
    public float whiskerAngle = 35f;  
    [Range(0, 100)] public float weightAvoidance = 60f; // DOIT être très élevé pour surclasser la cible

    [Header("5. Navigation")]
    public List<Transform> waypoints;

    // --- Internal State ---
    private AIState currentState = AIState.Traveling;
    private Vector3 currentMissionTarget; 
    private int waypointIndex = 0;
    private bool orbitClockwise = true;
    
    private EnemyObjectiveTracker _tracker;
    private Vector3 _smoothDampVelocity;
    private bool _hasBeenInitialized = false; // Flag pour protéger l'initialisation

    void Awake()
    {
        _tracker = GetComponent<EnemyObjectiveTracker>();
    }

    void Start()
    {
        orbitClockwise = Random.value > 0.5f;

        // Protection : Si Initialize() a déjà tourné, on ne touche à rien
        if (_hasBeenInitialized) return;

        // Fallback pour les tests manuels dans la scène
        currentMissionTarget = transform.position + transform.forward * 100f;
    }

    // Appelé par AirPath.Spawn
    public void Initialize(List<Transform> pathPoints)
    {
        _hasBeenInitialized = true;
        
        // Nettoyage de la liste
        this.waypoints = new List<Transform>(pathPoints).Where(t => t != null).ToList();

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
    
    void Update()
    {
        UpdateMissionLogic();
        MoveWithFlocking();
        ApplyVisuals();
    }

    // --- STEP 1: LOGIQUE DE MISSION ---
    void UpdateMissionLogic()
    {
        float dist = Vector3.Distance(transform.position, currentMissionTarget);

        switch (currentState)
        {
            case AIState.Traveling:
                // Passage au point suivant
                if (dist < 15f) 
                {
                    waypointIndex++;
                    if (waypointIndex < waypoints.Count)
                    {
                        currentMissionTarget = waypoints[waypointIndex].position;
                    }
                    else
                    {
                        currentState = AIState.Attacking;
                    }
                }
                break;

            case AIState.Attacking:
                // Récupération de la cible via le système réactif
                Transform targetTransform = _tracker.CurrentTarget.Value;

                if (targetTransform == null)
                {
                    // Tentative de trouver une cible de secours si le tracker est vide
                    TryFindBackupTarget();
                    targetTransform = _tracker.CurrentTarget.Value;
                }

                if (targetTransform != null)
                {
                    // Orbite d'attaque
                    Vector3 dirFromTarget = (transform.position - targetTransform.position).normalized;
                    dirFromTarget.y = 0; 
                    float angleOffset = orbitClockwise ? 20f : -20f;
                    Vector3 futurePos = Quaternion.Euler(0, angleOffset, 0) * dirFromTarget;
                    
                    // On vise un point autour de la cible
                    currentMissionTarget = targetTransform.position + (futurePos * 25f) + (Vector3.up * 15f);
                }
                else
                {
                    // Voler tout droit si aucune cible n'existe dans le monde
                    currentMissionTarget = transform.position + transform.forward * 100f;
                }
                break;
        }
    }

    void TryFindBackupTarget()
    {
        var randomObjective = FindFirstObjectByType<DestructibleObjective>();
        if (randomObjective != null)
        {
            _tracker.Initialize(randomObjective, randomObjective);
        }
    }

    // --- STEP 2: PHYSIQUE & BOIDS ---
    void MoveWithFlocking()
    {
        // 1. Vecteur de Mission (Où je veux aller)
        Vector3 directionToTarget = (currentMissionTarget - transform.position).normalized;
        Vector3 finalMoveDirection = directionToTarget * weightTarget;

        // 2. Séparation (Ne pas toucher les copains)
        Vector3 separationMove = Vector3.zero;
        int count = 0;
        Collider[] neighbors = Physics.OverlapSphere(transform.position, neighborRadius, allyLayer);

        foreach (Collider c in neighbors)
        {
            if (c.gameObject == gameObject) continue;
            Transform t = c.transform;
            Vector3 pushDir = transform.position - t.position;
            float d = pushDir.magnitude;
            
            if (d < separationRadius)
            {
                // Force exponentielle quand on est très proche
                float force = Mathf.Clamp(1.0f / (d * d + 0.001f), 0f, 10f); 
                separationMove += pushDir.normalized * force; 
            }
            count++;
        }
        if (count > 0) finalMoveDirection += separationMove * weightSeparation;

        // 3. Évitement d'Obstacle (Whiskers) - PRIORITÉ ABSOLUE
        Vector3 avoidanceMove = GetObstacleAvoidanceVector();
        if (avoidanceMove != Vector3.zero)
        {
            // On ajoute une force massive pour contrer tout le reste
            finalMoveDirection += avoidanceMove * weightAvoidance;
        }

        // 4. Application du mouvement
        // On lisse l'entrée pour éviter les tremblements, mais avec un temps très court (0.1s)
        Vector3 smoothedDirection = Vector3.SmoothDamp(transform.forward, finalMoveDirection.normalized, ref _smoothDampVelocity, directionSmoothing);

        if (smoothedDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(smoothedDirection);
            // Rotation rapide vers la nouvelle direction calculée
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime * 50f);
        }

        // Vitesse variable (plus lent si on monte, plus vite si on descend)
        float speedMod = baseSpeed * (1f - (transform.forward.y * 0.4f));
        transform.Translate(Vector3.forward * speedMod * Time.deltaTime);

        // Pas de côté d'urgence (Strafe) si on est collé à un autre avion
        if (separationMove.magnitude > 0.5f)
        {
            Vector3 strafe = Vector3.ClampMagnitude(separationMove, 5f);
            transform.position += strafe * Time.deltaTime * 3f;
        }
    }

    // --- STEP 2.5: WHISKERS LOGIC ---
    Vector3 GetObstacleAvoidanceVector()
    {
        Vector3 avoidance = Vector3.zero;
        RaycastHit hit;

        // Centre
        if (Physics.Raycast(transform.position, transform.forward, out hit, whiskerLength, obstacleLayer))
            avoidance += hit.normal;

        // Droite
        Vector3 rightDir = Quaternion.AngleAxis(whiskerAngle, transform.up) * transform.forward;
        if (Physics.Raycast(transform.position, rightDir, out hit, whiskerLength, obstacleLayer))
            avoidance += hit.normal;

        // Gauche
        Vector3 leftDir = Quaternion.AngleAxis(-whiskerAngle, transform.up) * transform.forward;
        if (Physics.Raycast(transform.position, leftDir, out hit, whiskerLength, obstacleLayer))
            avoidance += hit.normal;

        return avoidance; // Retourne la normale moyenne des obstacles touchés
    }

    // --- STEP 3: VISUALS ---
    void ApplyVisuals()
    {
        if (visualModel == null) return;

        // Turbulences légères
        float nX = (Mathf.PerlinNoise(Time.time * 2f, 0) - 0.5f) * 0.5f;
        float nY = (Mathf.PerlinNoise(0, Time.time * 2f) - 0.5f) * 0.5f;
        visualModel.localPosition = new Vector3(nX, nY, 0);

        // Inclinaison (Roll) dans les virages
        Vector3 localTarget = transform.InverseTransformPoint(currentMissionTarget);
        float targetRoll = Mathf.Clamp(-localTarget.x * maxBankAngle, -maxBankAngle, maxBankAngle);
        
        visualModel.localRotation = Quaternion.Slerp(visualModel.localRotation, Quaternion.Euler(0, 0, targetRoll), Time.deltaTime * 3f);
    }

    // --- DEBUG ---
    void OnDrawGizmos()
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
            Vector3 rightDir = Quaternion.AngleAxis(whiskerAngle, transform.up) * transform.forward;
            Vector3 leftDir = Quaternion.AngleAxis(-whiskerAngle, transform.up) * transform.forward;
            Gizmos.DrawRay(transform.position, transform.forward * whiskerLength);
            Gizmos.DrawRay(transform.position, rightDir * whiskerLength);
            Gizmos.DrawRay(transform.position, leftDir * whiskerLength);
        }
    }
}