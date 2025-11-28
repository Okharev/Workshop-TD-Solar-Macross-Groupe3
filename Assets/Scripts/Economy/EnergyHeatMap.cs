using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    public class EnergyHeatmapSystem : MonoBehaviour
    {
        [Header("Map Settings")]
        [SerializeField] private Vector2 mapSize = new Vector2(100, 100);
        [SerializeField] private Vector3 mapCenter = Vector3.zero;
        

        [Header("Visual Settings")]
        [Tooltip("Resolution of the texture.")]
        [SerializeField] private int resolution = 128; 
        
        [Tooltip("Color ramp. Left = Low Energy (0), Right = High Energy (Max).")]
        [SerializeField] private Gradient energyGradient;
        
        [Tooltip("The amount of energy required to hit the brightest color (100% on gradient).")]
        [SerializeField] private float maxEnergySpectrum = 500f; // e.g., 500 units = White Hot
        
        [SerializeField] private float globalAlpha = 0.6f;
        [SerializeField] private float verticalOffset = 0.2f;
        [Header("Update Settings")]
        [SerializeField] private float refreshRate = 0.1f; 

        // Dependencies
        private readonly List<IEnergyProducer> producers = new();
        private Texture2D heatmapTexture;
        private Renderer meshRenderer;
        
        public static EnergyHeatmapSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            InitializeVisuals();
        }

        private void InitializeVisuals()
        {
            heatmapTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            heatmapTexture.wrapMode = TextureWrapMode.Clamp;
            heatmapTexture.filterMode = FilterMode.Bilinear; // Smooths the pixels

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = "Heatmap_Overlay";
            plane.transform.SetParent(transform);
            
            plane.transform.localPosition = mapCenter + Vector3.up * verticalOffset;
            plane.transform.localRotation = Quaternion.Euler(90, 0, 0);
            plane.transform.localScale = new Vector3(mapSize.x, mapSize.y, 1);

            meshRenderer = plane.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Sprites/Default")); 
            mat.mainTexture = heatmapTexture;
            meshRenderer.material = mat;
            
            Destroy(plane.GetComponent<Collider>());
            ToggleHeatmap(false);
        }

        private void OnEnable() => StartCoroutine(UpdateRoutine());

        public void Register(IEnergyProducer producer)
        {
            if (!producers.Contains(producer)) producers.Add(producer);
        }

        public void Unregister(IEnergyProducer producer)
        {
            if (producers.Contains(producer)) producers.Remove(producer);
        }

        public void ToggleHeatmap(bool isActive)
        {
            if(meshRenderer != null) meshRenderer.enabled = isActive;
        }

        private IEnumerator UpdateRoutine()
        {
            var wait = new WaitForSeconds(refreshRate);
            Color[] colors = new Color[resolution * resolution];

            while (enabled)
            {
                if (meshRenderer.enabled)
                {
                    GenerateDensityMap(colors);
                    heatmapTexture.SetPixels(colors);
                    heatmapTexture.Apply();
                }
                yield return wait;
            }
        }

        // --- CORE LOGIC: OVERLAP DENSITY ---
        private void GenerateDensityMap(Color[] colors)
        {
            float worldWidth = mapSize.x;
            float worldHeight = mapSize.y;
            float worldLeft = mapCenter.x - worldWidth / 2f;
            float worldBottom = mapCenter.z - worldHeight / 2f; 
            var activeProducers = new List<(Vector3 pos, float rSq, float r, int energy)>();
            
            foreach (var p in producers)
            {
                if (p == null || p.Equals(null)) continue;
                
                // We now grab the AVAILABLE ENERGY (Power)
                int energy = p.GetAvailableEnergy();
                
                // Optimization: Don't draw dead generators
                if (energy <= 0) continue;

                float r = p.GetBroadcastRadius();
                activeProducers.Add((p.GetPosition(), r * r, r, energy));
            }

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float normX = (float)x / (resolution - 1);
                    float normY = (float)y / (resolution - 1);

                    Vector3 samplePos = new Vector3(
                        worldLeft + normX * worldWidth,
                        mapCenter.y,
                        worldBottom + normY * worldHeight
                    );

                    float totalEnergyAtPixel = 0f;

                    foreach (var (pos, rSq, r, energy) in activeProducers)
                    {
                        Vector3 flatPos = pos; 
                        flatPos.y = mapCenter.y;

                        float distSqr = (flatPos - samplePos).sqrMagnitude;

                        if (distSqr < rSq)
                        {
                            float dist = Mathf.Sqrt(distSqr);
                            
                            // Calculate "Signal Quality" (0.0 to 1.0)
                            // 1.0 at center, 0.0 at edge
                            float signalQuality = Mathf.Clamp01(1.0f - (dist / r));
                            
                            // Optional: Curve it so the power stays strong further out
                            signalQuality = Mathf.Pow(signalQuality, 0.5f);

                            // Multiply signal by the Generator's actual POWER
                            totalEnergyAtPixel += signalQuality * energy;
                        }
                    }

                    // Normalize based on our Spectrum definition
                    // Example: 
                    // Pixel A has 50 energy (Wind) -> t = 0.1 (Blue)
                    // Pixel B has 500 energy (Nuclear) -> t = 1.0 (White)
                    // Pixel C has overlap (500 + 50) -> t = 1.1 (Clamped to White)
                    
                    if (totalEnergyAtPixel <= 1f)
                    {
                        colors[y * resolution + x] = Color.clear;
                    }
                    else
                    {
                        float t = Mathf.Clamp01(totalEnergyAtPixel / maxEnergySpectrum);
                        Color c = energyGradient.Evaluate(t);
                        c.a = globalAlpha; 
                        colors[y * resolution + x] = c;
                    }
                }
            }
        }
    }
    
}