using UI;
using UnityEngine;
using UnityEngine.EventSystems;

// Nécessaire pour éviter de cliquer à travers l'UI

public class SelectionInputHandler : MonoBehaviour
{
    [Header("Configuration")] [SerializeField]
    private LayerMask selectionLayer; // Pour ne cliquer que sur les calques "Units" ou "Buildings"

    private UnityEngine.Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = UnityEngine.Camera.main;
    }

    private void Update()
    {
        // 1. Détecter le clic gauche de la souris
        if (Input.GetMouseButtonDown(0)) HandleSelection();
    }

    private void HandleSelection()
    {
        // 2. IMPORTANT : Empêcher le clic si la souris est sur l'UI
        // Note : Assure-toi d'avoir un EventSystem dans ta scène
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

        // 3. Créer le rayon depuis la position de la souris
        var ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // 4. Lancer le rayon
        if (Physics.Raycast(ray, out hit, 1000f, selectionLayer))
        {
            // 5. Vérifier si l'objet touché implémente ISelectable
            // TryGetComponent est très performant et évite les erreurs null
            if (hit.collider.TryGetComponent(out ISelectable selectedObject))
                // C'est une cible valide (Tour, Ennemi) -> On notifie le système
                SelectionManager.Select(selectedObject);
            else
                // On a touché un objet physique (ex: le sol) mais qui n'est pas sélectionnable
                SelectionManager.Deselect();
        }
        else
        {
            // On a cliqué dans le vide (ciel) -> Désélectionner
            SelectionManager.Deselect();
        }
    }
}