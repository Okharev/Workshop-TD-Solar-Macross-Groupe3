using System.Collections;
using Economy;
using UnityEngine;

namespace Camera
{
    public sealed class FreeFlyCamera : MonoBehaviour
    {
        [Header("Cinematic Entry")]
        [Tooltip("Si vrai, lance la transition au démarrage.")]
        public bool playIntroOnStart = true;

        [Tooltip("L'objet cible où la caméra doit atterrir (La position du Joueur).")]
        public Transform playerStartPoint;

        [Tooltip("Combien de temps attendre avant de commencer à descendre.")]
        public float startDelay = 2.0f; 

        [Tooltip("La durée du voyage.")]
        public float introDuration = 3.0f;

        [Tooltip("La courbe de vitesse.")]
        public AnimationCurve introCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Movement Settings")] 
        public float movementSpeed = 10f;
        public float boostMultiplier = 5f;
        public float moveSmoothTime = 0.15f;

        [Header("Zoom Settings")]
        public float scrollSensitivity = 150f; 

        [Header("Look Settings")] 
        public float mouseSensitivity = 2f;
        public bool invertY;
        public float minPitchAngle = -60f;
        public float maxPitchAngle = 60f;

        [Header("Position Constraints")] 
        public bool enableHeightLimit = true;
        public float minHeight;
        public float maxHeight = 100f;

        [Header("Obstacle Avoidance")] 
        public bool autoAvoidObstacles = true;
        public LayerMask obstacleLayers;
        public float heightBuffer = 2.0f;
        public float predictionTime = 0.5f;
        public float climbSmoothing = 4f;
        public float rayCastSourceHeight = 3.0f;

        // Variables internes
        private Vector3 _currentVelocity; 
        private float _rotationX;
        private float _rotationY;
        private Vector3 _smoothDampVelocityRef; 
        private float _targetAutoHeight = -9999f;
        
        // Bloque les contrôles pendant l'intro
        private bool _isLocked = false; 

        private void Start()
        {
            EnergyHeatmapSystem.Instance.ToggleHeatmap(false);
            
            // Initialisation des rotations (au cas où l'intro est désactivée)
            var rot = transform.localRotation.eulerAngles;
            _rotationY = rot.y;
            _rotationX = rot.x;
            _targetAutoHeight = transform.position.y;

            // Lancement de l'intro
            if (playIntroOnStart && playerStartPoint != null)
            {
                StartCoroutine(PlayCinematicEntry());
            }
        }

        private void Update()
        {
            // Si on est en mode cinématique, on coupe les contrôles
            if (_isLocked) return;

            HandleMouseLook();
            HandleMovementAndAvoidance();
        }

        public IEnumerator PlayCinematicEntry()
        {
            _isLocked = true; // 1. On verrouille tout de suite

            // 2. Le Délai : On attend un peu en haut avant de bouger
            yield return new WaitForSeconds(startDelay);

            // --- Configuration du trajet ---
            
            // Départ = Position actuelle de la caméra (En hauteur)
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            // Arrivée = Le point du joueur
            Vector3 endPos = playerStartPoint.position;
            Quaternion endRot = playerStartPoint.rotation;

            float timer = 0f;

            // 3. La boucle d'animation
            while (timer < 1f)
            {
                timer += Time.deltaTime / introDuration;
                float t = introCurve.Evaluate(timer);

                transform.position = Vector3.Lerp(startPos, endPos, t);
                transform.rotation = Quaternion.Slerp(startRot, endRot, t);

                yield return null;
            }

            // 4. Finalisation (Snap final pour être précis)
            transform.position = endPos;
            transform.rotation = endRot;

            // 5. Synchronisation des axes de souris pour éviter les sauts
            // On récupère la rotation de l'arrivée (PlayerStartPoint)
            var finalEuler = playerStartPoint.eulerAngles;
            _rotationY = finalEuler.y;
            _rotationX = finalEuler.x;
            
            // Correction d'angle pour Unity (0..360 -> -180..180)
            if (_rotationX > 180) _rotationX -= 360; 
            
            // Reset de la vélocité
            _currentVelocity = Vector3.zero; 
            _smoothDampVelocityRef = Vector3.zero;

            _isLocked = false; // 6. On libère le joueur
        }

        // --- Le reste du code reste identique ---
        
        private void OnDrawGizmos()
        {
            // On dessine une ligne verte vers le point d'arrivée
            if (playerStartPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, playerStartPoint.position);
                Gizmos.DrawWireSphere(playerStartPoint.position, 0.5f);
            }

            if (!autoAvoidObstacles || !Application.isPlaying) return;

            Gizmos.color = Color.yellow;
            var futurePos = transform.position + _currentVelocity * predictionTime;
            var rayOrigin = new Vector3(futurePos.x, transform.position.y + rayCastSourceHeight, futurePos.z);

            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * 20f);
            Gizmos.DrawWireSphere(rayOrigin, 0.5f);
        }
        
        public void TeleportTo(Vector3 targetPosition)
        {
            targetPosition.y = transform.position.y;
            transform.position = targetPosition;
            _currentVelocity = Vector3.zero; 
            _smoothDampVelocityRef = Vector3.zero;
        }

        private void HandleMouseLook()
        {
            if (Input.GetMouseButton(1))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                var mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                var mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

                _rotationY += mouseX;
                _rotationX += invertY ? mouseY : -mouseY;
                _rotationX = Mathf.Clamp(_rotationX, minPitchAngle, maxPitchAngle);

                transform.localRotation = Quaternion.Euler(_rotationX, _rotationY, 0f);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleMovementAndAvoidance()
        {
            var forward = transform.forward;
            var right = transform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            var inputDir = Vector3.zero;
            inputDir += forward * Input.GetAxisRaw("Vertical");
            inputDir += right * Input.GetAxisRaw("Horizontal");

            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            Vector3 verticalMove = -(Vector3.up * (scrollInput * scrollSensitivity));

            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

            var targetSpeed = movementSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) targetSpeed *= boostMultiplier;

            var targetVelocity = (inputDir * targetSpeed) + verticalMove;

            _currentVelocity = Vector3.SmoothDamp(
                _currentVelocity,
                targetVelocity,
                ref _smoothDampVelocityRef,
                moveSmoothTime
            );

            var nextPosition = transform.position + _currentVelocity * Time.unscaledDeltaTime;

            if (autoAvoidObstacles)
            {
                var futureProbePos = nextPosition + _currentVelocity * predictionTime;
                var rayOrigin = new Vector3(futureProbePos.x, nextPosition.y + rayCastSourceHeight, futureProbePos.z);
                var ray = new Ray(rayOrigin, Vector3.down);

                if (Physics.Raycast(ray, out var hit, 100f, obstacleLayers))
                {
                    var minSafeY = hit.point.y + heightBuffer;
                    _targetAutoHeight = nextPosition.y < minSafeY ? minSafeY : nextPosition.y;
                }

                var finalY = Mathf.Lerp(nextPosition.y, Mathf.Max(nextPosition.y, _targetAutoHeight),
                    Time.unscaledDeltaTime * climbSmoothing);

                nextPosition.y = finalY;
            }

            if (enableHeightLimit) nextPosition.y = Mathf.Clamp(nextPosition.y, minHeight, maxHeight);

            transform.position = nextPosition;
        }
    }
}