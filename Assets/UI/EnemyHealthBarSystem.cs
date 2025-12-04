using System.Collections.Generic;
using Enemy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    public class EnemyHealthBarSystem : MonoBehaviour
    {
        public static EnemyHealthBarSystem Instance { get; private set; }

        [Header("Setup")]
        public UIDocument uiDocument;

        [Header("Visual Settings")]
        public float barWidth = 32f;
        public float barHeight = 4f;
        
        [Tooltip("Ajouté à la hauteur SEULEMENT si aucun objet 'UI_Anchor' n'est trouvé.")]
        public float fallbackVerticalOffset = 2.0f; 
        
        public bool hideIfFullHealth = true;

        [Header("Optimization & Culling")]
        public float minDistance = 5f;
        public float maxDistance = 40f;

        // --- Structures de Données ---
        private class TrackedData
        {
            public MonoBehaviour Key;        
            public HealthComponent Health;   
            public HealthBarElement Visual;  
        }

        private class HealthBarElement
        {
            public VisualElement Root;
            public VisualElement Fill;
            public Transform Anchor; 
        }

        private readonly List<TrackedData> _trackedList = new List<TrackedData>();
        private readonly Dictionary<MonoBehaviour, int> _indexMap = new Dictionary<MonoBehaviour, int>();
        private readonly Queue<HealthBarElement> _pool = new Queue<HealthBarElement>();

        private VisualElement _container;
        private UnityEngine.Camera _mainCam;
        private bool _isVisible = true;

        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;
        }

        private void OnEnable()
        {
            _mainCam = UnityEngine.Camera.main;
            if (!uiDocument) uiDocument = GetComponent<UIDocument>();

            var root = uiDocument.rootVisualElement;

            // --- LE CORRECTIF EST ICI ---
            _container = new VisualElement
            {
                pickingMode = PickingMode.Ignore, 
                style = 
                { 
                    // 1. On sort du flux Flexbox standard
                    position = Position.Absolute, 
            
                    // 2. On colle aux 4 coins pour couvrir tout l'écran (0,0 à ScreenWidth, ScreenHeight)
                    top = 0,
                    bottom = 0,
                    left = 0,
                    right = 0,
            
                    // Sécurité : On s'assure qu'il n'y a pas de marge qui décale tout
                    marginRight = 0, marginLeft = 0, marginTop = 0, marginBottom = 0,
                    paddingRight = 0, paddingLeft = 0, paddingTop = 0, paddingBottom = 0
                }
            };
            root.Add(_container);
        }

        // --- API PUBLIQUE ---

        public void RegisterEnemy(MonoBehaviour key, HealthComponent health)
        {
            if (_indexMap.ContainsKey(key)) return;

            var visual = Acquire(key);
            var data = new TrackedData { Key = key, Health = health, Visual = visual };

            _trackedList.Add(data);
            _indexMap.Add(key, _trackedList.Count - 1);
        }

        public void UnregisterEnemy(MonoBehaviour key)
        {
            if (_indexMap.TryGetValue(key, out int index))
            {
                Release(_trackedList[index].Visual);

                int lastIndex = _trackedList.Count - 1;
                var lastData = _trackedList[lastIndex];

                _trackedList[index] = lastData;
                _indexMap[lastData.Key] = index;

                _trackedList.RemoveAt(lastIndex);
                _indexMap.Remove(key);
            }
        }

        // --- UPDATE LOOP ---

        private void LateUpdate()
        {
            if (!_isVisible) return;
            
            // SÉCURITÉS ANTI-CRASH
            if (_mainCam == null) _mainCam = UnityEngine.Camera.main;
            if (_mainCam == null || _container.panel == null) return;

            Vector3 camPos = _mainCam.transform.position;
            Vector3 camFwd = _mainCam.transform.forward;

            for (int i = 0; i < _trackedList.Count; i++)
            {
                var data = _trackedList[i];

                if (data.Key == null) continue;

                // 1. Logique de Santé
                var current = data.Health.CurrentHealth.Value;
                float max = data.Health.MaxHealth;
                float pct = Mathf.Clamp01(current / max);

                if (data.Health.CurrentHealth.Value < 0 || (hideIfFullHealth && pct >= 0.99f))
                {
                    data.Visual.Root.style.display = DisplayStyle.None;
                    continue;
                }

                data.Visual.Fill.style.width = Length.Percent(pct * 100f);
                

                data.Visual.Fill.style.backgroundColor = new Color(0.9f, 0.2f, 0.2f, 1f); 

                Vector3 worldPos = data.Visual.Anchor.position;
                
                if (data.Visual.Anchor == data.Key.transform) 
                    worldPos.y += fallbackVerticalOffset;

                float dist = Vector3.Distance(camPos, worldPos);

                if (dist > maxDistance || Vector3.Dot(camFwd, worldPos - camPos) < 0)
                {
                    data.Visual.Root.style.display = DisplayStyle.None;
                    continue;
                }

                if (data.Visual.Root.style.display == DisplayStyle.None)
                    data.Visual.Root.style.display = DisplayStyle.Flex;

                data.Visual.Root.style.opacity = Mathf.InverseLerp(maxDistance, minDistance, dist);

                Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                    _container.panel, worldPos, _mainCam
                );
                
                panelPos.x -= barWidth * 0.5f;
                panelPos.y -= barHeight * 0.5f;
                
                data.Visual.Root.transform.position = panelPos;
                data.Visual.Root.style.scale = new Scale(Vector2.one * Mathf.Lerp(1.5f, 0.5f, Mathf.InverseLerp(minDistance, maxDistance, dist)));
            }
        }

        // --- POOLING & CREATION ---

        private HealthBarElement Acquire(MonoBehaviour key)
        {
            HealthBarElement bar;
            if (_pool.Count > 0)
            {
                bar = _pool.Dequeue();
                bar.Root.style.display = DisplayStyle.Flex;
            }
            else
            {
                bar = CreateVisual();
                _container.Add(bar.Root);
            }

            var anchor = key.transform.Find("UI_Anchor");
            
            bar.Anchor = anchor ? anchor : key.transform;
            
            return bar;
        }

        private void Release(HealthBarElement bar)
        {
            bar.Root.style.display = DisplayStyle.None;
            _pool.Enqueue(bar);
        }

        private HealthBarElement CreateVisual()
        {
            var bg = new VisualElement();
            bg.pickingMode = PickingMode.Ignore;
            bg.style.position = Position.Absolute;
            bg.style.width = barWidth;
            bg.style.height = barHeight;

            // --- CENTRAGE DU PIVOT ---
            // Important pour que le Scale (zoom) se fasse depuis le milieu
            bg.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
    
            // Note : On a retiré le "style.translate" ici car on fait le calcul manuellement
            // dans le LateUpdate, c'est souvent plus précis pour éviter le flou.

            bg.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            bg.style.borderTopLeftRadius = 2; bg.style.borderTopRightRadius = 2;
            bg.style.borderBottomRightRadius = 2; bg.style.borderBottomLeftRadius = 2;

            var fill = new VisualElement();
            fill.pickingMode = PickingMode.Ignore;
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(100);
    
            // Rouge
            fill.style.backgroundColor = new Color(0.9f, 0.2f, 0.2f, 1f); 
            fill.style.borderTopLeftRadius = 2; fill.style.borderBottomLeftRadius = 2;
            fill.style.borderTopRightRadius = 2; fill.style.borderBottomRightRadius = 2;

            bg.Add(fill);
            return new HealthBarElement { Root = bg, Fill = fill };
        }
    }
}