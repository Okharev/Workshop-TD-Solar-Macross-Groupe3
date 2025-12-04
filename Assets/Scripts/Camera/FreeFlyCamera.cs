using UnityEngine;

namespace Camera
{
    public class FreeFlyCamera : MonoBehaviour
    {
        [Header("Movement Settings")] public float movementSpeed = 10f;

        public float boostMultiplier = 5f;

        [Tooltip("Time in seconds to reach target speed. Lower = Snappier (e.g. 0.1), Higher = Driftier (e.g. 0.5)")]
        public float moveSmoothTime = 0.15f;

        [Header("Look Settings")] public float mouseSensitivity = 2f;

        public bool invertY;

        [Tooltip("Lowest angle the camera can look down")]
        public float minPitchAngle = -60f;

        [Tooltip("Highest angle the camera can look up")]
        public float maxPitchAngle = 60f;

        [Header("Position Constraints")] public bool enableHeightLimit = true;

        public float minHeight;
        public float maxHeight = 100f;

        [Header("Obstacle Avoidance")] public bool autoAvoidObstacles = true;

        public LayerMask obstacleLayers;

        [Tooltip("Minimum height to maintain above the surface found below.")]
        public float heightBuffer = 2.0f;

        [Tooltip("How far ahead (seconds) to check. 0.5 = checks where you will be in 0.5s")]
        public float predictionTime = 0.5f;

        [Tooltip("How fast the camera smooths its vertical rise.")]
        public float climbSmoothing = 4f;

        [Tooltip(
            "How high above the camera the avoidance ray starts. Allows climbing ledges, but ignores high ceilings.")]
        public float rayCastSourceHeight = 3.0f;

        // SmoothDamp Reference Variables
        private Vector3 _currentVelocity; // The actual velocity applied to transform

        // Internal state
        private float _rotationX;
        private float _rotationY;
        private Vector3 _smoothDampVelocityRef; // Internal variable for SmoothDamp math

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

        // --- Debug Visualization ---
        private void OnDrawGizmos()
        {
            if (!autoAvoidObstacles || !Application.isPlaying) return;

            Gizmos.color = Color.yellow;
            var futurePos = transform.position + _currentVelocity * predictionTime;
            var rayOrigin = new Vector3(futurePos.x, transform.position.y + rayCastSourceHeight, futurePos.z);

            // Draw the probe ray
            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * 20f);
            Gizmos.DrawWireSphere(rayOrigin, 0.5f);
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
            // --- 1. Calculate Target Input Velocity ---
            var inputDir = Vector3.zero;
            inputDir += transform.forward * Input.GetAxisRaw("Vertical");
            inputDir += transform.right * Input.GetAxisRaw("Horizontal");
            if (Input.GetKey(KeyCode.E)) inputDir += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) inputDir += Vector3.down;

            // Normalize input so diagonal movement isn't faster
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

            var targetSpeed = movementSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) targetSpeed *= boostMultiplier;

            var targetVelocity = inputDir * targetSpeed;

            // ---  2. SmoothDamp for High-Quality Physics Feel ---
            // SmoothDamp gradually changes _currentVelocity towards targetVelocity
            _currentVelocity = Vector3.SmoothDamp(
                _currentVelocity,
                targetVelocity,
                ref _smoothDampVelocityRef,
                moveSmoothTime
            );

            // Calculate where we are going this frame
            var nextPosition = transform.position + _currentVelocity * Time.unscaledDeltaTime;

            // --- 3. Local Raycast Logic --
            if (autoAvoidObstacles)
            {
                // Predict future position using current velocity
                var futureProbePos = nextPosition + _currentVelocity * predictionTime;

                // ORIGIN: Instead of Y+500, we start at (FutureY + SmallOffset)
                // This checks "Can I step up onto something?" without checking the sky.
                var rayOrigin = new Vector3(futureProbePos.x, nextPosition.y + rayCastSourceHeight, futureProbePos.z);
                var ray = new Ray(rayOrigin, Vector3.down);

                // We only cast down as far as the source height + a bit more to find the floor
                // 100f range is plenty for local ground checks
                if (Physics.Raycast(ray, out var hit, 100f, obstacleLayers))
                {
                    var minSafeY = hit.point.y + heightBuffer;
                    _targetAutoHeight = nextPosition.y < minSafeY ? minSafeY : nextPosition.y;
                }

                // Apply Smooth Lift
                var finalY = Mathf.Lerp(nextPosition.y, Mathf.Max(nextPosition.y, _targetAutoHeight),
                    Time.unscaledDeltaTime * climbSmoothing);

                nextPosition.y = finalY;
            }

            // --- 4. Apply Height Constraints ---
            if (enableHeightLimit) nextPosition.y = Mathf.Clamp(nextPosition.y, minHeight, maxHeight);

            // --- 5. Apply Final Position ---
            transform.position = nextPosition;
        }
    }
}