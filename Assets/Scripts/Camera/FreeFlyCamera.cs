using UnityEngine;

namespace Camera
{
    public sealed class FreeFlyCamera : MonoBehaviour
    {
        [Header("Movement Settings")] 
        public float movementSpeed = 10f;
        public float boostMultiplier = 5f;

        [Tooltip("Temps pour atteindre la vitesse cible. Plus bas = plus réactif.")]
        public float moveSmoothTime = 0.15f;

        [Header("Zoom Settings")]
        [Tooltip("Vitesse du zoom avec la molette.")]
        public float scrollSensitivity = 150f; 

        [Header("Look Settings")] 
        public float mouseSensitivity = 2f;
        public bool invertY;

        [Tooltip("Angle minimum pour regarder vers le bas")]
        public float minPitchAngle = -60f;

        [Tooltip("Angle maximum pour regarder vers le haut")]
        public float maxPitchAngle = 60f;

        [Header("Position Constraints")] 
        public bool enableHeightLimit = true;
        public float minHeight;
        public float maxHeight = 100f;

        [Header("Obstacle Avoidance")] 
        public bool autoAvoidObstacles = true;
        public LayerMask obstacleLayers;

        [Tooltip("Hauteur minimum à maintenir au-dessus du sol.")]
        public float heightBuffer = 2.0f;
        public float predictionTime = 0.5f;
        public float climbSmoothing = 4f;
        public float rayCastSourceHeight = 3.0f;

        // SmoothDamp Reference Variables
        private Vector3 _currentVelocity; 
        private float _rotationX;
        private float _rotationY;
        private Vector3 _smoothDampVelocityRef; 
        private float _targetAutoHeight = -9999f;

        private void Start()
        {
            var rot = transform.localRotation.eulerAngles;
            _rotationY = rot.y;
            _rotationX = rot.x;
            _targetAutoHeight = transform.position.y;
        }

        private void Update()
        {
            HandleMouseLook();
            HandleMovementAndAvoidance();
        }

        private void OnDrawGizmos()
        {
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
            // --- 1. Get planar direction ---
            
            var forward = transform.forward;
            var right = transform.right;

            // On écrase la composante Y à 0 pour garder le mouvement plat
            forward.y = 0f;
            right.y = 0f;

            // Normize to avoid faster diagonal moves
            forward.Normalize();
            right.Normalize();

            var inputDir = Vector3.zero;
            inputDir += forward * Input.GetAxisRaw("Vertical");
            inputDir += right * Input.GetAxisRaw("Horizontal");

            // --- Gestion du Zoom (Molette) ---
            // On récupère le scroll de la souris pour l'axe Y
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            
            // Use scroll speed 
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

            // --- 3. Obstacle avoidance ---
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