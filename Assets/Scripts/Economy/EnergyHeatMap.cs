using UnityEngine;
using UnityEngine.Rendering;

namespace Economy
{
    public class EnergyHeatmapSystem : MonoBehaviour
    {
        private static readonly int EnergyID = Shader.PropertyToID("_Energy");
        private static readonly int CoreRadiusID = Shader.PropertyToID("_CoreRadius");
        private static readonly int MapCoordsID = Shader.PropertyToID("_MapCoords");
        private static readonly int HeatmapTexID = Shader.PropertyToID("_HeatmapTex");

        [Header("References")] [SerializeField]
        private Shader brushShader;

        [SerializeField] private Material projectorMaterial;

        [Header("Map Settings")] [SerializeField]
        private Vector2 mapSize = new(100, 100);

        [SerializeField] private Vector2 mapCenterOffset = Vector2.zero;

        [Header("Visual Settings")] [Range(32, 2048)] [SerializeField]
        private int textureResolution = 512;

        [SerializeField] private float projectorHeight = 50f;

        [Range(0f, 0.95f)] [SerializeField] private float brushCoreRadius = 0.5f;

        [Header("Performance")] [Tooltip("If true, redraws automatically when the Grid changes.")] [SerializeField]
        private bool autoRefresh = true;

        private Material _brushMaterial;

        private CommandBuffer _cmd;
        private RenderTexture _heatmapRT;
        private Renderer _projectorRenderer;
        private MaterialPropertyBlock _propBlock;
        private Mesh _quadMesh;

        public static EnergyHeatmapSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            InitializeResources();
            InitializeProjectorVolume();
        }

        private void Start()
        {
            // Subscribe to the Manager
            if (EnergyGridManager.Instance)
                EnergyGridManager.Instance.OnGridResolved += OnGridUpdated;

            RefreshHeatmap();
        }

        private void OnDestroy()
        {
            if (EnergyGridManager.Instance)
                EnergyGridManager.Instance.OnGridResolved -= OnGridUpdated;

            if (_heatmapRT) _heatmapRT.Release();
            if (_brushMaterial) Destroy(_brushMaterial);
            _cmd?.Release();
        }

        private void OnGridUpdated()
        {
            if (autoRefresh) RenderHeatmap();
        }

        // Public hook for the PlacementManager to show/hide the map
        public void ToggleHeatmap(bool state)
        {
            if (_projectorRenderer) _projectorRenderer.enabled = state;
            if (state) RefreshHeatmap();
        }

        public void RefreshHeatmap()
        {
            RenderHeatmap();
        }

        private void RenderHeatmap()
        {
            // Safety checks
            if (!_heatmapRT || !_heatmapRT.IsCreated()) return;
            if (!EnergyGridManager.Instance) return;

            // 1. Prepare Buffer
            _cmd.Clear();
            _cmd.SetRenderTarget(_heatmapRT);
            _cmd.ClearRenderTarget(true, true, Color.black);

            // 2. Setup Ortho Camera for the Texture
            var ortho = Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 100f);
            _cmd.SetViewProjectionMatrices(Matrix4x4.identity, ortho);

            // 3. Draw Producers
            foreach (var p in EnergyGridManager.Instance.AllProducers)
            {
                if (!p || !p.isActiveAndEnabled) continue;

                float energy = p.GetAvailable();
                // float energy = p.MaxCapacity.Value; // Uncomment to visualize TOTAL RANGE

                if (energy <= 0) continue;

                var worldPos = p.transform.position;
                var radius = p.BroadcastRadius.Value;

                // 4. Calculate UV Space
                var u = (worldPos.x - mapCenterOffset.x) / mapSize.x + 0.5f;
                var v = (worldPos.z - mapCenterOffset.y) / mapSize.y + 0.5f;
                var sX = radius * 2f / mapSize.x;
                var sY = radius * 2f / mapSize.y;

                // Optimization: Cull if off-map
                if (u + sX < 0 || u - sX > 1 || v + sY < 0 || v - sY > 1) continue;

                var matrix = Matrix4x4.TRS(new Vector3(u, v, 0), Quaternion.identity, new Vector3(sX, sY, 1));

                _propBlock.Clear();
                _propBlock.SetFloat(EnergyID, energy);
                _propBlock.SetFloat(CoreRadiusID, brushCoreRadius);

                _cmd.DrawMesh(_quadMesh, matrix, _brushMaterial, 0, 0, _propBlock);
            }

            Graphics.ExecuteCommandBuffer(_cmd);
            UpdateProjectorUniforms();
        }

        #region Initialization Boilerplate (Unchanged)

        // Kept standard setup code
        private void InitializeResources()
        {
            _heatmapRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.RHalf)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            _heatmapRT.Create();

            if (!brushShader) brushShader = Shader.Find("Hidden/Economy/HeatmapBrush");
            _brushMaterial = new Material(brushShader);

            _quadMesh = new Mesh
            {
                name = "HeatmapQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0),
                    new Vector3(0.5f, 0.5f, 0)
                },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };

            _cmd = new CommandBuffer { name = "BakeHeatmap" };
            _propBlock = new MaterialPropertyBlock();
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
            _projectorRenderer.lightProbeUsage = LightProbeUsage.Off;

            UpdateProjectorUniforms();
        }

        private void UpdateProjectorUniforms()
        {
            if (!projectorMaterial) return;
            projectorMaterial.SetVector(MapCoordsID,
                new Vector4(mapSize.x, mapSize.y, mapCenterOffset.x, mapCenterOffset.y));
            projectorMaterial.SetTexture(HeatmapTexID, _heatmapRT);
        }

        #endregion
    }
}