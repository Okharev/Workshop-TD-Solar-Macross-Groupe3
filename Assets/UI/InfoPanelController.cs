using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public struct InteractionButton
    {
        public string Label;
        public Action OnClick;
    }

    public interface ISelectable
    {
        string DisplayName { get; }
        string Description { get; }

        Dictionary<string, string> GetStats();

        List<InteractionButton> GetInteractions();

        void OnSelect();
        void OnDeselect();
    }


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

    public class InfoPanelController : MonoBehaviour
    {
        private VisualElement _actionsContainer;
        private Label _descLabel;
        private UIDocument _document;
        private VisualElement _panel;
        private VisualElement _statsContainer;
        private Label _titleLabel;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;

            // Récupération des références UXML
            _panel = root.Q<VisualElement>("SidePanel");
            _titleLabel = _panel.Q<Label>("Title");
            _descLabel = _panel.Q<Label>("Description");
            _statsContainer = _panel.Q<VisualElement>("StatsContainer");
            _actionsContainer = _panel.Q<VisualElement>("ActionsContainer");
        }

        private void OnEnable()
        {
            SelectionManager.OnObjectSelected += ShowPanel;
            SelectionManager.OnDeselected += HidePanel;
        }

        private void OnDisable()
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
                statLabel.AddToClassList("stat-text"); // Ajouter du style USS si besoin
                _statsContainer.Add(statLabel);
            }

            _actionsContainer.Clear();
            var actions = target.GetInteractions();

            if (actions != null)
                foreach (var action in actions)
                {
                    var btn = new Button(action.OnClick);
                    btn.text = action.Label;
                    btn.AddToClassList("action-button"); // Style USS
                    _actionsContainer.Add(btn);
                }

            _panel.AddToClassList("side-panel--open");
        }

        private void HidePanel()
        {
            _panel.RemoveFromClassList("side-panel--open");
        }
    }
}