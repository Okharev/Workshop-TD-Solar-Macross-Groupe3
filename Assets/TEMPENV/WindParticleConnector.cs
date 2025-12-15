using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class WindParticleConnector : MonoBehaviour
{
    private ParticleSystem _ps;
    private ParticleSystem.VelocityOverLifetimeModule _velModule;

    [Header("Réglages")]
    public float particleSpeedMultiplier = 10f; 

    void Start()
    {
        _ps = GetComponent<ParticleSystem>();
        _velModule = _ps.velocityOverLifetime;

        // Force l'activation du module et l'espace Monde
        _velModule.enabled = true;
        _velModule.space = ParticleSystemSimulationSpace.World; 
    }

    void Update()
    {
        if (GlobalWindManager.Instance == null) return;

        Vector2 windDir2D = GlobalWindManager.Instance.windDirection.normalized;
        float globalStrength = GlobalWindManager.Instance.globalWindStrength;
        float windSpeed = GlobalWindManager.Instance.windSpeed;

        Vector3 windDir3D = new Vector3(windDir2D.x, 0f, windDir2D.y);

        float finalSpeed = windSpeed * globalStrength * particleSpeedMultiplier;
        
        // Applique la force sur X et Z (le Y reste à 0 pour un vent horizontal)
        _velModule.x = new ParticleSystem.MinMaxCurve(windDir3D.x * finalSpeed);
        _velModule.z = new ParticleSystem.MinMaxCurve(windDir3D.z * finalSpeed);
    }
}