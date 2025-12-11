using UnityEngine;

namespace Towers
{
    public class WindRotator : MonoBehaviour
    {
        [SerializeField] private Transform toRotate;
        
        [Header("Rotation Settings")]
        public float minSpeed = 20f;
        public float maxSpeed = 300f; // Vitesse max quand une rafale blanche passe

        void Update()
        {
            if (toRotate == null) return;

            float windIntensity = 0f;

            // On demande au Manager s'il existe
            if (GlobalWindManager.Instance != null)
            {
                windIntensity = GlobalWindManager.Instance.GetWindAtPosition(transform.position);
            }

            // Interpolation de la vitesse basée sur l'intensité du vent (0 à 1)
            float currentSpeed = Mathf.Lerp(minSpeed, maxSpeed, windIntensity);

            // Rotation
            toRotate.Rotate(Vector3.up, currentSpeed * Time.deltaTime);
        }
    }
}