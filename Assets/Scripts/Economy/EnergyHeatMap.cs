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

        [Header("Targeting")]
        [Tooltip("Select the Layer that your Energy Zone colliders are on. The system will ignore physical walls.")]
        [SerializeField] private LayerMask energyZoneLayer; // <-- NOUVEAU : Filtre de Layer

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
        private MaterialPropertyBlock _propBlock;
        
        private Mesh _unitCubeMesh;
        private Mesh _unitSphereMesh;
        private Mesh _lineQuadMesh; 

        // --- PREVIEW DATA ---
        private Vector4? _previewData; 
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

        // --- PUBLIC API ---

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
        
        public void RefreshHeatmap() { RenderHeatmap(); }
        
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

            float left = mapCenterOffset.x - mapSize.x * 0.5f;
            float right = mapCenterOffset.x + mapSize.x * 0.5f;
            float bottom = mapCenterOffset.y - mapSize.y * 0.5f;
            float top = mapCenterOffset.y + mapSize.y * 0.5f;
            
            Matrix4x4 projectionMatrix = Matrix4x4.Ortho(left, right, bottom, top, -1000f, 1000f);
            Vector3 camPos = new Vector3(mapCenterOffset.x, 100f, mapCenterOffset.y);
            Matrix4x4 viewMatrix = Matrix4x4.TRS(camPos, Quaternion.LookRotation(Vector3.down, Vector3.forward), Vector3.one).inverse;

            _cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            // 2. DESSINER LES FORMES EXACTES (FILTRÉES PAR LAYER)
            foreach (var p in EnergyGridManager.Instance.AllProducers)
            {
                if (!p || !p.isActiveAndEnabled) continue;
                float energy = p.GetAvailable();
                if (energy <= 0) continue;

                // --- CORRECTION MAJEURE ICI ---
                // On récupère TOUS les colliders, puis on cherche celui qui correspond au Layer
                Collider[] allCols = p.GetComponentsInChildren<Collider>();
                Collider targetCol = null;

                foreach (var c in allCols)
                {
                    // Vérification bitwise pour voir si le layer de l'objet est dans le Mask
                    if ((energyZoneLayer.value & (1 << c.gameObject.layer)) > 0)
                    {
                        targetCol = c;
                        break; // On a trouvé la zone d'énergie !
                    }
                }

                if (targetCol != null)
                {
                    DrawCollider(targetCol, energy);
                }
            }

            // 3. DESSINER LE PREVIEW
            if (_previewData.HasValue)
            {
                Vector3 pPos = new Vector3(_previewData.Value.x, _previewData.Value.y, _previewData.Value.z);
                float pEnergy = _previewData.Value.w;
                float pRadius = _previewRadius;

                Matrix4x4 matrix = Matrix4x4.TRS(pPos, Quaternion.identity, Vector3.one * (pRadius * 2));
                
                _propBlock.Clear();
                _propBlock.SetFloat(EnergyID, pEnergy);
                _cmd.DrawMesh(_unitSphereMesh, matrix, _brushMaterial, 0, 0, _propBlock);
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

        private void DrawCollider(Collider col, float energy)
        {
            Transform t = col.transform;
            Mesh meshToDraw = null;
            Matrix4x4 matrix = Matrix4x4.identity;

            if (col is BoxCollider box)
            {
                meshToDraw = _unitCubeMesh;
                Vector3 worldCenter = t.TransformPoint(box.center);
                Vector3 worldScale = Vector3.Scale(t.lossyScale, box.size);
                matrix = Matrix4x4.TRS(worldCenter, t.rotation, worldScale);
            }
            else if (col is SphereCollider sphere)
            {
                meshToDraw = _unitSphereMesh;
                Vector3 worldCenter = t.TransformPoint(sphere.center);
                float maxScale = Mathf.Max(t.lossyScale.x, Mathf.Max(t.lossyScale.y, t.lossyScale.z));
                float worldDiameter = sphere.radius * 2f * maxScale;
                matrix = Matrix4x4.TRS(worldCenter, t.rotation, Vector3.one * worldDiameter);
            }
            else if (col is MeshCollider meshCol)
            {
                if(meshCol.sharedMesh != null)
                {
                    meshToDraw = meshCol.sharedMesh;
                    matrix = t.localToWorldMatrix; 
                }
            }
            else if (col is CapsuleCollider capsule)
            {
                 meshToDraw = _unitSphereMesh;
                 float maxScale = Mathf.Max(t.lossyScale.x, t.lossyScale.z); 
                 float worldDiameter = capsule.radius * 2f * maxScale;
                 Vector3 worldCenter = t.TransformPoint(capsule.center);
                 matrix = Matrix4x4.TRS(worldCenter, t.rotation, Vector3.one * worldDiameter);
            }

            if (meshToDraw != null)
            {
                _propBlock.Clear();
                _propBlock.SetFloat(EnergyID, energy);
                _cmd.DrawMesh(meshToDraw, matrix, _brushMaterial, 0, 0, _propBlock);
            }
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

            _lineQuadMesh = new Mesh { name = "LineQuad" };
            _lineQuadMesh.SetVertices(new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) });
            _lineQuadMesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            _lineQuadMesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            
            GameObject cubeTemp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _unitCubeMesh = cubeTemp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cubeTemp);

            GameObject sphereTemp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _unitSphereMesh = sphereTemp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphereTemp);
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