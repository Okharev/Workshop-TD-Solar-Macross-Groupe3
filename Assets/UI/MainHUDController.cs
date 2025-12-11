using System;
using System.Collections.Generic;
using Camera;
using Economy;
using Enemy;
using Placement;
using Towers;
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

            _actionsContainer.Clear();
            var actions = target.GetInteractions();
            if (actions != null)
                foreach (var action in actions)
                {
                    var btn = new Button(action.OnExecute) { text = action.Label };
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

                if (obj && obj.TryGetComponent<HealthComponent>(out var health))
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
        private readonly Action<BuildingEntity> _onBuildingSelected;
        private readonly VisualElement _root;
        private readonly VisualElement _slotsContainer;

        private readonly VisualElement _tooltipContainer;
        private readonly Label _tooltipCost;
        private readonly Label _tooltipDesc;
        private readonly Label _tooltipTitle;

        public BuildingBarView(VisualElement rootElement, List<BuildingEntity> buildings, Action<BuildingEntity> onSelect)
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

        private void GenerateButtons(List<BuildingEntity> buildings)
        {
            _slotsContainer.Clear();

            foreach (var building in buildings)
            {
                var button = new Button();
                button.AddToClassList("building-slot");

                // Gestion de l'icône ou texte par défaut
                if (building.icon)
                    button.style.backgroundImage = new StyleBackground(building.icon);
                else
                    button.text = building.name[..1];

                button.RegisterCallback<MouseEnterEvent>(evt => ShowTooltip(building));
                button.RegisterCallback<MouseLeaveEvent>(evt => HideTooltip());
                button.RegisterCallback<ClickEvent>(evt => _onBuildingSelected?.Invoke(building));

                _slotsContainer.Add(button);
            }
        }

        private void ShowTooltip(BuildingEntity data)
        {
            if (_tooltipContainer == null) return;

            _tooltipTitle.text = data.name.ToUpper();
            _tooltipCost.text = $"{data.cost} CREDITS";
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
    
    public class WavePanelView
    {
        private readonly WaveManager _manager;
        
        // UI Elements
        private readonly VisualElement _container;
        private readonly Label _waveTitleLabel;
        
        // Combat Elements
        private readonly VisualElement _combatContainer;
        private readonly Label _enemyCountLabel;
        private readonly ProgressBar _waveProgressBar;

        // Build Elements
        private readonly VisualElement _buildContainer;
        private readonly Label _timerLabel;
        private readonly Button _startWaveButton;

        public WavePanelView(VisualElement root, WaveManager manager)
        {
            _manager = manager;
            
            // 1. Récupération des éléments UI
            // Note: Assure-toi que les noms correspondent à ton UXML
            _container = root.Q("WaveInfoContainer"); // Ou le nom de ton instance
            
            // Si le UXML est directement instancié ou si on cherche dans le root global :
            if (_container == null) _container = root; 

            _waveTitleLabel = _container.Q<Label>("WaveTitleLabel");
            
            _combatContainer = _container.Q("CombatStateContainer");
            _enemyCountLabel = _container.Q<Label>("EnemyCountLabel");
            _waveProgressBar = _container.Q<ProgressBar>("WaveProgressBar");

            _buildContainer = _container.Q("BuildStateContainer");
            _timerLabel = _container.Q<Label>("TimerLabel");
            _startWaveButton = _container.Q<Button>("StartWaveButton");

            // 2. Setup du bouton
            if (_startWaveButton != null)
            {
                _startWaveButton.clicked += OnNextWaveClicked;
            }

            // 3. Abonnements aux événements du WaveManager
            if (_manager)
            {
                _manager.OnWaveStarted += HandleWaveStarted;
                _manager.OnWaveFinished += HandleWaveFinished;
                _manager.OnAllWavesCompleted += HandleAllWavesCompleted;
                
                // Abonnements aux valeurs réactives (ReactiveInt / ReactiveFloat)
                _manager.enemiesRemaining.Subscribe(UpdateEnemyCount);
                _manager.timeToNextWave.Subscribe(UpdateTimer);
                
                // Initialisation de l'état
                UpdateVisualState(_manager.IsWaveActive);
                RefreshTitle();
            }
        }

        public void Dispose()
        {
            if (_startWaveButton != null) _startWaveButton.clicked -= OnNextWaveClicked;
            
            if (_manager)
            {
                _manager.OnWaveStarted -= HandleWaveStarted;
                _manager.OnWaveFinished -= HandleWaveFinished;
                _manager.OnAllWavesCompleted -= HandleAllWavesCompleted;
                
                // Note : Si tes ReactiveInt ont une méthode Unsubscribe, utilise-la ici.
                // Sinon, assure-toi que le WaveManager gère le nettoyage ou que l'Action est nettoyée.
                // _manager.enemiesRemaining.Unsubscribe(UpdateEnemyCount);
                // _manager.timeToNextWave.Unsubscribe(UpdateTimer);
            }
        }

        private void OnNextWaveClicked()
        {
            // Lance la vague (première ou suivante)
            if (_manager && !_manager.IsWaveActive) 
            {
                _manager.StartNextWave();
            }
        }

        // --- Mises à jour UI ---

        private void UpdateEnemyCount(int remaining)
        {
            if (!_manager.IsWaveActive) return;

            int total = _manager.totalEnemiesInWave.Value;
            // Éviter la division par zéro
            if (total <= 0) total = 1; 

            if (_enemyCountLabel != null)
                _enemyCountLabel.text = $"Enemies: {remaining} / {total}";

            if (_waveProgressBar != null)
            {
                float progress = 1f - ((float)remaining / total);
                _waveProgressBar.value = progress * 100f; // Si ProgressBar utilise %
                _waveProgressBar.title = $"{Mathf.RoundToInt(progress * 100)}%";
            }
        }

        private void UpdateTimer(float timeRemaining)
        {
            if (_manager.IsWaveActive) return;

            // Formater le temps (ex: 15.4s)
            if (_timerLabel != null)
            {
                _timerLabel.text = $"Next wave in: {timeRemaining:F1}s";
            }
        }

        private void UpdateVisualState(bool isCombat)
        {
            if (isCombat)
            {
                _combatContainer.style.display = DisplayStyle.Flex;
                _buildContainer.style.display = DisplayStyle.None;
            }
            else
            {
                _combatContainer.style.display = DisplayStyle.None;
                _buildContainer.style.display = DisplayStyle.Flex;
                // Met à jour le texte du bouton selon si c'est le début ou entre les vagues
                if (_startWaveButton != null)
                    _startWaveButton.text = _manager.CurrentWaveIndex == -1 ? "START GAME" : "START NEXT WAVE";
            }
        }
        
        private void RefreshTitle()
        {
            if (_waveTitleLabel == null) return;
            
            int displayIndex = _manager.CurrentWaveIndex + 1;
            // Si on n'a pas encore commencé, on affiche Wave 1 par défaut ou "Ready"
            if (displayIndex == 0) displayIndex = 1; 
            
            _waveTitleLabel.text = $"WAVE {displayIndex}";
        }

        // --- Event Handlers ---

        private void HandleWaveStarted(int index, string name)
        {
            RefreshTitle();
            UpdateVisualState(true); // Passe en mode Combat
        }

        private void HandleWaveFinished()
        {
            UpdateVisualState(false); // Passe en mode Build/Timer
        }

        private void HandleAllWavesCompleted()
        {
            if (_waveTitleLabel != null) _waveTitleLabel.text = "VICTORY!";
            _combatContainer.style.display = DisplayStyle.None;
            _buildContainer.style.display = DisplayStyle.None;
        }
    }
    
    [RequireComponent(typeof(UIDocument))]
    public class MainHUDController : MonoBehaviour
    {
        [Header("Game Dependencies")] [SerializeField]
        private WaveManager _waveManager;
        
        [SerializeField] private HealthComponent _nexus;

        [SerializeField] private DestructibleObjective _mainBase;
        [SerializeField] private DestructibleObjective _northPylon;
        [SerializeField] private DestructibleObjective _southPylon;
        [Header("Minimap")]
        [SerializeField] private RenderTexture _minimapTexture;
        [Header("Currency")]
        [SerializeField] private CurrencyManager _currencyManager;

        [Header("Minimap Interaction")]
        [SerializeField] private UnityEngine.Camera _minimapCamera; // Référence à la caméra Ortho du ciel
        [SerializeField] private FreeFlyCamera _playerCamera;
        
        [Header("Building System")]
        [SerializeField]
        private List<BuildingEntity> _availableBuildings;

        private BuildingBarView _buildingBar;

        // Les références aux sous-vues
        private InfoPanelView _infoPanel;
        private ObjectivesPanelView _objectivesPanel;
        private WavePanelView _wavePanel;
        private RadialProgress _nexusPanel;
        private Label _labelAmount;

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;

            var nexusRoot = root.Q("NexusHealthInstance");
            if (nexusRoot != null)
                _wavePanel = new WavePanelView(nexusRoot, _waveManager);
            
            // 1. Initialiser le Wave Panel
            var waveRoot = root.Q("WaveInfoContainer") ?? root.Q("WavePanelInstance");
            
            if (waveRoot != null)
                _wavePanel = new WavePanelView(waveRoot, _waveManager);
            else
                Debug.LogWarning("WavePanel UI not found!");

            // 2. Initialiser l'Info Panel
            var infoRoot = root.Q("InfoPanelInstance");
            if (infoRoot != null)
                _infoPanel = new InfoPanelView(infoRoot);

            // 3. Initialiser les Objectifs
            _objectivesPanel = new ObjectivesPanelView(root, _mainBase, _northPylon, _southPylon);

            _nexusPanel = root.Q<RadialProgress>("NexusHealth");
            _nexusPanel.dataSource = _nexus;
            
            _labelAmount = root.Q<Label>("Amount");
            _labelAmount.dataSource = _currencyManager;

            var buildingRoot = root.Q("BuildingBarInstance");
            if (buildingRoot != null)
            {
                Debug.Log("sdfsdfsdfsdfs");
                _buildingBar = new BuildingBarView(buildingRoot, _availableBuildings, OnBuildingSelected);
            }
            else
            {
                Debug.LogWarning("BuildingBarInstance introuvable dans le UXML.");
            }
            
            var minimapRender = root.Q<VisualElement>("MinimapRender");
            if (minimapRender != null && _minimapTexture != null)
            {
                minimapRender.style.backgroundImage = Background.FromRenderTexture(_minimapTexture);
                
                // Enregistrement du clic (PointerDown est mieux que Click pour la réactivité)
                minimapRender.RegisterCallback<PointerDownEvent>(evt => OnMinimapClicked(evt, minimapRender));
            }
        }
        
        private void OnMinimapClicked(PointerDownEvent evt, VisualElement element)
        {
            if (_minimapCamera == null || _playerCamera == null) return;

            // 1. Convertir la position de la souris (pixels locaux) en coordonnées normalisées (0 à 1)
            // L'origine (0,0) dans UI Toolkit est en HAUT à gauche.
            Vector2 localPos = evt.localPosition;
            float normalizedX = localPos.x / element.contentRect.width;
            float normalizedY = localPos.y / element.contentRect.height;

            // 2. Convertir en Viewport Point pour la caméra Minimap
            // Dans Unity Caméra, (0,0) est en BAS à gauche. Il faut inverser Y.
            Vector3 viewportPoint = new Vector3(normalizedX, 1f - normalizedY, 0f);

            // 3. Convertir en point dans le monde
            // ViewportToWorldPoint projette depuis la caméra.
            // Z correspond à la distance depuis la caméra. Comme elle est en haut (ex: Y=100), 
            // on veut projeter sur le sol.
            
            // Méthode Raycast (plus précise) :
            Ray ray = _minimapCamera.ViewportPointToRay(viewportPoint);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // Plan au sol (Y=0)

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 targetWorldPos = ray.GetPoint(enter);
                
                // 4. Déplacer le joueur
                _playerCamera.TeleportTo(targetWorldPos);
                
                Debug.Log($"Minimap Clicked! Moving to {targetWorldPos}");
            }
        }

        private void OnDisable()
        {
            _infoPanel?.Dispose();
            _wavePanel?.Dispose();
        }

        private void OnBuildingSelected(BuildingEntity data)
        {
            Debug.Log($"[MainHUD] Joueur veut construire : {data.name} pour {data.cost} or.");

            PlacementManager.Instance.StartPlacement(data);
        }
    }
}