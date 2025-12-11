using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GlobalWindManager : MonoBehaviour
{
    public static GlobalWindManager Instance;

    [Header("Paramètres Globaux du Vent")]
    public Texture2D windTexture;
    public Vector2 windDirection = new Vector2(1f, 0.5f);
    public float windSpeed = 0.5f;
    public float windScale = 0.05f;

    [Tooltip("Force générale du vent pour toute la scène.")]
    [Range(0f, 5f)]
    public float globalWindStrength = 1.0f;

    [Header("Debug")]
    public bool updateShaderGlobals = true;

    // IDs for shaders
    private static readonly int WindMapID = Shader.PropertyToID("_WindMap");
    private static readonly int WindDirID = Shader.PropertyToID("_WindDirection");
    private static readonly int WindSpeedID = Shader.PropertyToID("_WindSpeed");
    private static readonly int WindScaleID = Shader.PropertyToID("_WindScale");
    private static readonly int GlobalWindStrengthID = Shader.PropertyToID("_GlobalWindStrength");

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        if (windTexture == null) 
            Debug.LogWarning("⚠️ NO GlobalWindManager !");
        
        // Init at start to avoid lags
        UpdateWindGlobals();
    }

    void Update()
    {
        if (updateShaderGlobals)
        {
            UpdateWindGlobals();
        }
    }

    public float GetWindAtPosition(Vector3 worldPosition)
    {
        if (!windTexture) return 0f;

        // 1. Replicate the shaders maths
        Vector2 dir = windDirection.normalized;
        float timeOffset = Time.time * windSpeed;

        // UV = Position * Scale + (Direction * Temps)
        float u = (worldPosition.x * windScale) + (dir.x * timeOffset);
        float v = (worldPosition.z * windScale) + (dir.y * timeOffset);

        // 2. Gestion du Tiling (Repeat)
        // Texture2D.GetPixelBilinear ne boucle pas automatiquement, il faut utiliser Repeat
        u = Mathf.Repeat(u, 1.0f);
        v = Mathf.Repeat(v, 1.0f);

        // 3. Lecture (Nécessite que la texture soit en "Read/Write Enabled")
        return windTexture.GetPixelBilinear(u, v).r;
    }
    
    void UpdateWindGlobals()
    {
        Vector2 dir = windDirection.normalized;

        Shader.SetGlobalTexture(WindMapID, windTexture);
        Shader.SetGlobalVector(WindDirID, dir);
        Shader.SetGlobalFloat(WindSpeedID, windSpeed);
        Shader.SetGlobalFloat(WindScaleID, windScale);
        Shader.SetGlobalFloat(GlobalWindStrengthID, globalWindStrength);
    }
}