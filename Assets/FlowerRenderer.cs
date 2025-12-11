using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public sealed class FlowerRenderer : MonoBehaviour
{
    [Header("Settings")]
    public Mesh grassMesh;
    public Material grassMaterial;
    public ComputeShader cullingComputeShader;
    
    [Header("Spawning")]
    public int terrainLayerIndex = 0; // L'index de la texture au sol (0 = première texture)
    [Range(0, 1)] public float minDensity = 0.5f; // Densité min pour spawner (utile pour séparer herbe/fleurs)
    
    [Header("Configuration")]
    public int instanceCount = 100000; // Nombre cible d'instances
    public float drawDistance = 200f;
    public Terrain terrain;

    // Internal Buffers
    private ComputeBuffer allInstancesBuffer;
    private ComputeBuffer visibleInstancesBuffer;
    private ComputeBuffer argsBuffer;
    
    // Arguments for DrawMeshInstancedIndirect
    // [IndexCount, InstanceCount, StartIndex, BaseVertex, StartInstance]
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    void Start()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        InitBuffers();
    }

    void InitBuffers()
    {
        // --- 1. SETUP & SAFETY ---
        if (terrain == null) return;
        
        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        // Récupération de la SplatMap (Données de peinture du terrain)
        int alphamapWidth = data.alphamapWidth;
        int alphamapHeight = data.alphamapHeight;
        float[,,] splatmapData = data.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

        // Liste temporaire pour stocker les positions valides
        List<Vector3> validPositions = new List<Vector3>();

        // --- 2. GENERATION LOOP ---
        // On essaie 2x plus de points que nécessaire pour combler les zones vides
        int attempts = instanceCount * 2; 

        for (int i = 0; i < attempts; i++)
        {
            // 1. Position normalisée aléatoire (0 à 1)
            float normalizedX = Random.value;
            float normalizedZ = Random.value;

            // 2. Vérification de la Splat Map
            int mapX = Mathf.RoundToInt(normalizedX * (alphamapWidth - 1));
            int mapZ = Mathf.RoundToInt(normalizedZ * (alphamapHeight - 1));

            // On vérifie la force de la texture à cet endroit précis
            float layerStrength = splatmapData[mapZ, mapX, terrainLayerIndex];

            // 3. Si valide (assez de texture verte ici ?)
            if (layerStrength > minDensity)
            {
                // Position Monde X/Z
                float worldX = terrainPos.x + (normalizedX * data.size.x);
                float worldZ = terrainPos.z + (normalizedZ * data.size.z);

                // Petit décalage aléatoire pour casser la grille
                float jitter = 0.5f; 
                worldX += Random.Range(-jitter, jitter);
                worldZ += Random.Range(-jitter, jitter);

                // Hauteur du terrain (Y) à cet endroit précis
                float heightY = terrain.SampleHeight(new Vector3(worldX, 0, worldZ));

                // Position Finale
                Vector3 finalPos = new Vector3(worldX, terrainPos.y + heightY, worldZ);

                validPositions.Add(finalPos);
            }

            // Si on a atteint notre objectif, on arrête
            if (validPositions.Count >= instanceCount) break;
        }

        int finalCount = validPositions.Count;
        if (finalCount == 0) { Debug.LogWarning($"Aucune instance générée pour {gameObject.name}. Vérifiez le Layer Index."); return; }

        // --- 3. CREATE BUFFERS ---
        
        // Buffer source (toutes les positions)
        allInstancesBuffer = new ComputeBuffer(finalCount, sizeof(float) * 3);
        allInstancesBuffer.SetData(validPositions.ToArray());

        // Buffer destination (seulement les visibles, Append)
        visibleInstancesBuffer = new ComputeBuffer(finalCount, sizeof(float) * 3, ComputeBufferType.Append);

        // Buffer d'arguments pour le GPU
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        
        args[0] = (uint)grassMesh.GetIndexCount(0);
        args[1] = 0; 
        args[2] = (uint)grassMesh.GetIndexStart(0);
        args[3] = (uint)grassMesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);

        // IMPORTANT : On assigne le buffer au MATÉRIEL une seule fois (le matériel est unique par renderer)
        grassMaterial.SetBuffer("_VisibleInstances", visibleInstancesBuffer);
        
        // NOTE : On NE SET PAS le Compute Shader ici. On le fait dans Update() 
        // pour gérer plusieurs objets utilisant le même shader.

        // On met à jour le compte réel pour le Dispatch
        this.instanceCount = finalCount; 
    }

    void Update()
    {
        if (allInstancesBuffer == null || instanceCount == 0) return;

        // 1. Reset du compteur Append
        visibleInstancesBuffer.SetCounterValue(0);

        // --- CORRECTIF MULTI-OBJETS ---
        // On dit au Compute Shader : "Pour CE calcul, utilise CES buffers"
        // Cela permet d'avoir de l'herbe ET des fleurs gérées par le même script.
        cullingComputeShader.SetBuffer(0, "_AllInstances", allInstancesBuffer);
        cullingComputeShader.SetBuffer(0, "_VisibleInstances", visibleInstancesBuffer);

        // 2. Envoi des données Caméra
        Matrix4x4 vp = UnityEngine.Camera.main.projectionMatrix * UnityEngine.Camera.main.worldToCameraMatrix;
        cullingComputeShader.SetMatrix("_VPMatrix", vp);
        cullingComputeShader.SetVector("_CamPos", UnityEngine.Camera.main.transform.position);
        cullingComputeShader.SetFloat("_MaxDistance", drawDistance);
        
        // 3. Dispatch (Exécution du culling)
        int groups = Mathf.CeilToInt(instanceCount / 64f);
        cullingComputeShader.Dispatch(0, groups, 1, 1);

        // 4. Copie du nombre d'instances visibles dans le buffer d'arguments
        ComputeBuffer.CopyCount(visibleInstancesBuffer, argsBuffer, 4); 

        // 5. Dessin final
        Graphics.DrawMeshInstancedIndirect(
            grassMesh, 
            0, 
            grassMaterial, 
            new Bounds(Vector3.zero, Vector3.one * 10000f), 
            argsBuffer
        );
    }

    void OnDisable()
    {
        // Nettoyage impératif de la mémoire GPU
        if (allInstancesBuffer != null) allInstancesBuffer.Release();
        if (visibleInstancesBuffer != null) visibleInstancesBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
    }
}