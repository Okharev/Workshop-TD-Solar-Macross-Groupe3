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
        ValidationResult Validate(Vector3 position, Quaternion rotation, BuildingSo data);
    }

    // 1. Composite (Holds other validators)
    public class CompositeValidator : IPlacementValidator
    {
        private readonly List<IPlacementValidator> validators = new();

        public ValidationResult Validate(Vector3 p, Quaternion r, BuildingSo d)
        {
            foreach (var v in validators)
            {
                var result = v.Validate(p, r, d);
                if (!result.IsValid) return result;
            }

            return ValidationResult.Success();
        }

        public void AddValidator(IPlacementValidator v)
        {
            validators.Add(v);
        }
    }

    // 2. Physics (Existing Logic)
    public class PhysicsValidator : IPlacementValidator
    {
        private readonly Collider[] cache = new Collider[1];
        private readonly LayerMask mask;
        private readonly float padding;

        public PhysicsValidator(LayerMask mask, float padding)
        {
            this.mask = mask;
            this.padding = padding;
        }

        public ValidationResult Validate(Vector3 pos, Quaternion rot, BuildingSo data)
        {
            // Assuming the prefab has a BoxCollider at root or first child
            var refCol = data.prefab.GetComponent<BoxCollider>();
            if (!refCol) refCol = data.prefab.GetComponentInChildren<BoxCollider>();

            if (refCol == null) return ValidationResult.Success(); // No collider to check

            var center = pos + rot * refCol.center;
            var halfExtents = refCol.size * (0.5f * padding);

            if (Physics.OverlapBoxNonAlloc(center, halfExtents, cache, rot, mask) > 0)
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
        private readonly LayerMask energyLayer;

        public AdditiveEnergyValidator(LayerMask layer)
        {
            energyLayer = layer;
        }

        public ValidationResult Validate(Vector3 pos, Quaternion rot, BuildingSo data)
        {
            var hits = Physics.OverlapSphere(pos, 0.5f, energyLayer);
            var totalAvailable = 0;
            
            // HashSet to prevent double counting the same generator
            var checkedProducers = new HashSet<IEnergyProducer>();

            foreach (var hit in hits)
            {
                IEnergyProducer provider = null;

                // 1. Direct check
                provider = hit.GetComponent<IEnergyProducer>();

                // 2. Link check
                if (provider == null)
                {
                    var link = hit.GetComponent<EnergyFieldLink>();
                    if (link != null) provider = link.GetProducer();
                }

                // 3. Add unique energy
                if (provider != null)
                {
                    // If we haven't counted this specific producer yet...
                    if (!checkedProducers.Contains(provider))
                    {
                        totalAvailable += provider.GetAvailableEnergy();
                        checkedProducers.Add(provider);
                    }
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
        private BuildingSo currentBuilding;
        private float currentRotationY;

        // Dependencies (Helpers)
        private PlacementGhost ghostHelper;
        private bool isPlacementMode;
        private UnityEngine.Camera mainCamera;
        private IPlacementValidator validator; // The Strategy Pattern

        private void Awake()
        {
            mainCamera = UnityEngine.Camera.main;

            energyLayerMask = LayerMask.GetMask("PowerGrid");
            obstacleLayerMask = LayerMask.GetMask("PlacementBlockers", "Roads");
            terrainLayerMask = LayerMask.GetMask("Terrain"); 
            
            // Initialize Ghost Helper
            ghostHelper = new PlacementGhost(validPreviewMat, invalidPreviewMat);

            // Setup Validation Strategy (Composite)
            var composite = new CompositeValidator();

            // 1. Check if we have Money
            // composite.AddValidator(new EconomyValidator());

            // 2. Check if space is empty (Physics)
            composite.AddValidator(new PhysicsValidator(obstacleLayerMask, overlapCheckPadding));

            // 3. Check if we have Power (Energy)
            composite.AddValidator(new AdditiveEnergyValidator(energyLayerMask));

            validator = composite;
        }

        // Events for UI
        public event Action OnPlacementStarted;
        public event Action OnPlacementEnded;
        public event Action<int> OnBuildingPlaced; // Pass cost/id

        #region Public API

        public void StartPlacement(BuildingSo blueprint)
        {
            if (blueprint == null) return;
            StopPlacement(); 

            currentBuilding = blueprint;
            isPlacementMode = true;

            ghostHelper.CreateGhost(blueprint.prefab.gameObject);
            
            // --- NEW: Enable Heatmap if the building needs power ---
            if (blueprint.energyDrain > 0)
            {
                EnergyHeatmapSystem.Instance?.ToggleHeatmap(true);
            }
            // -------------------------------------------------------
        
            OnPlacementStarted?.Invoke();
        }


        public void StopPlacement()
        {
            isPlacementMode = false;
            currentBuilding = null;
            ghostHelper.ClearGhost();
            
            // --- NEW: Hide Heatmap ---
            EnergyHeatmapSystem.Instance?.ToggleHeatmap(false);
            // -------------------------
        
            OnPlacementEnded?.Invoke();
        }
        #endregion

        #region Core Loop

        private void Update()
        {
            if (!isPlacementMode || currentBuilding == null) return;

            HandleInput();

            // 1. Find Mouse Position on Terrain
            var targetPos = GetMouseWorldPosition();

            if (targetPos.HasValue)
            {
                var position = targetPos.Value;
                var rotation = Quaternion.Euler(0, currentRotationY, 0);

                // 2. Update Ghost Position
                ghostHelper.UpdatePosition(position, rotation);

                // 3. Validate
                var result = validator.Validate(position, rotation, currentBuilding);
                ghostHelper.SetState(result.IsValid);

                // 4. Place Logic (Left Click)
                if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                {
                    if (result.IsValid)
                        PlaceTower(position, rotation);
                    else
                        Debug.Log($"Placement Failed: {result.Message}");
                    // Optional: Play Error Sound or Shake Camera
                }
            }
            else
            {
                ghostHelper.Hide();
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
            if (Mathf.Abs(scrollDelta) > 0.1f) currentRotationY += Mathf.Sign(scrollDelta) * rotationSpeed;
        }

        private void PlaceTower(Vector3 position, Quaternion rotation)
        {
            // 1. Spend Money
            // Assuming CurrencySystem is a Singleton as discussed
            // CurrencySystem.Instance.Spend(currentBuilding.cost); 
            // (Commented out until you link your CurrencySystem)

            // 2. Instantiate Real Object
            var newObj = Instantiate(currentBuilding.prefab.gameObject, position, rotation);

            // 3. Connect Energy (The Consumer Logic)
            // If the building has an EnergyConsumer component, it needs to register with the grid.
            // Note: If EnergyConsumer uses Start(), this happens automatically. 
            // If we need to "Pre-Deduct" energy from the grid logic we discussed earlier:
            ConsumeEnergyResources(position, currentBuilding, newObj);

            Debug.Log($"Placed {currentBuilding.name}");
            OnBuildingPlaced?.Invoke(currentBuilding.cost);

            // Optional: Continue placement (Shift key logic) or Stop
            // StopPlacement();
        }

        #endregion

        #region Helpers

        private Vector3? GetMouseWorldPosition()
        {
            var mousePos = Mouse.current.position.ReadValue();
            var ray = mainCamera.ScreenPointToRay(mousePos);

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
            var consumer = instance.GetComponent<IEnergyConsumer>();
            // consumer?.ConnectToPower(); // Assuming you made this public
        }

        #endregion
    }

    // ==========================================================
    // SUB-SYSTEMS (Could be in separate files for cleaner project)
    // ==========================================================

    #region Helper Classes (Ghost)


    public class PlacementGhost
    {
        // Settings
        private readonly Material validMaterial;
        private readonly Material invalidMaterial;

        // State
        private GameObject ghostObject;
        private Renderer[] renderers;
        private bool lastValidityState = true;

        public PlacementGhost(Material validMat, Material invalidMat)
        {
            this.validMaterial = validMat;
            this.invalidMaterial = invalidMat;
        }

        /// <summary>
        /// The "Fabric" method: Creates the visual representation and strips logic.
        /// </summary>
        public void CreateGhost(GameObject prefab)
        {
            // 1. Clean up existing ghost if any
            ClearGhost();

            // 2. Instantiate
            ghostObject = Object.Instantiate(prefab);
            ghostObject.name = "PlacementGhost";

            // 3. STRIPPING: Remove functional components
            // We don't want the ghost to collide with the mouse raycast
            var colliders = ghostObject.GetComponentsInChildren<Collider>();
            foreach (var c in colliders) Object.Destroy(c);

            // We don't want the ghost to run logic (like shooting or consuming energy)
            var scripts = ghostObject.GetComponentsInChildren<MonoBehaviour>();
            foreach (var s in scripts) Object.Destroy(s);

            // If you use NavMesh, remove obstacles too
            // var navObstacles = ghostObject.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>();
            // foreach (var n in navObstacles) Object.Destroy(n);

            // 4. Cache Renderers for performance
            renderers = ghostObject.GetComponentsInChildren<Renderer>();

            // 5. Initialize Color
            SetState(true, true); // Force update
        }

        public void UpdatePosition(Vector3 position, Quaternion rotation)
        {
            if (ghostObject == null) return;

            ghostObject.transform.position = position;
            ghostObject.transform.rotation = rotation;

            if (!ghostObject.activeSelf) ghostObject.SetActive(true);
        }

        public void SetState(bool isValid, bool forceUpdate = false)
        {
            if (ghostObject == null || renderers == null) return;
            
            // Optimization: Don't change materials if state hasn't changed
            if (!forceUpdate && isValid == lastValidityState) return;

            lastValidityState = isValid;
            Material targetMat = isValid ? validMaterial : invalidMaterial;

            foreach (var r in renderers)
            {
                if (r == null) continue;

                // Handle objects with multiple sub-meshes (materials)
                // We need to create a new array matching the length of the original
                Material[] newMats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < newMats.Length; i++)
                {
                    newMats[i] = targetMat;
                }
                r.materials = newMats;
            }
        }

        public void Hide()
        {
            if (ghostObject != null) ghostObject.SetActive(false);
        }

        public void ClearGhost()
        {
            if (ghostObject != null)
            {
                Object.Destroy(ghostObject);
                ghostObject = null;
                renderers = null;
            }
        }
    }
}
    #endregion
