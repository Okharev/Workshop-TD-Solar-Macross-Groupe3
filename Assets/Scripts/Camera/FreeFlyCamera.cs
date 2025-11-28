using UnityEngine;

namespace Camera
{
    public class FreeFlyCamera : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float movementSpeed = 10f;
        public float boostMultiplier = 5f;
        [Tooltip("How quickly the camera stops. Higher = snappier, Lower = driftier.")]
        public float movementSmoothness = 5f;

        [Header("Look Settings")]
        public float mouseSensitivity = 2f;
        public bool invertY = false;

        [Header("Obstacle Avoidance")]
        [Tooltip("Enable to make camera float over objects automatically")]
        public bool autoAvoidObstacles = true;
    
        [Tooltip("Which layers count as obstacles (e.g., Default, Ground)")]
        public LayerMask obstacleLayers;
    
        [Tooltip("How high above the ground to maintain the camera")]
        public float heightBuffer = 2.0f;
    
        [Tooltip("How far ahead (in seconds of movement) to check for obstacles. Higher = smoother reaction.")]
        public float predictionTime = 0.5f;

        [Tooltip("How fast the camera smooths its vertical rise to avoid objects")]
        public float climbSmoothing = 4f;

        // Internal state
        private float rotationX = 0f;
        private float rotationY = 0f;
        private Vector3 currentDirVelocity = Vector3.zero;
    
        // This variable helps smooth the forced height adjustment
        private float targetAutoHeight = -9999f; 

        private void Start()
        {
            Vector3 rot = transform.localRotation.eulerAngles;
            rotationY = rot.y;
            rotationX = rot.x;
        
            // Initialize target height to current height
            targetAutoHeight = transform.position.y;
        }

        private void Update()
        {
            HandleMouseLook();
            HandleMovementAndAvoidance();
        }

        private void HandleMouseLook()
        {
            if (Input.GetMouseButton(1)) 
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

                rotationY += mouseX;
                rotationX += invertY ? mouseY : -mouseY;
                rotationX = Mathf.Clamp(rotationX, -90f, 90f);

                transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleMovementAndAvoidance()
        {
            // --- 1. Calculate Input Direction ---
            Vector3 targetInput = Vector3.zero;
            targetInput += transform.forward * Input.GetAxisRaw("Vertical");
            targetInput += transform.right * Input.GetAxisRaw("Horizontal");
            if (Input.GetKey(KeyCode.E)) targetInput += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) targetInput += Vector3.down;

            float currentSpeed = movementSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) currentSpeed *= boostMultiplier;

            // --- 2. Smooth Velocity (The "Drift" Logic) ---
            currentDirVelocity = Vector3.Lerp(currentDirVelocity, targetInput, Time.unscaledDeltaTime * movementSmoothness);
        
            // Calculate the position we WOULD be at if we just moved normally
            Vector3 nextPosition = transform.position + (currentDirVelocity * currentSpeed * Time.unscaledDeltaTime);

            // --- 3. Predictive Obstacle Avoidance ---
            if (autoAvoidObstacles)
            {
                // Predict further ahead based on speed and predictionTime
                // This creates the "Probe" that checks the hill before you hit it
                Vector3 futureProbePos = nextPosition + (currentDirVelocity * (currentSpeed * predictionTime));

                // We cast from the sky downwards at that future position
                // Origin: Future X/Z, but High Up Y
                Vector3 rayOrigin = new Vector3(futureProbePos.x, nextPosition.y + 500f, futureProbePos.z);
                Ray ray = new Ray(rayOrigin, Vector3.down);
                RaycastHit hit;

                // Check if there is ground below the future point
                if (Physics.Raycast(ray, out hit, 1000f, obstacleLayers))
                {
                    // The minimum Y we want to be at is Ground + Buffer
                    float minSafeY = hit.point.y + heightBuffer;

                    // If our next intended position is too low...
                    if (nextPosition.y < minSafeY)
                    {
                        // We update the target height. 
                        // Note: We don't snap nextPosition.y immediately. We store the GOAL.
                        targetAutoHeight = minSafeY;
                    }
                    else
                    {
                        // If we are flying high enough, target height is just where we are (no force up)
                        // But we allow falling down to the buffer if gravity was a thing (it's not here).
                        // So we just reset target to current to stop lifting.
                        targetAutoHeight = nextPosition.y; 
                    }
                }

                // --- 4. Apply Smooth Lift ---
                // If the avoidance says "Go Higher", we smoothly Lerp the Y axis only
                // Mathf.Max ensures we never go BELOW the avoidance height, but user can fly HIGHER if they want (E key)
                float finalY = Mathf.Lerp(nextPosition.y, Mathf.Max(nextPosition.y, targetAutoHeight), Time.unscaledDeltaTime * climbSmoothing);
            
                // Apply to the next position
                nextPosition.y = finalY;
            }

            // --- 5. Apply Final Position ---
            transform.position = nextPosition;
        }
    }
}