using UnityEngine;
using System.Collections;

public class CameraFPSLimiter : MonoBehaviour
{
    public float targetFPS = 10f; // Set this to 10
    private UnityEngine.Camera cam;

    void Start()
    {
        cam = GetComponent<UnityEngine.Camera>();
        
        // IMPORTANT: Disable the camera so Unity stops rendering it automatically
        cam.enabled = false; 

        // Start our manual loop
        StartCoroutine(RenderLoop());
    }

    IEnumerator RenderLoop()
    {
        while (true)
        {
            // 1. Render the image manually
            cam.Render();

            // 2. Wait for the calculated time (1 second / 10 frames = 0.1s)
            yield return new WaitForSeconds(1f / targetFPS);
        }
    }
}