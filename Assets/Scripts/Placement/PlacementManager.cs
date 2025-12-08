using System;
using System.Collections.Generic;
using Buildings;
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
            // Check if we have enough money
            // We use CanAfford (or direct comparison) to check without spending
            if (!CurrencyManager.Instance.CanAfford(data.cost))
            {
                return ValidationResult.Fail($"Insufficient Funds ({data.cost})");
            }

            return ValidationResult.Success();        }
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
            // 1. Safety Check: Ensure the data and prefab exist
            if (!data || !data.currentLevelPrefab)
            {
                // You might want to return Success here if you want to allow placement 
                // of "invisible" logic objects, but Fail is safer for debugging.
                return ValidationResult.Fail("Data or Prefab is missing");
            }

            // 2. Find the collider (Try root first, then children)
            // Note: We use the prefab directly. This is safe for reading data.
            var refCol = data.currentLevelPrefab.GetComponent<BoxCollider>();
            if (!refCol) refCol = data.currentLevelPrefab.GetComponentInChildren<BoxCollider>();

            // If this building has no collider, we assume it can be placed anywhere (e.g. a ground decal)
            if (!refCol) return ValidationResult.Success();

            // 3. Calculate Center and Size accurately
            // Note: If the collider is on a child, we must account for the child's local position
            Vector3 localCenter = refCol.center;
            if (refCol.transform != data.currentLevelPrefab.transform)
            {
                // Add child offset if the collider is not on the root
                localCenter = refCol.transform.localPosition + refCol.center;
            }

            var center = pos + (rot * localCenter);
            var halfExtents = refCol.size * (0.5f * _padding);

            // 4. Perform the Overlap Check
            if (Physics.OverlapBoxNonAlloc(center, halfExtents, _cache, rot, _mask) > 0)
            {
                return ValidationResult.Fail("Obstacle detected");
            }

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
                {
                    if (hit.TryGetComponent<EnergyFieldLink>(out var link))
                    {
                        provider = link.GetProducer();
                    }
                }

                if (provider && !checkedProducers.Contains(provider))
                {
                    // Check if we are actually in range
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
        [Header("Layer Configuration")] [SerializeField]
        private LayerMask terrainLayerMask;

        [SerializeField] private LayerMask obstacleLayerMask;
        [SerializeField] private LayerMask energyLayerMask;

        [Header("Settings")] [SerializeField] private float rotationSpeed = 10f;

        [SerializeField] private float overlapCheckPadding = 0.9f;

        [Header("Visuals")] [SerializeField] private Material validPreviewMat;

        [SerializeField] private Material invalidPreviewMat;
        
        public event Action OnPlacementStarted;
        public event Action OnPlacementEnded;
        public event Action<int> OnBuildingPlaced;

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

            // Re-initializing masks if needed, or rely on Inspector
            // energyLayerMask = LayerMask.GetMask("PowerGrid"); 

            _ghostHelper = new PlacementGhost(validPreviewMat, invalidPreviewMat);

            var composite = new CompositeValidator();
            // composite.AddValidator(new EconomyValidator());
            composite.AddValidator(new PhysicsValidator(obstacleLayerMask, overlapCheckPadding));
            composite.AddValidator(new EconomyValidator());

            // Don't need but might be good to warn player in placement ui
            // composite.AddValidator(new AdditiveEnergyValidator(energyLayerMask));

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

                var result = _validator.Validate(position, rotation, _currentBuilding);
                _ghostHelper.SetState(result.IsValid);

                if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                {
                    if (result.IsValid)
                    {
                        ConfirmPlacement(position, rotation);
                    }
                }
            }
            else
            {
                _ghostHelper.Hide();
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
            // Huh ?
        }
        
        public void StartPlacement(BuildingEntity blueprint)
        {
            if (!blueprint) return;
            StopPlacement();

            _currentBuilding = blueprint;
            _isPlacementMode = true;

            _ghostHelper.CreateGhost(blueprint.currentLevelPrefab.gameObject);

            if (blueprint.energyDrain > 0) EnergyHeatmapSystem.Instance?.ToggleHeatmap(true);

            OnPlacementStarted?.Invoke();
        }

        public void StopPlacement()
        {
            _isPlacementMode = false;
            _currentBuilding = null;
            _ghostHelper.ClearGhost();

            EnergyHeatmapSystem.Instance?.ToggleHeatmap(false);

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

        private void PlaceTower(Vector3 position, Quaternion rotation)
        {
            var newObj = Instantiate(_currentBuilding.currentLevelPrefab.gameObject, position, rotation);

            OnBuildingPlaced?.Invoke(_currentBuilding.cost);
            Debug.Log($"Placed {_currentBuilding.name}");
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
        private Renderer[] _renderers;

        public PlacementGhost(Material validMat, Material invalidMat)
        {
            _validMaterial = validMat;
            _invalidMaterial = invalidMat;
        }

        public void CreateGhost(GameObject prefab)
        {
            ClearGhost();
    
            // Instantiate the object
            _ghostObject = Object.Instantiate(prefab);
            _ghostObject.name = "PlacementGhost";

            // 1. Disable Colliders (don't destroy them, just make them non-interactive)
            var colliders = _ghostObject.GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;

            // 2. Disable Scripts (MonoBehaviours)
            var scripts = _ghostObject.GetComponentsInChildren<MonoBehaviour>();
            foreach (var s in scripts)
            {
                s.enabled = false; 
            }
    
            // 3. Handle Physics (make Rigidbodies kinematic so they don't fall)
            var rbs = _ghostObject.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            // 4. Disable AudioSources and Animators/ParticleSystems if needed
            foreach (var audio in _ghostObject.GetComponentsInChildren<AudioSource>()) audio.enabled = false;
            foreach (var anim in _ghostObject.GetComponentsInChildren<Animator>()) anim.enabled = false;
            foreach (var ps in _ghostObject.GetComponentsInChildren<ParticleSystem>()) ps.Stop();

            // 5. Gather Renderers for the material swap
            _renderers = _ghostObject.GetComponentsInChildren<Renderer>();
    
            // 6. IMPORTANT: Set to IgnoreRaycast layer so the ghost doesn't block the mouse ray
            SetLayerRecursively(_ghostObject, LayerMask.NameToLayer("Ignore Raycast"));

            SetState(true, true);
        }

        private void SetLayerRecursively(GameObject obj, int newLayer)
        {
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
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
            }
        }
    }
}