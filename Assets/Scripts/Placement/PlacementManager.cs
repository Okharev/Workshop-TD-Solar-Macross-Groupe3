using System;
using System.Collections.Generic;
using Economy;
using Towers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Placement
{
    #region Validation Strategy Pattern

    public struct ValidationResult
    {
        public bool IsValid;
        public string Message;

        public static ValidationResult Success()
        {
            return new ValidationResult { IsValid = true };
        }

        public static ValidationResult Fail(string msg)
        {
            return new ValidationResult { IsValid = false, Message = msg };
        }
    }

    public interface IPlacementValidator
    {
        ValidationResult Validate(Vector3 position, Quaternion rotation, BuildingEntity data);
    }

    public sealed class CompositeValidator : IPlacementValidator
    {
        private readonly List<IPlacementValidator> _validators = new();

        public ValidationResult Validate(Vector3 p, Quaternion r, BuildingEntity d)
        {
            foreach (var v in _validators)
            {
                var result = v.Validate(p, r, d);
                if (!result.IsValid) return result;
            }

            return ValidationResult.Success();
        }

        public void AddValidator(IPlacementValidator v)
        {
            _validators.Add(v);
        }
    }

    public sealed class EconomyValidator : IPlacementValidator
    {
        public ValidationResult Validate(Vector3 position, Quaternion rotation, BuildingEntity data)
        {
            if (!CurrencyManager.Instance.CanAfford(data.cost))
                return ValidationResult.Fail($"Insufficient Funds ({data.cost})");
            return ValidationResult.Success();
        }
    }

    public sealed class PhysicsValidator : IPlacementValidator
    {
        private readonly Collider[] _cache = new Collider[1];
        private readonly LayerMask _mask;
        private readonly float _padding;

        public PhysicsValidator(LayerMask mask, float padding)
        {
            _mask = mask;
            _padding = padding;
        }

        public ValidationResult Validate(Vector3 pos, Quaternion rot, BuildingEntity data)
        {
            if (!data || !data.currentLevelPrefab) return ValidationResult.Fail("Data or Prefab is missing");

            // --- CHANGEMENT : Support de tous les types de Colliders ---
            // On cherche n'importe quel Collider, pas seulement BoxCollider
            var refCol = data.currentLevelPrefab.GetComponent<Collider>();
            if (!refCol) refCol = data.currentLevelPrefab.GetComponentInChildren<Collider>();

            if (!refCol) return ValidationResult.Success();

            // Initialisation des valeurs par défaut
            Vector3 localCenter = Vector3.zero;
            Vector3 size = Vector3.one;

            // Extraction des dimensions selon le type de Collider
            if (refCol is BoxCollider box)
            {
                localCenter = box.center;
                size = box.size;
            }
            else if (refCol is SphereCollider sphere)
            {
                localCenter = sphere.center;
                // On transforme le rayon en taille de boîte (diamètre)
                size = Vector3.one * (sphere.radius * 2f);
            }
            else if (refCol is CapsuleCollider capsule)
            {
                localCenter = capsule.center;
                float diameter = capsule.radius * 2f;
                size = new Vector3(diameter, diameter, diameter);
                
                // La hauteur affecte un axe selon la direction (0=X, 1=Y, 2=Z)
                switch (capsule.direction)
                {
                    case 0: size.x = capsule.height; break;
                    case 1: size.y = capsule.height; break;
                    case 2: size.z = capsule.height; break;
                }
            }
            else if (refCol is MeshCollider meshCol && meshCol.sharedMesh != null)
            {
                localCenter = meshCol.sharedMesh.bounds.center;
                size = meshCol.sharedMesh.bounds.size;
            }
            
            // Gestion de la position relative si le collider est sur un enfant
            if (refCol.transform != data.currentLevelPrefab.transform)
            {
                // Note : Cela suppose que l'enfant n'a pas de rotation complexe par rapport au parent
                localCenter = refCol.transform.localPosition + localCenter;
            }

            // Calcul final de la boîte de détection dans le monde
            var center = pos + rot * localCenter;
            var halfExtents = size * (0.5f * _padding);

            // On utilise OverlapBox qui agit comme une "Bounding Box" englobante pour tous les types
            if (Physics.OverlapBoxNonAlloc(center, halfExtents, _cache, rot, _mask) > 0)
                return ValidationResult.Fail("Obstacle detected");

            return ValidationResult.Success();
        }
    }

    #endregion

    public sealed class AdditiveEnergyValidator : IPlacementValidator
    {
        private readonly LayerMask _energyLayer;

        public AdditiveEnergyValidator(LayerMask layer)
        {
            _energyLayer = layer;
        }

        public ValidationResult Validate(Vector3 pos, Quaternion rot, BuildingEntity data)
        {
            if (data.energyDrain <= 0) return ValidationResult.Success();

            var hits = Physics.OverlapSphere(pos, 0.5f, _energyLayer);

            var totalAvailable = 0;
            var checkedProducers = new HashSet<EnergyProducer>();

            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent<EnergyProducer>(out var provider))
                    if (hit.TryGetComponent<EnergyFieldLink>(out var link))
                        provider = link.GetProducer();

                if (provider && !checkedProducers.Contains(provider))
                {
                    var dist = Vector3.Distance(pos, provider.transform.position);

                    if (dist <= provider.BroadcastRadius.Value)
                    {
                        totalAvailable += provider.GetAvailable();
                        checkedProducers.Add(provider);
                    }
                }
            }

            if (totalAvailable < data.energyDrain)
                return ValidationResult.Fail($"Low Voltage ({totalAvailable}/{data.energyDrain})");

            return ValidationResult.Success();
        }
    }

    [DefaultExecutionOrder(-100)]
    public sealed class PlacementManager : MonoBehaviour
    {
        [SerializeField] private ElectricityVisualizer visualizer;

        [Header("Layer Configuration")] [SerializeField]
        private LayerMask terrainLayerMask;

        [SerializeField] private LayerMask obstacleLayerMask;
        [SerializeField] private LayerMask energyLayerMask;

        [Header("Settings")] [SerializeField] private float rotationSpeed = 10f;

        [SerializeField] private float overlapCheckPadding = 0.9f;

        [Header("Visuals")] [SerializeField] private Material validPreviewMat;

        [SerializeField] private Material invalidPreviewMat;
        private EnergyConsumer _cachedConsumer;
        private EnergyProducer _cachedProducer;

        private BuildingEntity _currentBuilding;
        private float _currentRotationY;
        private PlacementGhost _ghostHelper;
        private bool _isPlacementMode;
        private UnityEngine.Camera _mainCamera;
        private IPlacementValidator _validator;

        public static PlacementManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            _mainCamera = UnityEngine.Camera.main;

            _ghostHelper = new PlacementGhost(validPreviewMat, invalidPreviewMat);

            var composite = new CompositeValidator();
            composite.AddValidator(new PhysicsValidator(obstacleLayerMask, overlapCheckPadding));
            composite.AddValidator(new EconomyValidator());

            _validator = composite;
        }

        private void Update()
        {
            if (!_isPlacementMode || !_currentBuilding) return;

            HandleInput();
            var targetPos = GetMouseWorldPosition();

            if (targetPos.HasValue)
            {
                var position = targetPos.Value;
                var rotation = Quaternion.Euler(0, _currentRotationY, 0);

                _ghostHelper.UpdatePosition(position, rotation);
                UpdateEnergyPreview(position);

                var result = _validator.Validate(position, rotation, _currentBuilding);
                _ghostHelper.SetState(result.IsValid);

                if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                    if (result.IsValid)
                        ConfirmPlacement(position, rotation);
            }
            else
            {
                _ghostHelper.Hide();
            }
        }

        public event Action OnPlacementStarted;
        public event Action OnPlacementEnded;
        public event Action<int> OnBuildingPlaced;

        private void UpdateEnergyPreview(Vector3 ghostPosition)
        {
            // CAS A : Producteur (Heatmap)
            if (_cachedProducer != null)
            {
                var radius = _cachedProducer.BroadcastRadius.Value > 0 ? _cachedProducer.BroadcastRadius.Value : 15f;
                EnergyHeatmapSystem.Instance?.SetPreview(ghostPosition, radius, _cachedProducer.MaxCapacity.Value);
            }

            // CAS B : Consommateur (Simulation de connexion fidèle)
            if (_cachedConsumer != null && visualizer != null)
            {
                var energyNeeded = _cachedConsumer.totalRequirement.BaseValue;

                var hits = Physics.OverlapSphere(ghostPosition, 20f, energyLayerMask);
                var candidates = new List<EnergyProducer>();

                foreach (var hit in hits)
                {
                    EnergyProducer provider = null;
                    if (hit.TryGetComponent(out EnergyProducer p)) provider = p;
                    else if (hit.TryGetComponent(out EnergyFieldLink l)) provider = l.GetProducer();

                    if (provider != null)
                        if (Vector3.Distance(ghostPosition, provider.transform.position) <=
                            provider.BroadcastRadius.Value)
                            if (!candidates.Contains(provider))
                                candidates.Add(provider);
                }

                candidates.Sort((a, b) =>
                {
                    var mobileComp = (b.isMobileGenerator ? 1 : 0).CompareTo(a.isMobileGenerator ? 1 : 0);
                    if (mobileComp != 0) return mobileComp;
                    return b.MaxCapacity.Value.CompareTo(a.MaxCapacity.Value);
                });

                var targets = new List<Vector3>();

                foreach (var prod in candidates)
                {
                    if (energyNeeded <= 0) break;

                    var available = prod.GetAvailable();

                    if (available > 0)
                    {
                        var take = Mathf.Min(available, energyNeeded);
                        targets.Add(prod.transform.position);
                        energyNeeded -= take;
                    }
                }

                visualizer.PreviewConnections(ghostPosition, targets);
            }
        }

        private void ConfirmPlacement(Vector3 position, Quaternion rotation)
        {
            var newBuilding = BuildingManager.CreateBuilding(_currentBuilding, position, rotation);

            if (newBuilding)
            {
                OnBuildingPlaced?.Invoke(_currentBuilding.cost);
                Debug.Log($"Placé : {_currentBuilding.displayName}");
                StopPlacement();
            }
        }

        public void StartPlacement(BuildingEntity blueprint)
        {
            if (!blueprint) return;
            StopPlacement();

            _currentBuilding = blueprint;
            _isPlacementMode = true;

            _cachedProducer = _currentBuilding.GetComponent<EnergyProducer>();
            _cachedConsumer = _currentBuilding.GetComponent<EnergyConsumer>();

            _ghostHelper.CreateGhost(blueprint);

            if (_cachedConsumer != null || _cachedProducer != null) EnergyHeatmapSystem.Instance?.ToggleHeatmap(true);

            OnPlacementStarted?.Invoke();
        }

        public void StopPlacement()
        {
            _isPlacementMode = false;
            _currentBuilding = null;

            _cachedProducer = null;
            _cachedConsumer = null;

            _ghostHelper.ClearGhost();

            EnergyHeatmapSystem.Instance?.ToggleHeatmap(false);
            EnergyHeatmapSystem.Instance?.ClearPreview();

            if (visualizer != null) visualizer.ClearPreview();

            OnPlacementEnded?.Invoke();
        }

        private void HandleInput()
        {
            if (Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                StopPlacement();
                return;
            }

            var scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollDelta) > 0.1f) _currentRotationY += Mathf.Sign(scrollDelta) * rotationSpeed;
        }

        private Vector3? GetMouseWorldPosition()
        {
            var mousePos = Mouse.current.position.ReadValue();
            var ray = _mainCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out var hit, 1000f, terrainLayerMask)) return hit.point;
            return null;
        }

        private bool IsPointerOverUI()
        {
            return EventSystem.current && EventSystem.current.IsPointerOverGameObject();
        }
    }

    public sealed class PlacementGhost
    {
        private readonly Material _invalidMaterial;
        private readonly Material _validMaterial;
        private GameObject _ghostObject;
        private bool _lastValidityState = true;

        private GameObject _rangeVisual;
        private Renderer[] _renderers;

        public PlacementGhost(Material validMat, Material invalidMat)
        {
            _validMaterial = validMat;
            _invalidMaterial = invalidMat;
        }

        public void CreateGhost(BuildingEntity data)
        {
            ClearGhost();

            var prefab = data.currentLevelPrefab;

            // Instantiate the object
            _ghostObject = Object.Instantiate(prefab);
            _ghostObject.name = "PlacementGhost";

            // 1. Disable Colliders
            var colliders = _ghostObject.GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;

            // 2. Disable Scripts
            var scripts = _ghostObject.GetComponentsInChildren<MonoBehaviour>();
            foreach (var s in scripts) s.enabled = false;

            // 3. Handle Physics
            var rbs = _ghostObject.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            // 4. Disable Audio/Anim/Particles
            foreach (var audio in _ghostObject.GetComponentsInChildren<AudioSource>()) audio.enabled = false;
            foreach (var anim in _ghostObject.GetComponentsInChildren<Animator>()) anim.enabled = false;
            foreach (var ps in _ghostObject.GetComponentsInChildren<ParticleSystem>()) ps.Stop();

            // 5. Gather Renderers for the material swap BEFORE adding the range indicator
            _renderers = _ghostObject.GetComponentsInChildren<Renderer>();

            // 6. Set to IgnoreRaycast
            SetLayerRecursively(_ghostObject, LayerMask.NameToLayer("Ignore Raycast"));

            // 7. Visualisation de la portée
            if (data is BaseTower towerData)
            {
                var rangeVis = prefab.GetComponent<TowerRangeVisualizer>();
                if (rangeVis != null && rangeVis.rangeIndicatorPrefab != null)
                {
                    _rangeVisual = Object.Instantiate(rangeVis.rangeIndicatorPrefab, _ghostObject.transform);
                    _rangeVisual.transform.localPosition = Vector3.zero;
                    _rangeVisual.transform.localRotation = Quaternion.identity;

                    var diameter = towerData.baseRange * 2.0f;
                    _rangeVisual.transform.localScale = new Vector3(diameter, diameter, diameter);

                    _rangeVisual.SetActive(true);
                }
            }

            SetState(true, true);
        }

        private void SetLayerRecursively(GameObject obj, int newLayer)
        {
            obj.layer = newLayer;
            foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, newLayer);
        }

        public void UpdatePosition(Vector3 position, Quaternion rotation)
        {
            if (!_ghostObject) return;
            _ghostObject.transform.position = position;
            _ghostObject.transform.rotation = rotation;
            if (!_ghostObject.activeSelf) _ghostObject.SetActive(true);
        }

        public void SetState(bool isValid, bool forceUpdate = false)
        {
            if (!_ghostObject || _renderers == null) return;
            if (!forceUpdate && isValid == _lastValidityState) return;

            _lastValidityState = isValid;
            var targetMat = isValid ? _validMaterial : _invalidMaterial;

            foreach (var r in _renderers)
            {
                if (!r) continue;
                var newMats = new Material[r.sharedMaterials.Length];
                for (var i = 0; i < newMats.Length; i++) newMats[i] = targetMat;
                r.materials = newMats;
            }
        }

        public void Hide()
        {
            if (_ghostObject) _ghostObject.SetActive(false);
        }

        public void ClearGhost()
        {
            if (_ghostObject)
            {
                Object.Destroy(_ghostObject);
                _ghostObject = null;
                _renderers = null;
                _rangeVisual = null;
            }
        }
    }
}