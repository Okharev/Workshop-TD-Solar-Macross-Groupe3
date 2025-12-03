using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildingBarPanelController : MonoBehaviour
{
    [Header("UI Document")] [SerializeField]
    private UIDocument _uiDocument;

    [Header("Data")] [SerializeField] private List<BuildingData> _availableBuildings;

    // Références UI
    private VisualElement _slotsContainer;
    private VisualElement _tooltipContainer;
    private Label _tooltipCost;
    private Label _tooltipDesc;
    private Label _tooltipTitle;

    private void OnEnable()
    {
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();

        // Attendre que l'UI soit chargée
        var root = _uiDocument.rootVisualElement;

        _slotsContainer = root.Q<VisualElement>("SlotsContainer");
        _tooltipContainer = root.Q<VisualElement>("TooltipContainer");
        _tooltipTitle = root.Q<Label>("TooltipTitle");
        _tooltipCost = root.Q<Label>("TooltipCost");
        _tooltipDesc = root.Q<Label>("TooltipDesc");

        GenerateButtons();
    }

    private void GenerateButtons()
    {
        _slotsContainer.Clear();

        foreach (var building in _availableBuildings)
        {
            // 1. Création du bouton
            var button = new Button();
            button.AddToClassList("building-slot");

            // Si tu as une icône, tu peux l'ajouter en background :
            if (building.Icon != null)
                button.style.backgroundImage = new StyleBackground(building.Icon);
            else
                button.text = building.Name.Substring(0, 1); // Fallback: première lettre

            // 2. Gestion des événements (Hover et Click)

            // Entrée de la souris : Afficher Tooltip
            button.RegisterCallback<MouseEnterEvent>(evt => ShowTooltip(building));

            // Sortie de la souris : Cacher Tooltip
            button.RegisterCallback<MouseLeaveEvent>(evt => HideTooltip());

            // Clic : Sélectionner (Action à définir)
            button.RegisterCallback<ClickEvent>(evt => SelectBuilding(building));

            _slotsContainer.Add(button);
        }
    }

    private void ShowTooltip(BuildingData data)
    {
        _tooltipTitle.text = data.Name.ToUpper();
        _tooltipCost.text = $"{data.Cost} CREDITS";
        _tooltipDesc.text = data.Description;

        // Retirer la classe 'hidden' et assurer l'affichage
        _tooltipContainer.style.display = DisplayStyle.Flex;
        _tooltipContainer.RemoveFromClassList("tooltip-hidden");
    }

    private void HideTooltip()
    {
        _tooltipContainer.AddToClassList("tooltip-hidden");
        // On attend la fin de l'animation CSS (optionnel, ou on masque direct)
        // Pour l'instant, le CSS gère l'opacité, mais on peut ajouter un délai si besoin.
    }

    private void SelectBuilding(BuildingData data)
    {
        Debug.Log($"Bâtiment sélectionné : {data.Name}");
        // Ici, tu appelleras ton système de construction (PlacementSystem)
    }

    // Petite classe simple pour définir nos bâtiments (à remplacer par tes vraies données plus tard)
    [Serializable]
    public struct BuildingData
    {
        public string Name;
        public int Cost;
        public string Description;
        public Sprite Icon; // Optionnel si tu veux afficher une image
    }
}