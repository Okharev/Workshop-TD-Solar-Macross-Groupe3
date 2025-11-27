using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Placement
{

    public class PlacementManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private LayerMask terrainLayerMask;   // Sur quoi on peut poser (le sol)
        [SerializeField] private LayerMask obstacleLayerMask;  // Ce qui bloque la construction (tours, murs, arbres)
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float overlapCheckPadding = 0.9f; // Réduit légèrement la boite de collision pour être permissif

        [Header("Visuals")]
        [SerializeField] private Material validPreviewMat;
        [SerializeField] private Material invalidPreviewMat;

        // Événements pour l'intégration UI Toolkit future
        public event Action OnPlacementStarted;
        public event Action OnPlacementEnded;

        // État interne
        private BuildingSo selectedBuildingBlueprint;
        private bool isPlacementMode = false;
        private Camera mainCamera;
        private Collider[] colliderCache = new Collider[1];
        
        // Ghost (Fantôme)
        private GameObject ghostObject;
        private Renderer[] ghostRenderers;
        private BoxCollider prefabColliderReference; // Pour connaitre la taille de la tour
        private float currentRotationY = 0f;
        

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (!isPlacementMode || selectedBuildingBlueprint == null) return;

            HandleInput();
            HandlePlacementLoop();
        }

        #region Public API (Pour l'UI)

        /// <summary>
        /// Appelle cette méthode depuis ton UI pour commencer le placement.
        /// </summary>
        public void StartPlacement(BuildingSo blueprint)
        {
            if (blueprint == null) return;

            StopPlacement(); // Nettoyer l'état précédent si nécessaire

            selectedBuildingBlueprint = blueprint;
            isPlacementMode = true;
            currentRotationY = 0f;

            CreateGhost(blueprint.prefab.gameObject);
            
            // Notifier l'UI que le placement a commencé (utile pour masquer des panneaux par exemple)
            OnPlacementStarted?.Invoke();
        }

        public void StopPlacement()
        {
            ClearGhost();
            selectedBuildingBlueprint = null;
            isPlacementMode = false;
            prefabColliderReference = null;
            
            OnPlacementEnded?.Invoke();
        }

        #endregion

        #region Core Logic

        private void HandleInput()
        {
            // Annulation
            if (Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                StopPlacement();
                return;
            }

            // Rotation (Molette de la souris)
            float scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollDelta) > 0.1f)
            {
                // On normalise la vitesse de rotation
                currentRotationY += Mathf.Sign(scrollDelta) * rotationSpeed; 
            }
        }

        private void HandlePlacementLoop()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            // 1. Trouver le point sur le sol
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, terrainLayerMask))
            {
                Vector3 targetPosition = hit.point;

                // Mettre à jour la position et rotation du fantôme
                if (ghostObject)
                {
                    ghostObject.transform.position = targetPosition;
                    ghostObject.transform.rotation = Quaternion.Euler(0, currentRotationY, 0);
                    ghostObject.SetActive(true);
                }

                // 2. Vérifier si l'emplacement est valide
                bool isValid = IsValidPlacement(targetPosition, currentRotationY);
                UpdateGhostColor(isValid);

                // 3. Placer la tour (Clic Gauche)
                if (isValid && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    // Petit délai anti-clic accidentel si besoin, sinon placement direct
                    PlaceTower(targetPosition, currentRotationY);
                }
            }
            else
            {
                // Si la souris n'est pas sur le terrain, on cache le fantôme
                if (ghostObject) ghostObject.SetActive(false);
            }
        }

        private bool IsValidPlacement(Vector3 position, float rotationY)
        {

            if (!prefabColliderReference) return true;

            Vector3 center = position + (Quaternion.Euler(0, rotationY, 0) * prefabColliderReference.center);
            Vector3 halfExtents = prefabColliderReference.size * (0.5f * overlapCheckPadding);
            
            var size = Physics.OverlapBoxNonAlloc(center, halfExtents, colliderCache, Quaternion.Euler(0, rotationY, 0), obstacleLayerMask);

            return size == 0;
        }

        private void PlaceTower(Vector3 position, float rotationY)
        {
            // Dépense

            // Instantiation
            Instantiate(
                selectedBuildingBlueprint.prefab.gameObject,
                position,
                Quaternion.Euler(0, rotationY, 0)
            );

            Debug.Log($"Placed {selectedBuildingBlueprint.name} (Free)");

            // Optionnel : Arrêter le placement après une pose
            // StopPlacement(); 
        }

        #endregion

        #region Visuals (Ghost)

        private void CreateGhost(GameObject prefab)
        {
            // Récupérer le collider du PREFAB original pour les calculs de collision plus tard
            prefabColliderReference = prefab.GetComponent<BoxCollider>();
            if (!prefabColliderReference)
            {
                prefabColliderReference = prefab.GetComponentInChildren<BoxCollider>();
            }

            // Création visuelle
            ghostObject = Instantiate(prefab);
            ghostObject.name = "PlacementGhost";

            // Nettoyage des composants fonctionnels sur le fantôme
            // On enlève les Colliders pour que le Raycast de la souris traverse le fantôme et touche le sol
            foreach (var c in ghostObject.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var s in ghostObject.GetComponentsInChildren<MonoBehaviour>()) Destroy(s);
            // Si tu as un NavMeshObstacle, détruis-le aussi
            // foreach (var n in ghostObject.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()) Destroy(n);

            ghostRenderers = ghostObject.GetComponentsInChildren<Renderer>();
            UpdateGhostColor(true);
        }

        private void UpdateGhostColor(bool isValid)
        {
            if (ghostRenderers == null) return;
            Material targetMat = isValid ? validPreviewMat : invalidPreviewMat;

            foreach (var r in ghostRenderers)
            {
                // Création d'un tableau temporaire pour remplacer tous les matériaux
                var mats = new Material[r.sharedMaterials.Length];
                for (var i = 0; i < mats.Length; i++) mats[i] = targetMat;
                r.materials = mats;
            }
        }

        private void ClearGhost()
        {
            if (ghostObject)
            {
                Destroy(ghostObject);
                ghostObject = null;
                ghostRenderers = null;
            }
        }

        #endregion
    }
}
