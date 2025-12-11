using UnityEngine;

[DefaultExecutionOrder(-100)] // Très tôt pour initialiser les shaders avant le rendu
public class GlobalWindManager : MonoBehaviour
{
    public static GlobalWindManager Instance;

    [Header("Paramètres du Vent")]
    public Texture2D windTexture;
    public Vector2 windDirection = new Vector2(1f, 0.5f); // Direction unifiée
    public float windSpeed = 0.5f;
    public float windScale = 0.05f; // Échelle de la texture (Tiling)

    [Header("Debug")]
    public bool updateShaderGlobals = true;

    // IDs pour optimiser les appels Shader
    private static readonly int WindMapID = Shader.PropertyToID("_WindMap");
    private static readonly int WindDirID = Shader.PropertyToID("_WindDirection");
    private static readonly int WindSpeedID = Shader.PropertyToID("_WindSpeed");
    private static readonly int WindScaleID = Shader.PropertyToID("_WindScale");

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        // Validation simple
        if (windTexture == null) Debug.LogWarning("Attention : Pas de WindTexture assignée au GlobalWindManager !");
    }

    void Update()
    {
        if (!updateShaderGlobals) return;

        // Normalisation de la direction pour éviter que la vitesse change si on modifie le vecteur
        Vector2 dir = windDirection.normalized;

        Shader.SetGlobalTexture(WindMapID, windTexture);
        Shader.SetGlobalVector(WindDirID, dir);
        Shader.SetGlobalFloat(WindSpeedID, windSpeed);
        Shader.SetGlobalFloat(WindScaleID, windScale);
    }

    /// <summary>
    /// Récupère la valeur du vent (0 à 1) à une position donnée dans le monde.
    /// Parfait pour le Gameplay (particules, drapeaux, sons).
    /// </summary>
    public float GetWindAtPosition(Vector3 worldPosition)
    {
        if (windTexture == null) return 0f;

        // 1. Répliquer exactement le math du Shader
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
}