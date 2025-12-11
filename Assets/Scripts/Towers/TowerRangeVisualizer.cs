using UnityEngine;
using Towers;

    [RequireComponent(typeof(BaseTower))]
    public class TowerRangeVisualizer : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GameObject rangeSpherePrefab;
        [SerializeField] private float yOffset = 0.0f;
        [SerializeField] private bool showOnlyOnSelection = true;

        private BaseTower _tower;
        private GameObject _currentRangeIndicator;
        private Transform _indicatorTrans;

        private void Awake()
        {
            _tower = GetComponent<BaseTower>();
            
            _tower.range.Observable.Subscribe(e => UpdateRangeScale()).AddTo(this);
        }

        private void Start()
        {
            if (rangeSpherePrefab)
            {
                _currentRangeIndicator = Instantiate(rangeSpherePrefab, transform);
                _indicatorTrans = _currentRangeIndicator.transform;
                _indicatorTrans.localPosition = new Vector3(0, yOffset, 0);
                
                UpdateRangeScale(); 

                if (showOnlyOnSelection) 
                    _currentRangeIndicator.SetActive(false);
            }
        }

        private void UpdateRangeScale()
        {
            float radius = 5f;

            if (_tower.range is { Value: > 0 })
            {
                radius = _tower.range.Value;
            }
            else if (_tower.baseRange > 0)
            {
                radius = _tower.baseRange;
            }

            float scale = radius * 2.0f;
            
            // Application
            if (!Mathf.Approximately(_indicatorTrans.localScale.x, scale))
            {
                _indicatorTrans.localScale = new Vector3(scale, scale, scale);
            }
        }

        public void ToggleRangeVisibility(bool isVisible)
        {
            if (_currentRangeIndicator)
            {
                _currentRangeIndicator.SetActive(isVisible);
                if (isVisible) UpdateRangeScale();
            }
        }
    }
