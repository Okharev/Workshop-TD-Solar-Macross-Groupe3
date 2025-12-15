using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public sealed class GrassRenderer : MonoBehaviour
{
    // --- Gestion statique des Occluders ---
    public static List<GrassOccluder> activeOccluders = new List<GrassOccluder>();
    public static void RegisterOccluder(GrassOccluder o) { if(!activeOccluders.Contains(o)) activeOccluders.Add(o); }
    public static void UnregisterOccluder(GrassOccluder o) { activeOccluders.Remove(o); }
    // ----------------------------------------------

    [Header("Settings")]
    public Mesh grassMesh;
    public Material grassMaterial;
    public ComputeShader cullingComputeShader;
    
    [Header("Terrain Blending")]
    public Texture2D terrainColorMap; 

    [Header("Spawning")]
    public int terrainLayerIndex = 0;
    [Range(0, 1)] public float minDensity = 0.5f;
    
    [Header("Configuration")]
    public int instanceCount = 100000;
    public float drawDistance = 200f;
    public Terrain terrain;

    [Header("Debug")]
    public bool renderAllGrass = false; // <--- COCHEZ CECI POUR TOUT VOIR

    private ComputeBuffer allInstancesBuffer;
    private ComputeBuffer visibleInstancesBuffer;
    private ComputeBuffer argsBuffer;
    
    // --- Buffer pour les occluders ---
    private ComputeBuffer occluderBuffer;
    private Vector4[] occluderData; 
    // ---------------------------------

    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    void Start()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        InitBuffers();
    }
    
    void InitBuffers()
    {
        if (!terrain) return;
        
        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        if (grassMaterial)
        {
            // Send size and pos to buffer
            grassMaterial.SetVector("_TerrainSize", data.size);
            grassMaterial.SetVector("_TerrainPos", terrainPos);
            
            // send texture info
            if (terrainColorMap)
                grassMaterial.SetTexture("_TerrainMap", terrainColorMap);
        }

        int alphamapWidth = data.alphamapWidth;
        int alphamapHeight = data.alphamapHeight;
        float[,,] splatmapData = data.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

        List<Vector4> validPositions = new List<Vector4>();
        int attempts = instanceCount * 2; 

        for (int i = 0; i < attempts; i++)
        {
            float normalizedX = Random.value;
            float normalizedZ = Random.value;

            int mapX = Mathf.RoundToInt(normalizedX * (alphamapWidth - 1));
            int mapZ = Mathf.RoundToInt(normalizedZ * (alphamapHeight - 1));

            float layerStrength = splatmapData[mapZ, mapX, terrainLayerIndex];

            if (layerStrength > minDensity)
            {
                float worldX = terrainPos.x + (normalizedX * data.size.x);
                float worldZ = terrainPos.z + (normalizedZ * data.size.z);

                float jitter = 0.5f; 
                worldX += Random.Range(-jitter, jitter);
                worldZ += Random.Range(-jitter, jitter);

                float heightY = terrain.SampleHeight(new Vector3(worldX, 0, worldZ));

                Vector4 finalPos = new Vector4(worldX, terrainPos.y + heightY, worldZ, Random.value);
                validPositions.Add(finalPos);
            }
            if (validPositions.Count >= instanceCount) break;
        }

        int finalCount = validPositions.Count;
        if (finalCount == 0) { Debug.LogWarning($"Aucune instance générée."); return; }
        
        allInstancesBuffer = new ComputeBuffer(finalCount, sizeof(float) * 4);
        allInstancesBuffer.SetData(validPositions.ToArray());

        visibleInstancesBuffer = new ComputeBuffer(finalCount, sizeof(float) * 4, ComputeBufferType.Append);

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        
        args[0] = (uint)grassMesh.GetIndexCount(0);
        args[1] = 0; 
        args[2] = (uint)grassMesh.GetIndexStart(0);
        args[3] = (uint)grassMesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);

        grassMaterial.SetBuffer("_VisibleInstances", visibleInstancesBuffer);
        
        this.instanceCount = finalCount; 
        
        occluderData = new Vector4[200]; 
        occluderBuffer = new ComputeBuffer(200, sizeof(float) * 4);
    }

    void Update()
    {
        if (allInstancesBuffer == null || instanceCount == 0) return;

        // --- Mise à jour des Occluders ---
        UpdateOccluders();
        // ---------------------------------

        visibleInstancesBuffer.SetCounterValue(0);

        cullingComputeShader.SetBuffer(0, "_AllInstances", allInstancesBuffer);
        cullingComputeShader.SetBuffer(0, "_VisibleInstances", visibleInstancesBuffer);
        
        // --- Envoi des occluders au Shader ---
        cullingComputeShader.SetBuffer(0, "_Occluders", occluderBuffer);
        cullingComputeShader.SetInt("_OccluderCount", activeOccluders.Count);
        // -------------------------------------

        // --- AJOUT : Envoi de l'option de debug ---
        // Si renderAllGrass est vrai, on envoie 1, sinon 0
        cullingComputeShader.SetInt("_RenderAll", renderAllGrass ? 1 : 0);
        // ------------------------------------------

        Matrix4x4 vp = UnityEngine.Camera.main.projectionMatrix * UnityEngine.Camera.main.worldToCameraMatrix;
        cullingComputeShader.SetMatrix("_VPMatrix", vp);
        cullingComputeShader.SetVector("_CamPos", UnityEngine.Camera.main.transform.position);
        cullingComputeShader.SetFloat("_MaxDistance", drawDistance);
        
        int groups = Mathf.CeilToInt(instanceCount / 64f);
        cullingComputeShader.Dispatch(0, groups, 1, 1);

        ComputeBuffer.CopyCount(visibleInstancesBuffer, argsBuffer, 4); 

        Graphics.DrawMeshInstancedIndirect(
            grassMesh, 
            0, 
            grassMaterial, 
            new Bounds(Vector3.zero, Vector3.one * 10000f), 
            argsBuffer
        );
    }

    // --- Fonction utilitaire ---
    void UpdateOccluders()
    {
        int count = activeOccluders.Count;
        if (count == 0) return;

        // Sécurité si on dépasse la taille du buffer
        if (count > occluderData.Length)
        {
            occluderBuffer.Release();
            occluderData = new Vector4[count + 50];
            occluderBuffer = new ComputeBuffer(occluderData.Length, sizeof(float) * 4);
        }

        // On remplit le tableau de données
        for (int i = 0; i < count; i++)
        {
            if(activeOccluders[i] != null)
            {
                Vector3 pos = activeOccluders[i].transform.position;
                float r = activeOccluders[i].radius;
                occluderData[i] = new Vector4(pos.x, pos.y, pos.z, r);
            }
        }
        
        // On envoie au GPU
        occluderBuffer.SetData(occluderData);
    }

    void OnDisable()
    {
        if (allInstancesBuffer != null) allInstancesBuffer.Release();
        if (visibleInstancesBuffer != null) visibleInstancesBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
        if (occluderBuffer != null) occluderBuffer.Release();
    }
}