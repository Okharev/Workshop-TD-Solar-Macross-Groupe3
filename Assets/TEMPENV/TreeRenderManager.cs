using UnityEngine;
using System.Collections.Generic;

public class TreeRenderManager : MonoBehaviour
{
    [System.Serializable]
    public struct TreeData {
        public Vector3 position;
        public Quaternion rotation;
        public float scale;
        public Vector4 colorTint;
    }

    [Header("Settings")]
    public ComputeShader cullShader; // Assurez-vous que ceci est assigné !
    public float maxDistance = 500f;

    private class TreeBatch {
        public Mesh mesh;
        public Material[] materials;
        public int count;
        
        // Buffers de données (partagés pour tout l'arbre)
        public ComputeBuffer allDataBuffer;
        public ComputeBuffer visibleIndicesBuffer;

        // Buffers d'arguments (UN PAR SUBMESH pour éviter les conflits)
        public List<ComputeBuffer> argsBuffers = new List<ComputeBuffer>();
        public List<uint[]> argsData = new List<uint[]>();

        public void Cleanup() {
            foreach (var buf in argsBuffers) buf?.Release();
            if (allDataBuffer != null) allDataBuffer.Release();
            if (visibleIndicesBuffer != null) visibleIndicesBuffer.Release();
        }
    }

    private List<TreeBatch> _batches = new List<TreeBatch>();
    private Bounds _sceneBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

    void Start()
    {
        var instances = FindObjectsOfType<GenshinTreeInstance>();
        if (instances.Length == 0) return;

        Dictionary<string, List<GenshinTreeInstance>> grouped = new Dictionary<string, List<GenshinTreeInstance>>();

        foreach (var inst in instances)
        {
            MeshFilter mf = inst.GetComponent<MeshFilter>();
            MeshRenderer mr = inst.GetComponent<MeshRenderer>();
            if (!mf || !mr) continue;

            string key = mf.sharedMesh.name;
            if (!grouped.ContainsKey(key)) grouped.Add(key, new List<GenshinTreeInstance>());
            grouped[key].Add(inst);

            mr.enabled = false; 
        }

        foreach (var group in grouped) CreateBatch(group.Value);
    }

    void CreateBatch(List<GenshinTreeInstance> instances)
    {
        TreeBatch batch = new TreeBatch();
        batch.count = instances.Count;
        MeshRenderer mr = instances[0].GetComponent<MeshRenderer>();
        batch.mesh = instances[0].GetComponent<MeshFilter>().sharedMesh;
        batch.materials = mr.sharedMaterials;

        TreeData[] data = new TreeData[batch.count];
        for (int i = 0; i < batch.count; i++) {
            Transform t = instances[i].transform;
            data[i] = new TreeData() {
                position = t.position, 
                rotation = t.rotation, 
                scale = t.lossyScale.x, 
                colorTint = instances[i].tint
            };
        }

        batch.allDataBuffer = new ComputeBuffer(batch.count, 48);
        batch.allDataBuffer.SetData(data);
        batch.visibleIndicesBuffer = new ComputeBuffer(batch.count, 4, ComputeBufferType.Append);
        
        // --- CORRECTION MAJEURE ICI ---
        // On crée un buffer d'arguments distinct pour chaque submesh
        for (int i = 0; i < batch.mesh.subMeshCount; i++)
        {
            // Buffer indirect standard : 5 uints
            ComputeBuffer argsBuf = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            
            // Initialisation des données : [IndexCount, InstanceCount, StartIndex, BaseVertex, StartInstance]
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            
            // On pré-remplit les données statiques (Topology) ici, une seule fois !
            args[0] = (uint)batch.mesh.GetIndexCount(i);
            args[1] = 0; // Instance count (sera rempli par le GPU)
            args[2] = (uint)batch.mesh.GetIndexStart(i);
            args[3] = (uint)batch.mesh.GetBaseVertex(i);
            args[4] = 0;

            argsBuf.SetData(args); // Envoi initial
            
            batch.argsBuffers.Add(argsBuf);
            batch.argsData.Add(args);
        }

        _batches.Add(batch);
    }

    void Update()
    {
        UnityEngine.Camera cam = UnityEngine.Camera.main;
        if (!cam || cullShader == null) return; // Sécurité

        Matrix4x4 vp = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix;
        cullShader.SetMatrix("_VPMatrix", vp);
        cullShader.SetVector("_CamPos", cam.transform.position);
        cullShader.SetFloat("_MaxDistance", maxDistance);

        MaterialPropertyBlock props = new MaterialPropertyBlock();

        foreach (var batch in _batches)
        {
            // 1. CULLING (Une seule fois par batch d'arbres)
            batch.visibleIndicesBuffer.SetCounterValue(0);
            cullShader.SetBuffer(0, "_AllInstances", batch.allDataBuffer);
            cullShader.SetBuffer(0, "_VisibleInstanceIndices", batch.visibleIndicesBuffer);
            cullShader.SetInt("_Count", batch.count); // Important pour le shader
            
            int threadGroups = Mathf.CeilToInt(batch.count / 64f);
            cullShader.Dispatch(0, threadGroups, 1, 1);

            // 2. RENDU (Boucle sur les submeshes)
            for (int i = 0; i < batch.mesh.subMeshCount; i++)
            {
                if (i >= batch.materials.Length) break;

                // On récupère le buffer spécifique à ce submesh
                ComputeBuffer argsBuffer = batch.argsBuffers[i];

                // Copie du nombre d'instances visibles (depuis le Counter buffer vers l'Args buffer à l'offset 4)
                ComputeBuffer.CopyCount(batch.visibleIndicesBuffer, argsBuffer, 4);

                props.Clear();
                props.SetBuffer("_TreeDataBuffer", batch.allDataBuffer);
                props.SetBuffer("_VisibleInstanceIndices", batch.visibleIndicesBuffer);

                Graphics.DrawMeshInstancedIndirect(
                    batch.mesh, 
                    i, 
                    batch.materials[i], 
                    _sceneBounds, 
                    argsBuffer, 
                    0, 
                    props
                );
            }
        }
    }

    void OnDisable() { foreach (var b in _batches) b.Cleanup(); }
}