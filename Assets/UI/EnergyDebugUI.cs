using System.Collections.Generic;
using Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class EnergyDebugUi : MonoBehaviour
    {
        [Header("UI Setup")]
        public UIDocument uiDocument;
        public KeyCode toggleKey = KeyCode.F3;

        [Header("Visual Configuration")]
        [Tooltip("Hauteur ajoutée si aucun 'UI_Anchor' n'est trouvé")]
        public float defaultVerticalOffset = 2.5f;

        [Header("Dynamic Visuals")]
        public float minDistance = 10f;
        public float maxDistance = 50f;
        [Range(0.5f, 2f)] public float scaleAtMinDist = 1.2f;
        [Range(0.5f, 2f)] public float scaleAtMaxDist = 0.7f;

        // --- Tracking Data ---
        private readonly List<Component> _trackedComponents = new List<Component>();
        private readonly Dictionary<Component, LabelElement> _activeLabels = new Dictionary<Component, LabelElement>();
        private readonly Queue<LabelElement> _pool = new Queue<LabelElement>();

        private VisualElement _container;
        private UnityEngine.Camera _mainCam;
        private bool _isVisible = true;

        private class LabelElement
        {
            public VisualElement Root; 
            public Label Text;
            public Transform CachedAnchor; 
            public bool IsCustomAnchor;    
        }

        private void OnEnable()
        {
            _mainCam = UnityEngine.Camera.main;
            if (!uiDocument) uiDocument = GetComponent<UIDocument>();

            var root = uiDocument.rootVisualElement;
            
            _container = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = 
                { 
                    position = Position.Absolute,
                    top = 0, bottom = 0, left = 0, right = 0,
                    marginLeft = 0, marginRight = 0, marginTop = 0, marginBottom = 0,
                    paddingLeft = 0, paddingRight = 0, paddingTop = 0, paddingBottom = 0
                }
            };
            root.Add(_container);

            if (EnergyGridManager.Instance)
            {
                EnergyGridManager.Instance.OnProducerRegistered += AddTracked;
                EnergyGridManager.Instance.OnProducerUnregistered += RemoveTracked;
                EnergyGridManager.Instance.OnConsumerRegistered += AddTracked;
                EnergyGridManager.Instance.OnConsumerUnregistered += RemoveTracked;
                ForceSync();
            }
        }

        private void OnDisable()
        {
            if (EnergyGridManager.Instance)
            {
                EnergyGridManager.Instance.OnProducerRegistered -= AddTracked;
                EnergyGridManager.Instance.OnProducerUnregistered -= RemoveTracked;
                EnergyGridManager.Instance.OnConsumerRegistered -= AddTracked;
                EnergyGridManager.Instance.OnConsumerUnregistered -= RemoveTracked;
            }
        }

        private void ForceSync()
        {
            foreach (var p in EnergyGridManager.Instance.AllProducers) AddTracked(p);
            foreach (var c in EnergyGridManager.Instance.AllConsumers) AddTracked(c);
        }

        private void AddTracked(Component item)
        {
            if (!_trackedComponents.Contains(item))
            {
                _trackedComponents.Add(item);
                Acquire(item); 
            }
        }

        private void RemoveTracked(Component item)
        {
            if (_trackedComponents.Contains(item))
            {
                _trackedComponents.Remove(item);
                Release(item);
            }
        }

        // --- BOUCLE PRINCIPALE ---

        private void LateUpdate()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _isVisible = !_isVisible;
                _container.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!_isVisible) return;

            // --- CORRECTION 3 : Sécurités Anti-Crash ---
            if (_mainCam == null) _mainCam = UnityEngine.Camera.main;
            if (_mainCam == null || _container.panel == null) return;

            Vector3 camPos = _mainCam.transform.position;
            Vector3 camForward = _mainCam.transform.forward;

            for (int i = _trackedComponents.Count - 1; i >= 0; i--)
            {
                var item = _trackedComponents[i];

                if (item == null)
                {
                    RemoveTracked(item);
                    continue;
                }

                if (!item.gameObject.activeInHierarchy)
                {
                    if (_activeLabels.TryGetValue(item, out var lbl)) 
                        lbl.Root.style.display = DisplayStyle.None;
                    continue;
                }

                if (_activeLabels.TryGetValue(item, out var element))
                {
                    Vector3 worldPos = element.CachedAnchor.position;
                    if (!element.IsCustomAnchor) worldPos.y += defaultVerticalOffset;

                    float dist = Vector3.Distance(camPos, worldPos);

                    if (dist > maxDistance)
                    {
                        element.Root.style.display = DisplayStyle.None;
                        continue;
                    }

                    if (item is EnergyProducer p) UpdateProducerVisuals(element, p);
                    else if (item is EnergyConsumer c) UpdateConsumerVisuals(element, c);

                    UpdateTransformAndVisuals(element, worldPos, dist, camPos, camForward);
                }
            }
        }

        private void UpdateTransformAndVisuals(LabelElement element, Vector3 worldPos, float dist, Vector3 camPos, Vector3 camFwd)
        {
            Vector3 direction = worldPos - camPos;
            if (Vector3.Dot(camFwd, direction) < 0)
            {
                element.Root.style.display = DisplayStyle.None;
                return;
            }

            if (element.Root.style.display == DisplayStyle.None) 
                element.Root.style.display = DisplayStyle.Flex;

            float factor = Mathf.InverseLerp(maxDistance, minDistance, dist); 
            
            element.Root.style.opacity = factor;

            float currentScale = Mathf.Lerp(scaleAtMaxDist, scaleAtMinDist, factor);
            element.Root.style.scale = new Scale(new Vector2(currentScale, currentScale));

            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                _container.panel, worldPos, _mainCam
            );
            
            // NOTE : Ici, contrairement à la barre de vie, on n'a pas besoin de faire 
            // panelPos.x -= width * 0.5f car on utilise le style.translate dans CreateVisual
            element.Root.transform.position = panelPos;
        }

        // --- CREATION VISUELLE ---

        private LabelElement CreateVisual()
        {
            var box = new VisualElement();
            box.style.position = Position.Absolute;
            
            // Taille auto pour s'adapter au texte
            box.style.width = StyleKeyword.Auto;
            box.style.height = StyleKeyword.Auto;
            
            box.style.paddingTop = 4; box.style.paddingBottom = 4;
            box.style.paddingLeft = 8; box.style.paddingRight = 8;

            // --- CORRECTION 2 : Centrage Parfait via CSS ---
            
            // 1. Le point de transformation (zoom) est au centre
            box.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
            
            // 2. On décale l'élément de -50% de sa propre taille (inconnue en pixels, connue en %)
            box.style.translate = new StyleTranslate(new Translate(
                new Length(-50, LengthUnit.Percent), 
                new Length(-50, LengthUnit.Percent), 
                0));

            box.style.backgroundColor = new Color(0, 0, 0, 0.65f);
            box.style.borderTopLeftRadius = 4; box.style.borderTopRightRadius = 4;
            box.style.borderBottomRightRadius = 4; box.style.borderBottomLeftRadius = 4;
            
            box.style.alignItems = Align.Center; 
            box.style.justifyContent = Justify.Center;

            var label = new Label();
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 11;
            label.style.color = Color.white;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;

            box.Add(label);
            return new LabelElement { Root = box, Text = label };
        }

        // --- POOLING & ANCHORS ---

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

            Transform customAnchor = key.transform.Find("UI_Anchor");
            if (customAnchor != null)
            {
                element.CachedAnchor = customAnchor;
                element.IsCustomAnchor = true;
            }
            else
            {
                element.CachedAnchor = key.transform;
                element.IsCustomAnchor = false;
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

        private static void UpdateProducerVisuals(LabelElement el, EnergyProducer p)
        {
            float current = p.CurrentLoad;
            float available = p.GetAvailable();
            float total = current + available;
            string typeName = p.isMobileGenerator ? "GEN" : "PLANT";
            el.Text.text = $"{typeName}\n{current:F0} / {total:F0}";
            el.Text.style.color = available > 0 ? new Color(0.3f, 1f, 1f) : new Color(1f, 0.3f, 0.3f);
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
}