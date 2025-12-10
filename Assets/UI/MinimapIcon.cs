using UnityEngine;

namespace UI
{
    public sealed class MinimapIcon : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Sprite _iconSprite;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private float _size = 5f;
        
        private const string MINIMAP_LAYER = "Minimap"; 

        private GameObject _iconObj;
        private Transform _iconTransform;

        private void Start()
        {
            CreateIcon();
        }

        private void CreateIcon()
        {
            _iconObj = new GameObject("MinimapIcon_Visual");
            
            int layerIndex = LayerMask.NameToLayer(MINIMAP_LAYER);
            if (layerIndex == -1)
            {
                Debug.LogWarning($"Le Layer '{MINIMAP_LAYER}' n'existe pas ! Créez-le dans les Project Settings.");
                // Fallback layer
                layerIndex = gameObject.layer; 
            }
            _iconObj.layer = layerIndex;

            var sr = _iconObj.AddComponent<SpriteRenderer>();
            

            if (_iconSprite != null) sr.sprite = _iconSprite; 
            
            sr.color = _color;

            _iconTransform = _iconObj.transform;
            _iconTransform.SetParent(transform);
            
            _iconTransform.localPosition = new Vector3(0, 10f, 0); 
            _iconTransform.localScale = new Vector3(_size, _size, 1f);
            
            _iconTransform.localRotation = Quaternion.Euler(90f, 0, 0);
        }

        private void LateUpdate()
        {
            if (_iconTransform != null)
            {
                _iconTransform.rotation = Quaternion.Euler(90f, 0, 0);
            }
        }

        private void OnDestroy()
        {
            if (_iconObj) Destroy(_iconObj);
        }
    }
}