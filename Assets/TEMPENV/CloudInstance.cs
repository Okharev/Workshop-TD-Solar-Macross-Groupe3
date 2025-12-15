using UnityEngine;

namespace TEMPENV
{
    [ExecuteAlways]
    public class CloudInstance : MonoBehaviour
    {
        [Header("Unique Settings")]
        public bool randomizeOnStart = true;
        public float seed = 0.0f;
    
        [Header("Lighting")]
        public Light mainSun; 

        // Internal variables
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;
    
        // Shader IDs
        private static readonly int SeedID = Shader.PropertyToID("_Seed");
        private static readonly int LightDirID = Shader.PropertyToID("_LightDir");
        private static readonly int ShadowColorID = Shader.PropertyToID("_ShadowColor");
    
        // NEW: Wind Integration ID
        private static readonly int LocalWindStrengthID = Shader.PropertyToID("_LocalWindStrength");

        void OnEnable()
        {
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();

            if (mainSun == null)
            {
                // Find the brightest directional light
                Light[] lights = FindObjectsOfType<Light>();
                foreach (Light l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        mainSun = l;
                        break;
                    }
                }
            }

            if (randomizeOnStart && Application.isPlaying)
            {
                Randomize();
            }
        }

        void Update()
        {
            UpdateCloudProperties();
        }

        public void Randomize()
        {
            seed = Random.Range(0.0f, 100.0f);
            UpdateCloudProperties();
        }

        void UpdateCloudProperties()
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_propBlock);

            // 1. Shape & Lighting
            _propBlock.SetFloat(SeedID, seed);
        
            if (mainSun != null)
            {
                Vector3 sunDir = -mainSun.transform.forward; 
                _propBlock.SetVector(LightDirID, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0));
            }
        
            // Match Ambient Color for Shadows (Optional)
            _propBlock.SetColor(ShadowColorID, RenderSettings.ambientSkyColor);

            // 2. WIND INTEGRATION
            // We ask the Manager: "How windy is it at my position?"
            float windIntensity = 0f;
        
            if (GlobalWindManager.Instance != null)
            {
                // Uses the texture sampling method from your file
                windIntensity = GlobalWindManager.Instance.GetWindAtPosition(transform.position);
            
                // Multiply by the global slider strength for easy tuning
                windIntensity *= GlobalWindManager.Instance.globalWindStrength;
            }

            // Send this specific intensity to the shader
            _propBlock.SetFloat(LocalWindStrengthID, windIntensity);

            _renderer.SetPropertyBlock(_propBlock);
        }
    }
}