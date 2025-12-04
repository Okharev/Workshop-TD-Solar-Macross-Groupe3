using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildingBarPanelController : MonoBehaviour
{
    [Header("UI Document")] [SerializeField]
    private UIDocument _uiDocument;

    [Header("Data")] [SerializeField] private List<BuildingData> _availableBuildings;

    private VisualElement _slotsContainer;
    private VisualElement _tooltipContainer;
    private Label _tooltipCost;
    private Label _tooltipDesc;
    private Label _tooltipTitle;

    private void OnEnable()
    {
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();

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
            var button = new Button();
            button.AddToClassList("building-slot");

            if (building.Icon != null)
                button.style.backgroundImage = new StyleBackground(building.Icon);
            else
                button.text = building.Name.Substring(0, 1); // Fallback: première lettre


            button.RegisterCallback<MouseEnterEvent>(evt => ShowTooltip(building));

            button.RegisterCallback<MouseLeaveEvent>(evt => HideTooltip());

            button.RegisterCallback<ClickEvent>(evt => SelectBuilding(building));

            _slotsContainer.Add(button);
        }
    }

    private void ShowTooltip(BuildingData data)
    {
        _tooltipTitle.text = data.Name.ToUpper();
        _tooltipCost.text = $"{data.Cost} CREDITS";
        _tooltipDesc.text = data.Description;

        _tooltipContainer.style.display = DisplayStyle.Flex;
        _tooltipContainer.RemoveFromClassList("tooltip-hidden");
    }

    private void HideTooltip()
    {
        _tooltipContainer.AddToClassList("tooltip-hidden");

    }

    private void SelectBuilding(BuildingData data)
    {
        Debug.Log($"Bâtiment sélectionné : {data.Name}");
    }

    [Serializable]
    public struct BuildingData
    {
        public string Name;
        public int Cost;
        public string Description;
        public Sprite Icon;
    }
}