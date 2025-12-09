using UI;
using UnityEngine;
using UnityEngine.UIElements;

public class HUDController : MonoBehaviour
{
    public UIDocument uiDocument;

    private RadialProgress _radialAvatar;
    private VisualElement _healthFill;
    private VisualElement _manaFill;

    void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // Query the custom Radial Bar
        _radialAvatar = root.Q<RadialProgress>("Radial-Avatar");

        // Query the Linear Fills
        _healthFill = root.Q<VisualElement>("Health-Fill");
        _manaFill = root.Q<VisualElement>("Mana-Fill");
    }

    // Call this from your Game Logic
    public void UpdateHUD(float healthPct, float manaPct, float xpPct)
    {
        // 1. Update Linear Bars (Width expands to the left due to CSS row-reverse)
        // We ensure we clamp between 0 and 100
        if (_healthFill != null) 
            _healthFill.style.width = Length.Percent(Mathf.Clamp(healthPct, 0, 100));
        
        if (_manaFill != null) 
            _manaFill.style.width = Length.Percent(Mathf.Clamp(manaPct, 0, 100));

        // 2. Update Radial Bar
        if (_radialAvatar != null)
            _radialAvatar.CurrentValue = xpPct;
    }
    
    // Testing logic
    private void Update()
    {
        // Simple Sine wave test
        float t = Time.time * 2f;
        UpdateHUD(
            50f + Mathf.Sin(t) * 50f,        // Health 0-100
            50f + Mathf.Cos(t) * 50f,        // Mana 0-100
            (Time.time * 10f) % 100f         // XP looping
        );
    }
}