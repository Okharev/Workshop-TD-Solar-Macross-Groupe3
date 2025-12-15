using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace Economy
{
    public sealed class EnergyHeatmapSystem : MonoBehaviour
    {
        private static readonly int EnergyID = Shader.PropertyToID("_Energy");
        private static readonly int MapCoordsID = Shader.PropertyToID("_MapCoords");
        private static readonly int HeatmapTexID = Shader.PropertyToID("_HeatmapTex");

        [Header("References")] 
        [SerializeField] private Shader solidBrushShader;
        [SerializeField] private Material projectorMaterial;

        [Header("Map Settings")] 
        [SerializeField] private Vector2 mapSize = new(100, 100);
        [SerializeField] private Vector2 mapCenterOffset = Vector2.zero;

        [Header("Visual Settings")] 
        [Range(32, 2048)] [SerializeField] private int textureResolution = 512;
        [SerializeField] private float projectorHeight = 50f;
        
        [Header("Flow Visualization")]
        [SerializeField] private bool showConnections = true;
        [SerializeField] private float connectionLineWidth = 0.5f; 
        [Range(0, 1)] [SerializeField] private float connectionOpacity = 0.5f;

        [Header("Performance")] 
        [SerializeField] private bool autoRefresh = true;

        private Material _brushMaterial;
        private CommandBuffer _cmd;
        private RenderTexture _heatmapRT;
        private Renderer _projectorRenderer;
        private MaterialPropertyBlock _propBlock; // Gardé pour DrawMesh (lignes)
        
        private Mesh _lineQuadMesh; 

        // --- PREVIEW DATA ---
        private Vector4? _previewData; // x, y, z, maxCapacity
        private float _previewRadius;

        public static EnergyHeatmapSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            InitializeResources();
            InitializeProjectorVolume();
            
            if (!solidBrushShader) solidBrushShader = Shader.Find("Hidden/Economy/SolidHeatmapBrush");
            if (solidBrushShader) _brushMaterial = new Material(solidBrushShader);
        }

        private void Start()
        {
            if (EnergyGridManager.Instance)
                EnergyGridManager.Instance.OnGridResolved += OnGridUpdated;
            RefreshHeatmap();
        }
        
        private void OnDestroy()
        {
            if (EnergyGridManager.Instance) EnergyGridManager.Instance.OnGridResolved -= OnGridUpdated;
            if (_heatmapRT) _heatmapRT.Release();
            if (_brushMaterial) Destroy(_brushMaterial);
            _cmd?.Release();
        }

        // --- PUBLIC API (Restaurée pour le PlacementManager) ---

        public void SetPreview(Vector3 pos, float radius, float maxCapacity)
        {
            _previewData = new Vector4(pos.x, pos.y, pos.z, maxCapacity);
            _previewRadius = radius;
            if(autoRefresh) RenderHeatmap();
        }
        
        public void ClearPreview()
        {
            _previewData = null;
            if(autoRefresh) RenderHeatmap();
        }

        public void ToggleHeatmap(bool state) 
        { 
            if (_projectorRenderer) _projectorRenderer.enabled = state; 
            if (state) RefreshHeatmap(); 
        }
        
        public void RefreshHeatmap() 
        { 
            RenderHeatmap(); 
        }
        
        private void OnGridUpdated() { if (autoRefresh) RenderHeatmap(); }

        // --- RENDERING ---

        private void RenderHeatmap()
        {
            if (!_heatmapRT || !_heatmapRT.IsCreated()) return;
            if (!EnergyGridManager.Instance) return;
            if (!_brushMaterial) return;

            _cmd.Clear();
            _cmd.SetRenderTarget(_heatmapRT);
            _cmd.ClearRenderTarget(true, true, Color.black);

            // 1. CONFIGURER LA CAMÉRA VIRTUELLE (TOP-DOWN)
            float left = mapCenterOffset.x - mapSize.x * 0.5f;
            float right = mapCenterOffset.x + mapSize.x * 0.5f;
            float bottom = mapCenterOffset.y - mapSize.y * 0.5f;
            float top = mapCenterOffset.y + mapSize.y * 0.5f;
            
            Matrix4x4 projectionMatrix = Matrix4x4.Ortho(left, right, bottom, top, -1000f, 1000f);
            Vector3 camPos = new Vector3(mapCenterOffset.x, 100f, mapCenterOffset.y);
            Matrix4x4 viewMatrix = Matrix4x4.TRS(camPos, Quaternion.LookRotation(Vector3.down, Vector3.forward), Vector3.one).inverse;

            _cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            // 2. DESSINER LES FORMES EXACTES (PRODUCTEURS)
            foreach (var p in EnergyGridManager.Instance.AllProducers)
            {
                if (!p || !p.isActiveAndEnabled) continue;
                float energy = p.GetAvailable();
                if (energy <= 0) continue;

                Renderer r = p.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    _cmd.SetGlobalFloat(EnergyID, energy);
                    _cmd.DrawRenderer(r, _brushMaterial, 0, 0);
                }
            }

            // 3. DESSINER LE PREVIEW (Si actif)
            if (_previewData.HasValue)
            {
                Vector3 pPos = new Vector3(_previewData.Value.x, _previewData.Value.y, _previewData.Value.z);
                float pEnergy = _previewData.Value.w;

                // On utilise le _lineQuadMesh comme base (c'est un quad 1x1 à plat)
                // On le scale à la taille du rayon * 2
                Matrix4x4 matrix = Matrix4x4.TRS(pPos, Quaternion.identity, new Vector3(_previewRadius * 2, 1, _previewRadius * 2));
                
                // Note: DrawMesh supporte le PropertyBlock, mais comme on a utilisé SetGlobalFloat 
                // pour les DrawRenderer au-dessus, on continue d'utiliser SetGlobalFloat pour la cohérence
                // ou on passe null au bloc.
                _cmd.SetGlobalFloat(EnergyID, pEnergy);
                _cmd.DrawMesh(_lineQuadMesh, matrix, _brushMaterial, 0, 0);
            }

            // 4. DESSINER LES CONNEXIONS
            if (showConnections)
            {
                var graph = EnergyGridManager.Instance.ConnectionGraph;
                foreach (var consumerKvp in graph)
                {
                    var consumer = consumerKvp.Key;
                    if(consumer == null) continue;

                    foreach (var producerKvp in consumerKvp.Value)
                    {
                        var producer = producerKvp.Key;
                        if(producer == null) continue;

                        DrawConnectionLine(producer.transform.position, consumer.transform.position, connectionOpacity);
                    }
                }
            }
            
            Graphics.ExecuteCommandBuffer(_cmd);
            UpdateProjectorUniforms();
        }

        private void DrawConnectionLine(Vector3 start, Vector3 end, float energyStrength)
        {
            Vector3 direction = (end - start);
            float length = direction.magnitude;
            if(length < 0.01f) return;

            Vector3 center = (start + end) * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 scale = new Vector3(connectionLineWidth, 1f, length);

            Matrix4x4 matrix = Matrix4x4.TRS(center, rotation, scale);

            // Pour DrawMesh, le PropertyBlock fonctionne bien et évite de changer l'état global trop souvent
            _propBlock.Clear();
            _propBlock.SetFloat(EnergyID, energyStrength);
            
            _cmd.DrawMesh(_lineQuadMesh, matrix, _brushMaterial, 0, 0, _propBlock);
        }

        private void InitializeResources()
        {
            _heatmapRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.RHalf)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _heatmapRT.Create();

            _cmd = new CommandBuffer { name = "BakeHeatmap" };
            _propBlock = new MaterialPropertyBlock();

            // Quad à plat sur XZ
            _lineQuadMesh = new Mesh
            {
                name = "LineQuad",
                vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
        }

        private void InitializeProjectorVolume()
        {
            var vol = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(vol.GetComponent<Collider>());
            vol.name = "Heatmap_Volume";
            vol.transform.SetParent(transform);
            vol.transform.localRotation = Quaternion.identity;
            vol.transform.localScale = new Vector3(mapSize.x, projectorHeight, mapSize.y);
            vol.transform.localPosition = new Vector3(mapCenterOffset.x, projectorHeight * 0.5f, mapCenterOffset.y);

            _projectorRenderer = vol.GetComponent<Renderer>();
            _projectorRenderer.sharedMaterial = projectorMaterial;
            _projectorRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _projectorRenderer.receiveShadows = false;
        }

        private void UpdateProjectorUniforms()
        {
            if (!projectorMaterial) return;
            projectorMaterial.SetVector(MapCoordsID, new Vector4(mapSize.x, mapSize.y, mapCenterOffset.x, mapCenterOffset.y));
            projectorMaterial.SetTexture(HeatmapTexID, _heatmapRT);
        }
    }
}