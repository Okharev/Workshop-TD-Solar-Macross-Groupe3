using System;
using System.Collections.Generic;
using Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

// Required for Energy sorting

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

    // 1. Composite (Holds other validators)
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

    // 2. Physics (Existing Logic)
    public class PhysicsValidator : IPlacementValidator
    {
        private readonly Collider[] _cache = new Collider[1];
        private readonly LayerMask _mask;
        private readonly float _padding;

        public PhysicsValidator(LayerMask mask, float padding)
        {
            this._mask = mask;
            this._padding = padding;
        }

        public ValidationResult Validate(Vector3 pos, Quaternion rot, BuildingSo data)
        {
            // Assuming the prefab has a BoxCollider at root or first child
            var refCol = data.prefab.GetComponent<BoxCollider>();
            if (!refCol) refCol = data.prefab.GetComponentInChildren<BoxCollider>();

            if (!refCol) return ValidationResult.Success(); // No collider to check

            var center = pos + rot * refCol.center;
            var halfExtents = refCol.size * (0.5f * _padding);

            if (Physics.OverlapBoxNonAlloc(center, halfExtents, _cache, rot, _mask) > 0)
                return ValidationResult.Fail("Obstacle detected");
            return ValidationResult.Success();
        }
    }

    // 3. Economy (Money Check)
    public class EconomyValidator : IPlacementValidator
    {
        public ValidationResult Validate(Vector3 p, Quaternion r, BuildingSo data)
        {
            // Need a reference to CurrencySystem (Singleton or Static)
            // if (CurrencySystem.Instance.GetBalance() < data.cost) 
            //     return ValidationResult.Fail("Insufficient Funds");

            return ValidationResult.Success();
        }
    }

    // 4. Energy (New Logic - Additive)
    public class AdditiveEnergyValidator : IPlacementValidator
    {
        private readonly LayerMask _energyLayer;

        public AdditiveEnergyValidator(LayerMask layer)
        {
            _energyLayer = layer;
        }

        public ValidationResult Validate(Vector3 pos, Quaternion rot, BuildingSo data)
        {
            var hits = Physics.OverlapSphere(pos, 0.5f, _energyLayer);
            var totalAvailable = 0;

            // HashSet to prevent double counting the same generator
            var checkedProducers = new HashSet<IReactiveEnergyProducer>();

            foreach (var hit in hits)
            {
                IReactiveEnergyProducer provider = null;

                // 1. Direct check
                provider = hit.GetComponent<IReactiveEnergyProducer>();

                // 2. Link check
                if (provider == null)
                {
                    var link = hit.GetComponent<EnergyFieldLink>();
                    if (link) provider = link.GetProducer();
                }

                // 3. Add unique energy
                if (provider != null)
                    // If we haven't counted this specific producer yet...
                    if (!checkedProducers.Contains(provider))
                    {
                        totalAvailable += provider.AvailableEnergy.CurrentValue;
                        checkedProducers.Add(provider);
                    }
            }

            if (totalAvailable < data.energyDrain)
                return ValidationResult.Fail("Low Voltage");

            return ValidationResult.Success();
        }
    }

    #endregion

    /// <summary>
    ///     Orchestrates the placement state, input, and delegation to validators/visuals.
    /// </summary>
    public class PlacementManager : MonoBehaviour
    {
        [Header("Layer Configuration")] [Tooltip("Layer for the ground only (Camera Raycast).")] [SerializeField]
        private LayerMask terrainLayerMask;

        [Tooltip("Layer for existing buildings/obstacles (Physics Validator).")] [SerializeField]
        private LayerMask obstacleLayerMask;

        [Tooltip("Layer for District Triggers and Generator Ranges (Energy Validator).")] [SerializeField]
        private LayerMask energyLayerMask;

        [Header("Settings")] [SerializeField] private float rotationSpeed = 10f;

        [SerializeField] private float overlapCheckPadding = 0.9f;

        [Header("Visuals")] [SerializeField] private Material validPreviewMat;

        [SerializeField] private Material invalidPreviewMat;

        // State
        private BuildingSo _currentBuilding;
        private float _currentRotationY;

        // Dependencies (Helpers)
        private PlacementGhost _ghostHelper;
        private bool _isPlacementMode;
        private UnityEngine.Camera _mainCamera;
        private IPlacementValidator _validator; // The Strategy Pattern

        private void Awake()
        {
            _mainCamera = UnityEngine.Camera.main;

            energyLayerMask = LayerMask.GetMask("PowerGrid");
            obstacleLayerMask = LayerMask.GetMask("PlacementBlockers", "Roads");
            terrainLayerMask = LayerMask.GetMask("Terrain");

            // Initialize Ghost Helper
            _ghostHelper = new PlacementGhost(validPreviewMat, invalidPreviewMat);

            // Setup Validation Strategy (Composite)
            var composite = new CompositeValidator();

            // 1. Check if we have Money
            // composite.AddValidator(new EconomyValidator());

            // 2. Check if space is empty (Physics)
            composite.AddValidator(new PhysicsValidator(obstacleLayerMask, overlapCheckPadding));

            // 3. Check if we have Power (Energy)
            composite.AddValidator(new AdditiveEnergyValidator(energyLayerMask));

            _validator = composite;
        }

        // Events for UI
        public event Action OnPlacementStarted;
        public event Action OnPlacementEnded;
        public event Action<int> OnBuildingPlaced; // Pass cost/id

        #region Public API

        public void StartPlacement(BuildingSo blueprint)
        {
            if (!blueprint) return;
            StopPlacement();

            _currentBuilding = blueprint;
            _isPlacementMode = true;

            _ghostHelper.CreateGhost(blueprint.prefab.gameObject);

            // Enable Heatmap if the building needs power ---
            if (blueprint.energyDrain > 0)
            {
                // EnergyHeatmapSystem.Instance?.ToggleHeatmap(true);
            }

            OnPlacementStarted?.Invoke();
        }


        public void StopPlacement()
        {
            _isPlacementMode = false;
            _currentBuilding = null;
            _ghostHelper.ClearGhost();

            // --- Hide Heatmap ---
            // EnergyHeatmapSystem.Instance?.ToggleHeatmap(false);
            // -------------------------

            OnPlacementEnded?.Invoke();
        }

        #endregion

        #region Core Loop

        private void Update()
        {
            if (!_isPlacementMode || !_currentBuilding) return;

            HandleInput();

            // 1. Find Mouse Position on Terrain
            var targetPos = GetMouseWorldPosition();

            if (targetPos.HasValue)
            {
                var position = targetPos.Value;
                var rotation = Quaternion.Euler(0, _currentRotationY, 0);

                // 2. Update Ghost Position
                _ghostHelper.UpdatePosition(position, rotation);

                // 3. Validate
                var result = _validator.Validate(position, rotation, _currentBuilding);
                _ghostHelper.SetState(result.isValid);

                // 4. Place Logic (Left Click)
                if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                {
                    if (result.isValid)
                        PlaceTower(position, rotation);
                    else
                        Debug.Log($"Placement Failed: {result.message}");
                    // Optional: Play Error Sound or Shake Camera
                }
            }
            else
            {
                _ghostHelper.Hide();
            }
        }

        private void HandleInput()
        {
            if (Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                StopPlacement();
                return;
            }

            // Rotation
            var scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollDelta) > 0.1f) _currentRotationY += Mathf.Sign(scrollDelta) * rotationSpeed;
        }

        private void PlaceTower(Vector3 position, Quaternion rotation)
        {
            // 1. Spend Money
            // Assuming CurrencySystem is a Singleton as discussed
            // CurrencySystem.Instance.Spend(currentBuilding.cost); 
            // (Commented out until you link your CurrencySystem)

            // 2. Instantiate Real Object
            var newObj = Instantiate(_currentBuilding.prefab.gameObject, position, rotation);

            // 3. Connect Energy (The Consumer Logic)
            // If the building has an EnergyConsumer component, it needs to register with the grid.
            // Note: If EnergyConsumer uses Start(), this happens automatically. 
            // If we need to "Pre-Deduct" energy from the grid logic we discussed earlier:
            ConsumeEnergyResources(position, _currentBuilding, newObj);

            Debug.Log($"Placed {_currentBuilding.name}");
            OnBuildingPlaced?.Invoke(_currentBuilding.cost);

            // Optional: Continue placement (Shift key logic) or Stop
            // StopPlacement();
        }

        #endregion

        #region Helpers

        private Vector3? GetMouseWorldPosition()
        {
            var mousePos = Mouse.current.position.ReadValue();
            var ray = _mainCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out var hit, 1000f, terrainLayerMask)) return hit.point;
            return null;
        }

        private bool IsPointerOverUI()
        {
            // Simple Unity EventSystem check
            return EventSystem.current &&
                   EventSystem.current.IsPointerOverGameObject();
        }

        // Logic to finalize energy consumption upon placement
        private void ConsumeEnergyResources(Vector3 pos, BuildingSo data, GameObject instance)
        {
            // If it's a generator, it doesn't consume
            // if (data.isGenerator) return; 

            // If it's a consumer, the component on the instantiated object handles the connection
            // inside its Start() method naturally.
            // However, if we want to force an immediate update:
            var consumer = instance.GetComponent<IReactiveEnergyConsumer>();
            // consumer?.ConnectToPower(); // Assuming you made this public
        }

        #endregion
    }

    // ==========================================================
    // SUB-SYSTEMS
    // ==========================================================

    #region Helper Classes (Ghost)

    public class PlacementGhost
    {
        private readonly Material _invalidMaterial;

        // Settings
        private readonly Material _validMaterial;

        // State
        private GameObject _ghostObject;
        private bool _lastValidityState = true;
        private Renderer[] _renderers;

        public PlacementGhost(Material validMat, Material invalidMat)
        {
            _validMaterial = validMat;
            _invalidMaterial = invalidMat;
        }

        /// <summary>
        ///     The "Fabric" method: Creates the visual representation and strips logic.
        /// </summary>
        public void CreateGhost(GameObject prefab)
        {
            // 1. Clean up existing ghost if any
            ClearGhost();

            // 2. Instantiate
            _ghostObject = Object.Instantiate(prefab);
            _ghostObject.name = "PlacementGhost";

            // 3. STRIPPING: Remove functional components
            // We don't want the ghost to collide with the mouse raycast
            var colliders = _ghostObject.GetComponentsInChildren<Collider>();
            foreach (var c in colliders) Object.Destroy(c);

            // We don't want the ghost to run logic (like shooting or consuming energy)
            var scripts = _ghostObject.GetComponentsInChildren<MonoBehaviour>();
            foreach (var s in scripts) Object.Destroy(s);

            // If you use NavMesh, remove obstacles too
            // var navObstacles = ghostObject.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>();
            // foreach (var n in navObstacles) Object.Destroy(n);

            // 4. Cache Renderers for performance
            _renderers = _ghostObject.GetComponentsInChildren<Renderer>();

            // 5. Initialize Color
            SetState(true, true); // Force update
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

            // Optimization: Don't change materials if state hasn't changed
            if (!forceUpdate && isValid == _lastValidityState) return;

            _lastValidityState = isValid;
            var targetMat = isValid ? _validMaterial : _invalidMaterial;

            foreach (var r in _renderers)
            {
                if (!r) continue;

                // Handle objects with multiple sub-meshes (materials)
                // We need to create a new array matching the length of the original
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

#endregion