using UnityEngine;

public class MinimapBaker : MonoBehaviour
{
    [Header("Capture Settings")]
    public LayerMask renderLayers; // Set to "Terrain", "Roads", etc.
    public int resolution = 2048;  // High res for crisp details
    public float worldSize = 500f; // The width/height of your map area
    
    [Header("Output Settings")]
    public string minimapLayerName = "Minimap"; // The layer for the generated plane
    public float yOffset = -100f; // Put the plane below the map so the player doesn't see it

    void Start()
    {
        BakeMap();
    }

[ContextMenu("Bake Map Now")]
    public void BakeMap()
    {
        // --- 1. PREPARE LIGHTING (No Shadows, Full Brightness) ---
        // Save original settings to restore later
        Color originalAmbient = RenderSettings.ambientLight;
        ShadowQuality originalShadows = QualitySettings.shadows;

        // Force flat, bright lighting for the photo
        RenderSettings.ambientLight = Color.white; 
        QualitySettings.shadows = ShadowQuality.Disable; 

        // --- 2. SETUP CAMERA ---
        GameObject camObj = new GameObject("TempBakerCam");
        UnityEngine.Camera cam = camObj.AddComponent<UnityEngine.Camera>();
        
        cam.orthographic = true;
        cam.orthographicSize = worldSize / 2f;
        
        // Position HIGH up (Y=2000) so we are above mountains
        cam.transform.position = new Vector3(transform.position.x, 2000f, transform.position.z);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        
        // Render deeply (3000 units) to catch the bottom of valleys
        cam.farClipPlane = 3000f; 
        
        cam.cullingMask = renderLayers;
        cam.clearFlags = CameraClearFlags.Color;
        
        // USE A NEUTRAL BACKGROUND (Not black)
        // If there is a hole in terrain, it will look like "ground" not a black void.
        cam.backgroundColor = new Color(0.3f, 0.4f, 0.2f); // Dark Greenish generic ground

        // --- 3. RENDER ---
        RenderTexture rt = new RenderTexture(resolution, resolution, 24);
        rt.Create();
        cam.targetTexture = rt;
        cam.Render();

        // --- 4. SAVE TO TEXTURE ---
        RenderTexture.active = rt;
        Texture2D mapTexture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        mapTexture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        mapTexture.Apply();

        // --- 5. CLEANUP & RESTORE ---
        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        Destroy(camObj);

        // Restore original lighting so your actual game doesn't look weird
        RenderSettings.ambientLight = originalAmbient;
        QualitySettings.shadows = originalShadows;

        // --- 6. CREATE PLANE ---
        CreateMapPlane(mapTexture);
    }

    void CreateMapPlane(Texture2D tex)
    {
        // Check if we already baked one and delete it
        Transform old = transform.Find("BakedMapPlane");
        if (old != null) Destroy(old.gameObject);

        // Create a Quad
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plane.name = "BakedMapPlane";
        plane.transform.parent = transform;
        
        // Position it exactly where the photo was taken (but lower)
        plane.transform.localPosition = new Vector3(0, yOffset, 0);
        
        // Rotate flat
        plane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        
        // Scale to match world size
        plane.transform.localScale = new Vector3(worldSize, worldSize, 1f);

        // Apply Texture (using a cheap Unlit shader)
        MeshRenderer mr = plane.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = tex;
        mr.material = mat;

        // Set Layer so ONLY Minimap Camera sees it
        int layerID = LayerMask.NameToLayer(minimapLayerName);
        plane.layer = layerID;
    }
}