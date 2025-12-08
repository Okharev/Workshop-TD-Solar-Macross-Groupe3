using System;
using Towers;
using UnityEngine;
using UnityEngine.EventSystems;

// Nécessaire pour éviter de cliquer à travers l'UI

namespace UI
{
    public static class SelectionManager
    {
        private static ISelectable _currentSelection;

        public static event Action<ISelectable> OnObjectSelected;
        public static event Action OnDeselected;

        public static void Select(ISelectable newSelection)
        {
            if (_currentSelection == newSelection) return;

            _currentSelection?.OnDeselect();

            _currentSelection = newSelection;
            _currentSelection.OnSelect();

            OnObjectSelected?.Invoke(newSelection);
        }

        public static void Deselect()
        {
            if (_currentSelection != null)
            {
                _currentSelection.OnDeselect();
                _currentSelection = null;
            }

            OnDeselected?.Invoke();
        }
    }
    
    public sealed class SelectionInputHandler : MonoBehaviour
    {
        [Header("Configuration")] [SerializeField]
        private LayerMask selectionLayer;

        private UnityEngine.Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = UnityEngine.Camera.main;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0)) HandleSelection();
        }

        private void HandleSelection()
        {

            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

            var ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1000f, selectionLayer))
            {
  
                if (hit.collider.TryGetComponent(out ISelectable selectedObject))
                    SelectionManager.Select(selectedObject);
                else
                    SelectionManager.Deselect();
            }
            else
            {
                SelectionManager.Deselect();
            }
        }
    }
}