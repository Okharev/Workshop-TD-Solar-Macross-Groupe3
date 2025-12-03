using UnityEngine;
using UnityEngine.UIElements;
using Placement; // Pour DestructibleObjective
using Enemy;     // Pour HealthComponent

public class ObjectivesPanelController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument _uiDocument;

    [Header("Objectives to Track")]
    [SerializeField] private DestructibleObjective _mainBase;
    [SerializeField] private DestructibleObjective _northPylon;
    [SerializeField] private DestructibleObjective _southPylon;

    private HealthBarView _northView;
    private HealthBarView _eastView;
    private HealthBarView _westView;

    private void OnEnable()
    {
        var root = _uiDocument.rootVisualElement;

        _northView = new HealthBarView(root.Q("Bar_North"), "North Pylon", _mainBase);
        _eastView = new HealthBarView(root.Q("Bar_East"), "East Pylon", _northPylon);
        _westView = new HealthBarView(root.Q("Bar_West"), "West Pylon", _southPylon);
    }

    // Classe interne pour encapsuler la logique d'une barre unique (Pattern View-Wrapper)
    private class HealthBarView
    {
        private VisualElement _barFill;
        private Label _label;
        private int _maxHealth;

        public HealthBarView(VisualElement rootElement, string displayName, DestructibleObjective objective)
        {
            if (rootElement == null) return;

            _label = rootElement.Q<Label>("ObjectiveLabel");
            _barFill = rootElement.Q<VisualElement>("HealthFill");

            if (_label != null) _label.text = displayName;

            if (objective)
            {
                var health = objective.GetComponent<HealthComponent>();
                
                if (health)
                {
                    _maxHealth = health.MaxHealth;
            
                    health.CurrentHealth.Subscribe(UpdateUI);
            
                    UpdateUI(health.CurrentHealth.Value);
                }
            }
            else
            {
                UpdateUI(0);
                if (_label != null) _label.text = $"{displayName} (Destroyed)";
            }
        }

        private void UpdateUI(int currentHealth)
        {
            if (_barFill == null) return;

            float percent = Mathf.Clamp01((float)currentHealth / _maxHealth);

            _barFill.style.width = Length.Percent(percent * 100f);

            if (percent < 0.3f)
            {
                _barFill.AddToClassList("health-critical");
            }
        }
    }
}