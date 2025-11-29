using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    namespace Economy
{
    public class EnergyHeatmapSystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Material heatShaderMaterial;
        [SerializeField] private Vector2 mapSize = new Vector2(100, 100);
        
        // Shader Property IDs (Performance Optimization)
        private static readonly int GenDataID = Shader.PropertyToID("_GenData");
        private static readonly int GenCountID = Shader.PropertyToID("_GenCount");

        // Data buffers
        private readonly List<IEnergyProducer> producers = new();
        private Vector4[] shaderDataBuffer = new Vector4[128];

        public static EnergyHeatmapSystem Instance { get; private set; }

        private void Awake()
        {
            // Singleton Setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            InitializeQuad();
        }

        private void Start()
        {
            // TODO(Florian) feels kinda hacky, maybe an event bus ?
            var allProducers = FindObjectsByType<EnergyProducer>(FindObjectsSortMode.None);
            foreach (var p in allProducers)
            {
                Register(p);
            }
        }

        private void InitializeQuad()
        {
            // Create a quad to cover the map
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Heatmap_Projector";
            quad.transform.SetParent(transform);
            
            // ROTATION: 90 degrees (Flat facing down/up)
            quad.transform.eulerAngles = new Vector3(90, 0, 0); 
            
            // SCALE: Make it cover the whole map
            quad.transform.localScale = new Vector3(mapSize.x, mapSize.y, 1);
            
            // Posiiton it high
            // It acts like a "Satellite" looking down.
            // Ensure it is NOT inside the camera's near clip plane, but definitely above terrain.
            float ceilingHeight = 50.0f; 
            quad.transform.localPosition = new Vector3(0f, 0f, 0f) + Vector3.up * ceilingHeight; 

            // Assign Material
            var rend = quad.GetComponent<Renderer>();
            rend.material = heatShaderMaterial; // Assign the new Projector Material here
            
            // Optimization: Projectors don't cast shadows
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Register(IEnergyProducer producer)
        {
            if (!producers.Contains(producer)) producers.Add(producer);
        }

        public void Unregister(IEnergyProducer producer)
        {
            if (producers.Contains(producer)) producers.Remove(producer);
        }
        
        public void ToggleHeatmap(bool show)
        {
            gameObject.SetActive(show);
        }

        private void Update()
        {
            // Update the Shader every frame
            UpdateShaderData();
        }

        private void UpdateShaderData()
        {
            int count = 0;

            for (int i = 0; i < producers.Count; i++)
            {
                // Safety check for array limit
                if (count >= 128) break;

                var p = producers[i];
                if (p == null || p.Equals(null)) continue;
                if (p.GetAvailableEnergy() <= 0) continue;

                Vector3 pos = p.GetPosition();
                
                // Pack data into Vector4: (X, Z, Radius, Energy)
                // Note: We use X and Z for top-down world coordinates
                shaderDataBuffer[count] = new Vector4(
                    pos.x, 
                    pos.z, 
                    p.GetBroadcastRadius(), 
                    (float)p.GetAvailableEnergy()
                );
                
                count++;
            }

            if (count == 0) 
            {
                // If this prints, your generators are not registered or have 0 energy!
                Debug.LogWarning($"Heatmap Update: 0 Generators active. Buffer empty.");
            }
            
            // Send to GPU
            heatShaderMaterial.SetInt(GenCountID, count);
            heatShaderMaterial.SetVectorArray(GenDataID, shaderDataBuffer);
        }
    }
}
    
}