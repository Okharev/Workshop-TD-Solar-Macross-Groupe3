using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Economy;

public class EnergyDebugToolkit : MonoBehaviour
{
    [Header("UI Setup")]
    public UIDocument uiDocument;
    public float verticalOffset = 2.5f;
    public KeyCode toggleKey = KeyCode.F3;


    private readonly Queue<LabelElement> _pool = new Queue<LabelElement>();
    
    private readonly Dictionary<Component, LabelElement> _activeLabels = new Dictionary<Component, LabelElement>();
    
    private readonly List<Component> _toRemove = new List<Component>();

    private VisualElement _container;
    private UnityEngine.Camera _mainCam;
    private bool _isVisible = true;

    private class LabelElement
    {
        public VisualElement Root; 
        public Label Text;         
    }

    private void OnEnable()
    {
        _mainCam = UnityEngine.Camera.main;
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

        var root = uiDocument.rootVisualElement;
        _container = new VisualElement();
        _container.pickingMode = PickingMode.Ignore;
        _container.style.flexGrow = 1;
        root.Add(_container);
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            _isVisible = !_isVisible;
            _container.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (!_isVisible || !EnergyGridManager.Instance) return;

        _toRemove.Clear();
        foreach (var kvp in _activeLabels)
        {
            if (!kvp.Key || !kvp.Key.gameObject.activeInHierarchy)
            {
                _toRemove.Add(kvp.Key);
            }
        }

        foreach (var deadKey in _toRemove)
        {
            Release(deadKey);
        }

        UpdateList(EnergyGridManager.Instance.AllProducers);
        UpdateList(EnergyGridManager.Instance.AllConsumers);
    }

    private void UpdateList<T>(IEnumerable<T> components) where T : Component
    {
        foreach (var item in components)
        {
            if (item == null || !item.gameObject.activeInHierarchy) continue;

            var labelEl = Acquire(item);

            if (item is EnergyProducer producer)
            {
                UpdateProducerVisuals(labelEl, producer);
            }
            else if (item is EnergyConsumer consumer)
            {
                UpdateConsumerVisuals(labelEl, consumer);
            }

            PositionLabel(labelEl, item.transform.position);
        }
    }
    
    private LabelElement Acquire(Component key)
    {
        if (_activeLabels.TryGetValue(key, out var existing)) return existing;

        LabelElement element;

        if (_pool.Count > 0)
        {
            element = _pool.Dequeue();
            element.Root.style.display = DisplayStyle.Flex;
        }
        else
        {
            element = CreateVisual();
            _container.Add(element.Root);
        }

        _activeLabels.Add(key, element);
        return element;
    }

    private void Release(Component key)
    {
        if (_activeLabels.TryGetValue(key, out var element))
        {
            element.Root.style.display = DisplayStyle.None;
            
            _pool.Enqueue(element);
            
            _activeLabels.Remove(key);
        }
    }


    private LabelElement CreateVisual()
    {
        var box = new VisualElement();
        box.style.position = Position.Absolute;
        box.style.width = 100;
        box.style.height = 50;
        box.style.translate = new StyleTranslate(new Translate(-50, -50, 0)); 
        box.style.backgroundColor = new Color(0, 0, 0, 0.65f);
        box.style.borderTopLeftRadius = 4;
        box.style.borderTopRightRadius = 4;
        box.style.borderBottomRightRadius = 4;
        box.style.borderBottomLeftRadius = 4;
        box.style.justifyContent = Justify.Center;
        box.style.alignItems = Align.Center;

        var label = new Label();
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 11;
        label.style.color = Color.white;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;

        box.Add(label);

        return new LabelElement { Root = box, Text = label };
    }

    private void PositionLabel(LabelElement element, Vector3 worldPos)
    {
        Vector3 targetPos = worldPos + Vector3.up * verticalOffset;
        
        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
            _container.panel, targetPos, _mainCam
        );

        Vector3 direction = targetPos - _mainCam.transform.position;
        bool isBehind = Vector3.Dot(_mainCam.transform.forward, direction) < 0;

        if (isBehind)
        {
             element.Root.style.display = DisplayStyle.None;
        }
        else
        {
            // Si on l'avait caché, on le remonte
            if (element.Root.style.display == DisplayStyle.None) 
                element.Root.style.display = DisplayStyle.Flex;

            element.Root.transform.position = panelPos;
        }
    }

    private void UpdateProducerVisuals(LabelElement el, EnergyProducer p)
    {
        float current = p.CurrentLoad;
        float available = p.GetAvailable();
        float total = current + available;

        string typeName = p.isMobileGenerator ? "GEN" : "PLANT";
        el.Text.text = $"{typeName}\n{current:F0} / {total:F0}";
        el.Text.style.color = available <= 0.01f ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 1f); // Rouge pastel / Cyan pastel
    }

    private void UpdateConsumerVisuals(LabelElement el, EnergyConsumer c)
    {
        bool isPowered = c.IsPowered;
        float req = c.TotalRequirement.Value;

        el.Text.text = isPowered ? "ON" : "OFF";
        el.Text.text += $"\n-{req:F0} PWR";
        el.Text.style.color = isPowered ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f);
    }
}