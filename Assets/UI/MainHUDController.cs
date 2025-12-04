using System;
using System.Collections.Generic;
using Enemy;
using Placement;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [Serializable]
    public struct BuildingData
    {
        public string Name;
        public int Cost;
        [TextArea] public string Description;
        public Sprite Icon;
    }

    public class InfoPanelView
    {
        private readonly VisualElement _actionsContainer;
        private readonly Label _descLabel;
        private readonly VisualElement _panel;
        private readonly VisualElement _statsContainer;
        private readonly Label _titleLabel;

        public InfoPanelView(VisualElement root)
        {
            _panel = root.Q("SidePanel");

            if (_panel == null)
            {
                Debug.LogError("InfoPanel: SidePanel not found!");
                return;
            }

            _titleLabel = _panel.Q<Label>("Title");
            _descLabel = _panel.Q<Label>("Description");
            _statsContainer = _panel.Q<VisualElement>("StatsContainer");
            _actionsContainer = _panel.Q<VisualElement>("ActionsContainer");

            // Events
            SelectionManager.OnObjectSelected += ShowPanel;
            SelectionManager.OnDeselected += HidePanel;
        }

        public void Dispose() 
        {
            SelectionManager.OnObjectSelected -= ShowPanel;
            SelectionManager.OnDeselected -= HidePanel;
        }

        private void ShowPanel(ISelectable target)
        {
            _titleLabel.text = target.DisplayName;
            _descLabel.text = target.Description;

            _statsContainer.Clear();
            foreach (var stat in target.GetStats())
            {
                var statLabel = new Label($"{stat.Key}: {stat.Value}");
                statLabel.AddToClassList("stat-text");
                _statsContainer.Add(statLabel);
            }

            _actionsContainer.Clear();
            var actions = target.GetInteractions();
            if (actions != null)
                foreach (var action in actions)
                {
                    var btn = new Button(action.OnClick) { text = action.Label };
                    btn.AddToClassList("action-button");
                    _actionsContainer.Add(btn);
                }

            _panel.AddToClassList("side-panel--open");
        }

        private void HidePanel()
        {
            _panel.RemoveFromClassList("side-panel--open");
        }
    }

    public class WavePanelView
    {
        private readonly WaveManager _manager;
        private readonly Button _nextWaveButton;
        private readonly Label _statusLabel;
        private readonly Label _waveIndexLabel;
        private readonly Label _waveNameLabel;

        public WavePanelView(VisualElement root, WaveManager manager)
        {
            _manager = manager;
            _statusLabel = root.Q<Label>("wave-status-label");
            _waveIndexLabel = root.Q<Label>("wave-index-label");
            _waveNameLabel = root.Q<Label>("wave-name-label");
            _nextWaveButton = root.Q<Button>("next-wave-btn");

            if (_nextWaveButton != null) _nextWaveButton.clicked += OnNextWaveClicked;

            if (_manager != null)
            {
                _manager.OnWaveStarted += HandleWaveStarted;
                _manager.OnWaveFinished += HandleWaveFinished;
                _manager.OnAllWavesCompleted += HandleAllWavesCompleted;
                UpdateUIState(false);
            }
        }

        public void Dispose()
        {
            if (_nextWaveButton != null) _nextWaveButton.clicked -= OnNextWaveClicked;
            if (_manager != null)
            {
                _manager.OnWaveStarted -= HandleWaveStarted;
                _manager.OnWaveFinished -= HandleWaveFinished;
                _manager.OnAllWavesCompleted -= HandleAllWavesCompleted;
            }
        }

        private void OnNextWaveClicked()
        {
            if (_manager && !_manager.IsWaveActive) _manager.StartNextWave();
        }

        private void HandleWaveStarted(int index, string name)
        {
            _waveIndexLabel.text = $"WAVE {index}";
            _waveNameLabel.text = name;
            _statusLabel.text = "IN PROGRESS";
            _statusLabel.style.color = new StyleColor(Color.red);
            _nextWaveButton?.AddToClassList("hidden");
        }

        private void HandleWaveFinished()
        {
            _statusLabel.text = "COMPLETE";
            _statusLabel.style.color = new StyleColor(Color.green);
            _nextWaveButton?.RemoveFromClassList("hidden");
        }

        private void HandleAllWavesCompleted()
        {
            _statusLabel.text = "VICTORY";
            _nextWaveButton?.AddToClassList("hidden");
        }

        private void UpdateUIState(bool active)
        {
            var idx = _manager.CurrentWaveIndex + 1;
            if (idx == 0) idx = 1;
            _waveIndexLabel.text = $"WAVE {idx}";
            _waveNameLabel.text = "Ready...";
            if (active) _nextWaveButton?.AddToClassList("hidden");
            else _nextWaveButton?.RemoveFromClassList("hidden");
        }
    }

    public class ObjectivesPanelView
    {
        public ObjectivesPanelView(VisualElement root, DestructibleObjective main, DestructibleObjective north,
            DestructibleObjective south)
        {
            new HealthBarRow(root.Q("Bar_North"), "North Pylon", main);
            new HealthBarRow(root.Q("Bar_East"), "East Pylon", north);
            new HealthBarRow(root.Q("Bar_West"), "West Pylon", south);
        }

        private class HealthBarRow
        {
            private readonly VisualElement _barFill;
            private readonly Label _label;
            private readonly int _maxHealth;

            public HealthBarRow(VisualElement root, string name, DestructibleObjective obj)
            {
                if (root == null) return;
                _label = root.Q<Label>("ObjectiveLabel");
                _barFill = root.Q<VisualElement>("HealthFill");

                if (_label != null) _label.text = name;

                if (obj != null && obj.TryGetComponent<HealthComponent>(out var health))
                {
                    _maxHealth = health.MaxHealth;
                    health.CurrentHealth.Subscribe(UpdateUI);
                    UpdateUI(health.CurrentHealth.Value);
                }
                else
                {
                    UpdateUI(0);
                    if (_label != null) _label.text = $"{name} (Destroyed)";
                }
            }

            private void UpdateUI(int current)
            {
                if (_barFill == null) return;
                var p = Mathf.Clamp01((float)current / _maxHealth);
                _barFill.style.width = Length.Percent(p * 100f);
                if (p < 0.3f) _barFill.AddToClassList("health-critical");
                else _barFill.RemoveFromClassList("health-critical");
            }
        }
    }

    public class BuildingBarView
    {
        private readonly Action<BuildingData> _onBuildingSelected;
        private readonly VisualElement _root;
        private readonly VisualElement _slotsContainer;

        private readonly VisualElement _tooltipContainer;
        private readonly Label _tooltipCost;
        private readonly Label _tooltipDesc;
        private readonly Label _tooltipTitle;

        public BuildingBarView(VisualElement rootElement, List<BuildingData> buildings, Action<BuildingData> onSelect)
        {
            _root = rootElement;
            _onBuildingSelected = onSelect;

            _slotsContainer = _root.Q<VisualElement>("SlotsContainer");
            _tooltipContainer = _root.Q<VisualElement>("TooltipContainer");

            _tooltipTitle = _root.Q<Label>("TooltipTitle");
            _tooltipCost = _root.Q<Label>("TooltipCost");
            _tooltipDesc = _root.Q<Label>("TooltipDesc");

            GenerateButtons(buildings);
        }

        private void GenerateButtons(List<BuildingData> buildings)
        {
            _slotsContainer.Clear();

            foreach (var building in buildings)
            {
                var button = new Button();
                button.AddToClassList("building-slot");

                // Gestion de l'icône ou texte par défaut
                if (building.Icon != null)
                    button.style.backgroundImage = new StyleBackground(building.Icon);
                else
                    button.text = building.Name[..1];

                button.RegisterCallback<MouseEnterEvent>(evt => ShowTooltip(building));
                button.RegisterCallback<MouseLeaveEvent>(evt => HideTooltip());
                button.RegisterCallback<ClickEvent>(evt => _onBuildingSelected?.Invoke(building));

                _slotsContainer.Add(button);
            }
        }

        private void ShowTooltip(BuildingData data)
        {
            if (_tooltipContainer == null) return;

            _tooltipTitle.text = data.Name.ToUpper();
            _tooltipCost.text = $"{data.Cost} CREDITS";
            _tooltipDesc.text = data.Description;

            _tooltipContainer.style.display = DisplayStyle.Flex;
            _tooltipContainer.RemoveFromClassList("tooltip-hidden");
        }

        private void HideTooltip()
        {
            if (_tooltipContainer == null) return;
            _tooltipContainer.AddToClassList("tooltip-hidden");
        }
    }

    [RequireComponent(typeof(UIDocument))]
    public class MainHUDController : MonoBehaviour
    {
        [Header("Game Dependencies")] [SerializeField]
        private WaveManager _waveManager;

        [SerializeField] private DestructibleObjective _mainBase;
        [SerializeField] private DestructibleObjective _northPylon;
        [SerializeField] private DestructibleObjective _southPylon;

        [Header("Building System")]
        // Liste configurable dans l'Inspecteur
        [SerializeField]
        private List<BuildingData> _availableBuildings;

        private BuildingBarView _buildingBar; // Nouvelle référence

        // Les références aux sous-vues
        private InfoPanelView _infoPanel;
        private ObjectivesPanelView _objectivesPanel;
        private WavePanelView _wavePanel;

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;

            // 1. Initialiser le Wave Panel
            var waveRoot = root.Q("WavePanelInstance");
            if (waveRoot != null)
                _wavePanel = new WavePanelView(waveRoot, _waveManager);

            // 2. Initialiser l'Info Panel
            var infoRoot = root.Q("InfoPanelInstance");
            if (infoRoot != null)
                _infoPanel = new InfoPanelView(infoRoot);

            // 3. Initialiser les Objectifs
            _objectivesPanel = new ObjectivesPanelView(root, _mainBase, _northPylon, _southPylon);

            var buildingRoot = root.Q("BuildingBarInstance");
            if (buildingRoot != null)
            {
                Debug.Log("sdfsdfsdfsdfs");
                // On passe la racine, la liste des données, et la fonction à appeler lors du clic
                _buildingBar = new BuildingBarView(buildingRoot, _availableBuildings, OnBuildingSelected);
            }
            else
            {
                Debug.LogWarning("BuildingBarInstance introuvable dans le UXML.");
            }
        }

        private void OnDisable()
        {
            _infoPanel?.Dispose();
            _wavePanel?.Dispose();
        }

        private void OnBuildingSelected(BuildingData data)
        {
            Debug.Log($"[MainHUD] Joueur veut construire : {data.Name} pour {data.Cost} or.");

            // TODO : Placement System
        }
    }
}