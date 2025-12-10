using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    [RequireComponent(typeof(LineRenderer))]
    public class MinimapFrustum : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Le Layer 'Minimap' créé précédemment.")]
        public string minimapLayerName = "Minimap";
        
        [Tooltip("Hauteur approximative du sol pour projeter le rectangle.")]
        public float groundLevel = 0f;

        private UnityEngine.Camera _mainCam;
        private LineRenderer _lineRenderer;
        private readonly Vector3[] _corners = new Vector3[4];

        private void Start()
        {
            _mainCam = GetComponent<UnityEngine.Camera>();
            _lineRenderer = GetComponent<LineRenderer>();

            // Configuration automatique du LineRenderer
            _lineRenderer.positionCount = 4;
            _lineRenderer.loop = true; // Pour fermer le rectangle
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth = 2f; // Épais pour être visible de haut
            _lineRenderer.endWidth = 2f;
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default")); // Shader simple
            _lineRenderer.startColor = Color.white;
            _lineRenderer.endColor = Color.white;
            
            // IMPORTANT : Mettre le LineRenderer sur le layer Minimap pour
            // qu'il ne soit visible QUE sur la minimap, pas dans le jeu principal.
            int layer = LayerMask.NameToLayer(minimapLayerName);
            if (layer != -1) gameObject.layer = layer;
        }

        private void Update()
        {
            CalculateFrustumCorners();
            _lineRenderer.SetPositions(_corners);
        }

        private void CalculateFrustumCorners()
        {
            // Les 4 coins de l'écran (Viewport : 0,0 à 1,1)
            // Bas-Gauche, Haut-Gauche, Haut-Droite, Bas-Droite
            _corners[0] = GetGroundPoint(new Vector3(0, 0, 0));
            _corners[1] = GetGroundPoint(new Vector3(0, 1, 0));
            _corners[2] = GetGroundPoint(new Vector3(1, 1, 0));
            _corners[3] = GetGroundPoint(new Vector3(1, 0, 0));
        }

        private Vector3 GetGroundPoint(Vector3 viewportPoint)
        {
            Ray ray = _mainCam.ViewportPointToRay(viewportPoint);
            
            // On utilise un plan mathématique au niveau du sol (Y = groundLevel)
            // C'est plus stable que le Raycast physique si la caméra regarde l'horizon
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundLevel, 0));

            if (groundPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }

            // Si on regarde le ciel, on clamp à une distance max pour éviter un rectangle infini
            return ray.GetPoint(300f); 
        }
    }
}