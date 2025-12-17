using UnityEngine;

public class LevelMusicStarter : MonoBehaviour
{
    [Header("Music Settings")]
    public AudioClip levelMusic; // Drag the specific music for this level here

    void Start()
    {
        // When the level loads, this Start() runs immediately.
        // It tells the AudioController to switch to this level's music.
        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlayMusic(levelMusic);
        }
        else
        {
            Debug.LogWarning("AudioController is missing from the scene!");
        }
    }
}