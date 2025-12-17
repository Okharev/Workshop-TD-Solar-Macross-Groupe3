using UnityEngine;

[DefaultExecutionOrder(-100)]
public class AudioController : MonoBehaviour
{
    public static AudioController Instance;

    [Header("Global Sources")]
    [SerializeField] private AudioSource musicSource; // For background music (2D)
    [SerializeField] private AudioSource uiSfxSource; // For UI clicks (2D)

    [Header("3D Audio Settings")]
    [SerializeField] private float minDistance = 5f;  // Distance where sound is at max volume
    [SerializeField] private float maxDistance = 25f; // Distance where sound becomes silent

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- MUSIC (Stays Constant/2D) ---

    public void PlayMusic(AudioClip musicClip)
    {
        if (musicSource.clip == musicClip) return;

        musicSource.clip = musicClip;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f; // 0.0 makes it fully 2D (Constant volume)
        musicSource.Play();
    }

    // --- UI SOUNDS (Stays Constant/2D) ---

    public void PlayUISound(AudioClip clip, float volume = 1.0f)
    {
        uiSfxSource.spatialBlend = 0f; // Ensure UI sounds are 2D
        uiSfxSource.PlayOneShot(clip, volume);
    }

    // --- 3D WORLD SOUNDS (Distance Based) ---

    /// <summary>
    /// Creates a temporary AudioSource at a specific position in the world.
    /// The sound volume will drop off based on distance to the Camera/Listener.
    /// </summary>
    public void PlaySound3D(AudioClip clip, Vector3 position, float volume = 1.0f)
    {
        if (clip == null) return;

        // 1. Create a temporary GameObject at the position
        GameObject tempAudioObject = new GameObject("TempAudio_SFX");
        tempAudioObject.transform.position = position;

        // 2. Add and configure the AudioSource
        AudioSource source = tempAudioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        
        // CRITICAL FOR 3D SOUND:
        source.spatialBlend = 1.0f; // 1.0 = Fully 3D sound
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Linear; // Linear fades out smoothly

        // 3. Play and Destroy
        source.Play();
        
        // Destroy the object after the clip finishes playing
        Destroy(tempAudioObject, clip.length); 
    }
}