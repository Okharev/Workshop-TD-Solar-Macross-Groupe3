using System;
using System.Collections.Generic;
using Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Placement
{
    #region Validation Strategy Pattern

    public struct ValidationResult
    {
        public bool isValid;
        public string message;

        public static ValidationResult Success()
        {
            return new ValidationResult { isValid = true };
        }

        public static ValidationResult Fail(string msg)
        {
            return new ValidationResult { isValid = false, message = msg };
        }
    }

    public interface IPlacementValidator
    {
        ValidationResult Validate(Vector3 position, Quaternion rotation, BuildingSo data);
    }

    public class CompositeValidator : IPlacementValidator
    {
        private readonly List<IPlacementValidator> _validators = new();

        public ValidationResult Validate(Vector3 p, Quaternion r, BuildingSo d)
        {
            foreach (var v in _validators)
            {
                var result = v.Validate(p, r, d);
                if (!result.isValid) return result;
            }

            return ValidationResult.Success();
        }

        public void AddValidator(IPlacementValidator v)
        {
            _validators.Add(v);
        }
    }

    public class PhysicsValidator : IPlacementValidator
    {
        private readonly Collider[] _cache = new Collider[1];
        private readonly LayerMask _mask;
        private readonly float _padding;

        public PhysicsValidator(LayerMask mask, float padding)
        {
            _mask = mask;
            _padding = padding;
        }

        public ValidationResult Validate(Vector3 pos, Quaternion rot, BuildingSo data)
        {
            var refCol = data.prefab.GetComponent<BoxCollider>();
            if (!refCol) refCol = data.prefab.GetComponentInChildren<BoxCollider>();
            if (!refCol) return ValidationResult.Success();
            var center = pos + rot * refCol.center;
            var halfExtents = refCol.size * (0.5f * _padding);
            if (Physics.OverlapBoxNonAlloc(center, halfExtents, _cache, rot, _mask) > 0)
                return ValidationResult.Fail("Obstacle detected");
            return ValidationResult.Success();
        }
    }

    #endregion

    // CHANGED: Validation Logic adapted to new Producer
    public class AdditiveEnergyValidator : IPlacementValidator
    {
        private readonly LayerMask _energyLayer;

        public AdditiveEnergyValidator(LayerMask layer)
        {
            _energyLayer = layer;
        }

        public ValidationResult Validate(Vector3 pos, Quaternion rot, BuildingSo data)
        {
            if (data.energyDrain <= 0) return ValidationResult.Success();

            // Find triggers (Generators/Districts)
            // Note: Since we use GridManager distance check, ideally we ask Manager.
            // But for simple "overlap" placement checks, physics is still fastest for "Who is nearby".
            var hits = Physics.OverlapSphere(pos, 0.5f, _energyLayer);

            var totalAvailable = 0;
            var checkedProducers = new HashSet<EnergyProducer>();

            foreach (var hit in hits)
            {
                EnergyProducer provider = null;

                // 1. Direct check
                provider = hit.GetComponent<EnergyProducer>();

                // 2. Link check (Refactored link class)
                if (!provider)
                {
                    var link = hit.GetComponent<EnergyFieldLink>();
                    if (link) provider = link.GetProducer();
                }

                // 3. Add unique energy
                if (provider && !checkedProducers.Contains(provider))
                {
                    // Check if we are actually in range (Double check logic)
                    var dist = Vector3.Distance(pos, provider.transform.position);
                    if (dist <= provider.BroadcastRadius.Value)
                    {
                        // Use new Public API
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
    public class PlacementManager : MonoBehaviour
    {
        [Header("Layer Configuration")] [SerializeField]
        private LayerMask terrainLayerMask;

        [SerializeField] private LayerMask obstacleLayerMask;
        [SerializeField] private LayerMask energyLayerMask;

        [Header("Settings")] [SerializeField] private float rotationSpeed = 10f;

        [SerializeField] private float overlapCheckPadding = 0.9f;

        [Header("Visuals")] [SerializeField] private Material validPreviewMat;

        [SerializeField] private Material invalidPreviewMat;

        private BuildingSo _currentBuilding;
        private float _currentRotationY;
        private PlacementGhost _ghostHelper;
        private bool _isPlacementMode;
        private UnityEngine.Camera _mainCamera;
        private IPlacementValidator _validator;

        private void Awake()
        {
            _mainCamera = UnityEngine.Camera.main;

            // Re-initializing masks if needed, or rely on Inspector
            // energyLayerMask = LayerMask.GetMask("PowerGrid"); 

            _ghostHelper = new PlacementGhost(validPreviewMat, invalidPreviewMat);

            var composite = new CompositeValidator();
            // composite.AddValidator(new EconomyValidator());
            composite.AddValidator(new PhysicsValidator(obstacleLayerMask, overlapCheckPadding));

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
                _ghostHelper.SetState(result.isValid);

                if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                    if (result.isValid)
                        PlaceTower(position, rotation);
            }
            else
            {
                _ghostHelper.Hide();
            }
        }

        public event Action OnPlacementStarted;
        public event Action OnPlacementEnded;
        public event Action<int> OnBuildingPlaced;

        public void StartPlacement(BuildingSo blueprint)
        {
            if (!blueprint) return;
            StopPlacement();

            _currentBuilding = blueprint;
            _isPlacementMode = true;

            _ghostHelper.CreateGhost(blueprint.prefab.gameObject);

            // CHANGED: Uncommented Heatmap toggle
            if (blueprint.energyDrain > 0) EnergyHeatmapSystem.Instance?.ToggleHeatmap(true);

            OnPlacementStarted?.Invoke();
        }

        public void StopPlacement()
        {
            _isPlacementMode = false;
            _currentBuilding = null;
            _ghostHelper.ClearGhost();

            // CHANGED: Uncommented Heatmap toggle
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
            // Instantiate Real Object
            var newObj = Instantiate(_currentBuilding.prefab.gameObject, position, rotation);

            // CHANGED: Energy Registration is automatic now!
            // When newObj is instantiated, EnergyConsumer.Start() runs.
            // It calls EnergyGridManager.Register().
            // The Manager sees the new drain, runs ResolveGrid(), and powers it.
            // No manual "ConsumeEnergyResources" needed.

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

    // [PlacementGhost Class remains exactly the same, no changes needed]
    public class PlacementGhost
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
            _ghostObject = Object.Instantiate(prefab);
            _ghostObject.name = "PlacementGhost";
            foreach (var c in _ghostObject.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (var s in _ghostObject.GetComponentsInChildren<MonoBehaviour>()) Object.Destroy(s);
            _renderers = _ghostObject.GetComponentsInChildren<Renderer>();
            SetState(true, true);
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