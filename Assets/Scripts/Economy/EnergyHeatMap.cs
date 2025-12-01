using R3;
using ObservableCollections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Economy
{
    public class EnergyHeatmapSystem : MonoBehaviour
    {
        [Header("References")] 
        [SerializeField] private Shader brushShader; 
        [SerializeField] private Material projectorMaterial; 

        [Header("Map Settings")] 
        [SerializeField] private Vector2 mapSize = new(100, 100);
        [SerializeField] private Vector2 mapCenterOffset = Vector2.zero;
        
        [Header("Visual Settings")]
        [Range(32, 2048)]
        [SerializeField] private int textureResolution = 512;
        [SerializeField] private float projectorHeight = 50f;
        [Range(0f, 0.95f)] 
        [SerializeField] private float brushCoreRadius = 0.5f;

        [Header("Performance")]
        [SerializeField] private bool realTimeUpdate = true;

        private readonly ObservableCollections.ObservableList<IReactiveEnergyProducer> _producers = new();
        
        private Material _brushMaterial;
        private RenderTexture _heatmapRT;
        private Renderer _projectorRenderer;
        private Mesh _quadMesh;
        
        private CommandBuffer _cmd;
        private MaterialPropertyBlock _propBlock;
        private CompositeDisposable _disposables = new();
        
        private static readonly int EnergyID = Shader.PropertyToID("_Energy");
        private static readonly int CoreRadiusID = Shader.PropertyToID("_CoreRadius");
        
        private static readonly int MapCoordsID = Shader.PropertyToID("_MapCoords");
        private static readonly int HeatmapTexID = Shader.PropertyToID("_HeatmapTex");
        
        public static EnergyHeatmapSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitializeResources();
            InitializeProjectorVolume();
        }

        private void Start()
        {
            if (realTimeUpdate)
            {
                Observable.EveryUpdate(UnityFrameProvider.PostLateUpdate)
                    .Subscribe(_ => RenderHeatmap())
                    .AddTo(_disposables);
            }
            else
            {

                Observable.Merge(
                    _producers.ObserveAdd().Select(_ => Unit.Default),
                    _producers.ObserveRemove().Select(_ => Unit.Default),
                    _producers.ObserveReset().Select(_ => Unit.Default)
                )
                .ThrottleFirstFrame(1) 
                .Subscribe(_ => RenderHeatmap())
                .AddTo(_disposables);
            }
            
            RenderHeatmap();
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
            if (_heatmapRT) _heatmapRT.Release();
            if (_brushMaterial) Destroy(_brushMaterial);
            _cmd?.Release();
        }

        public void Register(IReactiveEnergyProducer producer)
        {
            if (!_producers.Contains(producer)) _producers.Add(producer);
        }

        public void Unregister(IReactiveEnergyProducer producer)
        {
            if (_producers.Contains(producer)) _producers.Remove(producer);
        }

        private void RenderHeatmap()
        {
            if (!_heatmapRT || !_heatmapRT.IsCreated()) return;

            _cmd.Clear();
            _cmd.SetRenderTarget(_heatmapRT);
            _cmd.ClearRenderTarget(true, true, Color.black);

            var ortho = Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 100f);
            _cmd.SetViewProjectionMatrices(Matrix4x4.identity, ortho);

            for (var i = 0; i < _producers.Count; i++)
            {
                var p = _producers[i];
                if (p == null || p.Equals(null)) continue;
                
                float energy = p.AvailableEnergy.CurrentValue;
                if (energy <= 0) continue;

                var worldPos = p.Position;
                var radius = p.BroadcastRadius;

                var u = (worldPos.x - mapCenterOffset.x) / mapSize.x + 0.5f;
                var v = (worldPos.z - mapCenterOffset.y) / mapSize.y + 0.5f;
                var sX = radius * 2f / mapSize.x;
                var sY = radius * 2f / mapSize.y;

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
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) },
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
            
            var meshFilter = vol.GetComponent<MeshFilter>();
            if (meshFilter) meshFilter.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            UpdateProjectorUniforms();
        }

        private void UpdateProjectorUniforms()
        {
            if (!projectorMaterial) return;
            projectorMaterial.SetVector(MapCoordsID, new Vector4(mapSize.x, mapSize.y, mapCenterOffset.x, mapCenterOffset.y));
            projectorMaterial.SetTexture(HeatmapTexID, _heatmapRT);
        }
    }
}