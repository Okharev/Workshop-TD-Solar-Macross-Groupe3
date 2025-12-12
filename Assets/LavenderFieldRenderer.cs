using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways] // 1. Permet au script de tourner dans l'Éditeur !
public class LavenderFieldRenderer : MonoBehaviour
{
    [Header("Components")]
    public Mesh instanceMesh;
    public Material instanceMaterial;
    public ComputeShader cullingComputeShader;
    public Terrain terrain;

    [Header("Field Configuration")]
    public int rowCount = 20;
    public int plantsPerRow = 50;
    public float rowSpacing = 1.5f;
    public float plantSpacing = 0.3f;
    public int seed = 123; // 2. Pour garder la stabilité du hasard

    [Header("Realism")]
    [Range(0, 0.5f)] public float positionJitter = 0.15f;
    [Range(0, 1f)] public float widthJitter = 0.2f;

    [Header("Culling")]
    public float drawDistance = 200f;

    private ComputeBuffer allInstancesBuffer;
    private ComputeBuffer visibleInstancesBuffer;
    private ComputeBuffer argsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    private int instanceCount;
    
    // Pour éviter de spammer la régénération
    private bool needsUpdate = false;

    void OnEnable()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        InitBuffers();
    }

    // 3. Détecte les changements dans l'Inspecteur
    void OnValidate()
    {
        // On ne régénère pas immédiatement pour éviter de le faire 
        // à chaque chiffre tapé, on marque juste qu'une mise à jour est requise.
        needsUpdate = true;
    }

    void InitBuffers()
    {
        // Sécurité : On nettoie toujours avant de refaire
        ReleaseBuffers();

        if (instanceMesh == null || instanceMaterial == null || cullingComputeShader == null) return;
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) return; // Si toujours pas de terrain, on arrête

        // 4. Initialisation du hasard stable
        Random.InitState(seed);

        List<Vector4> positions = new List<Vector4>();
        Vector3 startPos = transform.position;

        float fieldWidth = rowCount * rowSpacing;
        float fieldLength = plantsPerRow * plantSpacing;
        Vector3 offsetOrigin = startPos - new Vector3(fieldWidth * 0.5f, 0, fieldLength * 0.5f);

        for (int r = 0; r < rowCount; r++)
        {
            float rowX = r * rowSpacing;

            for (int p = 0; p < plantsPerRow; p++)
            {
                float plantZ = p * plantSpacing;
                float x = offsetOrigin.x + rowX;
                float z = offsetOrigin.z + plantZ;

                x += Random.Range(-widthJitter, widthJitter);
                x += Random.Range(-positionJitter, positionJitter);
                z += Random.Range(-positionJitter, positionJitter);

                float y = terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
                positions.Add(new Vector4(x, y, z, Random.value));
            }
        }

        instanceCount = positions.Count;
        if (instanceCount == 0) return;

        allInstancesBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 4);
        allInstancesBuffer.SetData(positions.ToArray());

        visibleInstancesBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 4, ComputeBufferType.Append);
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = (uint)instanceMesh.GetIndexCount(0);
        args[1] = 0; 
        args[2] = (uint)instanceMesh.GetIndexStart(0);
        args[3] = (uint)instanceMesh.GetBaseVertex(0);
        argsBuffer.SetData(args);

        instanceMaterial.SetBuffer("_VisibleInstances", visibleInstancesBuffer);
        
        if (instanceMaterial.HasProperty("_TerrainSize"))
             instanceMaterial.SetVector("_TerrainSize", terrain.terrainData.size);
        if (instanceMaterial.HasProperty("_TerrainPos"))
             instanceMaterial.SetVector("_TerrainPos", terrain.transform.position);
             
        needsUpdate = false;
    }

    void Update()
    {
        // Si une modification a eu lieu dans l'inspecteur, on régénère
        if (needsUpdate) InitBuffers();

        if (allInstancesBuffer == null || !allInstancesBuffer.IsValid() || instanceCount == 0) return;

        visibleInstancesBuffer.SetCounterValue(0);

        cullingComputeShader.SetBuffer(0, "_AllInstances", allInstancesBuffer);
        cullingComputeShader.SetBuffer(0, "_VisibleInstances", visibleInstancesBuffer);

        // --- GESTION DES OCCLUDERS (TOUR) ---
        // On récupère la liste statique gérée par GrassRenderer (si elle existe)
        // Sinon on passe 0 pour éviter les erreurs shader
        if (GrassRenderer.activeOccluders != null)
        {
             // NOTE : Il faut s'assurer que GrassRenderer a bien créé le buffer 'occluderBuffer'.
             // Si tu veux que la lavande évite aussi les tours, il faudra partager ce buffer 
             // ou créer un système d'OcclusionManager global. 
             // Pour l'instant, on laisse vide pour éviter les erreurs.
             cullingComputeShader.SetInt("_OccluderCount", 0); 
        }
        // ------------------------------------

        Matrix4x4 vp;
        Vector3 camPos;

        // Gestion Caméra Editeur vs Jeu
        if (Application.isPlaying)
        {
            if (UnityEngine.Camera.main == null) return;
            vp = UnityEngine.Camera.main.projectionMatrix * UnityEngine.Camera.main.worldToCameraMatrix;
            camPos = UnityEngine.Camera.main.transform.position;
        }
        else
        {
            // Astuce pour que ça marche dans la Scene View
            if (UnityEditor.SceneView.lastActiveSceneView == null) return;
            UnityEngine.Camera sceneCam = UnityEditor.SceneView.lastActiveSceneView.camera;
            vp = sceneCam.projectionMatrix * sceneCam.worldToCameraMatrix;
            camPos = sceneCam.transform.position;
        }

        cullingComputeShader.SetMatrix("_VPMatrix", vp);
        cullingComputeShader.SetVector("_CamPos", camPos);
        cullingComputeShader.SetFloat("_MaxDistance", drawDistance);
        
        int groups = Mathf.CeilToInt(instanceCount / 64f);
        cullingComputeShader.Dispatch(0, groups, 1, 1);

        ComputeBuffer.CopyCount(visibleInstancesBuffer, argsBuffer, 4); 

        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 100000f); 
        Graphics.DrawMeshInstancedIndirect(instanceMesh, 0, instanceMaterial, bounds, argsBuffer);
    }

    void OnDisable()
    {
        ReleaseBuffers();
    }

    // 5. Nettoyage centralisé
    void ReleaseBuffers()
    {
        if (allInstancesBuffer != null) allInstancesBuffer.Release();
        if (visibleInstancesBuffer != null) visibleInstancesBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
        
        allInstancesBuffer = null;
        visibleInstancesBuffer = null;
        argsBuffer = null;
    }
}