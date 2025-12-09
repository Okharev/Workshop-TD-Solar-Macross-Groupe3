using UnityEngine;

namespace Towers
{
    public class WindRotator : MonoBehaviour
    {
        [SerializeField] private Transform toRotate;
        
        [Header("Wind Settings")]
        [Tooltip("The base rotation speed when the wind is calm.")]
        public float baseSpeed = 20f;

        [Tooltip("How much extra speed the wind adds during gusts.")]
        public float gustStrength = 150f;

        [Tooltip("How quickly the wind changes intensity (lower is smoother).")]
        public float gustFrequency = 0.5f;

        void Update()
        {
            // 1. Calculate the wind intensity
            // We use PerlinNoise to generate a value between 0 and 1 that changes smoothly over time.
            // This simulates natural wind better than random numbers.
            float windGust = Mathf.PerlinNoise(Time.time * gustFrequency, 0f);

            // 2. Determine the final rotation speed
            float currentSpeed = baseSpeed + (windGust * gustStrength);

            // 3. Apply the rotation
            // Vector3.up corresponds to the Y axis (0, 1, 0)
            // Time.deltaTime ensures the speed is per second, not per frame
            toRotate.transform.Rotate(Vector3.up, currentSpeed * Time.deltaTime);
        }
    }
}