using UnityEngine;
using Towers;

[RequireComponent(typeof(BaseTower))]
public class TowerRangeVisualizer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Le prefab contenant la sphère avec le shader")]
    public GameObject rangeIndicatorPrefab;
    
    [Tooltip("Si vrai, l'indicateur est visible uniquement quand la tour est sélectionnée")]
    public bool showOnlyOnSelect = true;

    private BaseTower _tower;
    private GameObject _visualInstance;
    private bool _isSelected;

    void Awake()
    {
        _tower = GetComponent<BaseTower>();
    }

    void Start()
    {
        // 1. Initialisation de l'instance visuelle (cachée par défaut)
        if (rangeIndicatorPrefab != null)
        {
            _visualInstance = Instantiate(rangeIndicatorPrefab, transform.position, Quaternion.identity, transform);
            _visualInstance.SetActive(false);
        }

        // 2. Abonnement réactif à la stat "Range"
        // CORRECTIF DU BUG : On s'assure que _visualInstance existe avant de modifier son scale
        if (_tower.range != null)
        {
            _tower.range.Observable.Subscribe(newRange => UpdateRangeScale(newRange)).AddTo(this);
        }
    }

    /// <summary>
    /// Appelé automatiquement quand la stat change via UniRx
    /// </summary>
    private void UpdateRangeScale(float rangeValue)
    {
        // PROTECTION ANTI-CRASH (NullReferenceException)
        if (_visualInstance == null) return;

        // La sphère Unity fait 1 unit de diamètre. Scale = Rayon * 2.
        float diameter = rangeValue * 2.0f;
        _visualInstance.transform.localScale = new Vector3(diameter, diameter, diameter);
    }

    // Ces méthodes doivent être appelées par ton système de sélection
    // Ou tu peux les appeler depuis BaseTower.OnSelect / OnDeselect
    public void OnSelect()
    {
        _isSelected = true;
        UpdateVisibility();
    }

    public void OnDeselect()
    {
        _isSelected = false;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (_visualInstance == null) return;

        // Affiche si sélectionné OU si on force l'affichage
        bool shouldShow = _isSelected || !showOnlyOnSelect;
        _visualInstance.SetActive(shouldShow);
        
        // Force une mise à jour du scale au moment de l'affichage pour être sûr
        if (shouldShow) UpdateRangeScale(_tower.range.Value);
    }
}