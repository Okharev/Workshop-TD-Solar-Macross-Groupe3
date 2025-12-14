using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FlowerTool : MonoBehaviour
{
    [Header("Flower Shape")]
    [Tooltip("3 est idéal pour l'optimisation/volume. 2 fait trop plat.")]
    [Range(2, 6)] public int planeCount = 3; 
    
    [Header("Dimensions")]
    public float totalHeight = 1.0f;
    public float stemWidth = 0.05f;
    
    [Tooltip("Largeur du haut de la fleur (évasement pour visibilité RTS)")]
    public float topWidth = 0.5f;

    [Header("Structure")]
    [Tooltip("Position verticale (0-1) où la tige s'arrête et les pétales commencent. Doit correspondre au _PetalStart du Shader.")]
    [Range(0.1f, 0.9f)] public float petalStartRatio = 0.6f;

    [Header("Preview")]
    public bool autoUpdate = true;

    private MeshFilter mf;

    private void OnValidate()
    {
        if (autoUpdate) Generate();
    }

    [ContextMenu("Generate Mesh")]
    public void Generate()
    {
        if (mf == null) mf = GetComponent<MeshFilter>();
        mf.mesh = CreateFlowerMesh();
    }

    Mesh CreateFlowerMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "CustomFlowerMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // Rotation incrémentale pour créer l'étoile
        float angleStep = 180f / planeCount;

        // Hauteur réelle du début des pétales
        float midY = totalHeight * petalStartRatio;

        for (int i = 0; i < planeCount; i++)
        {
            Quaternion rot = Quaternion.Euler(0, i * angleStep, 0);

            // --- VERTICES ---
            // On crée 3 niveaux : Bas (0), Milieu (Transition), Haut (1)
            
            // Niveau 0 : Base de la tige (Au pivot 0,0,0)
            Vector3 vBaseL = rot * new Vector3(-stemWidth, 0, 0);
            Vector3 vBaseR = rot * new Vector3(stemWidth, 0, 0);

            // Niveau 1 : Fin de tige / Début pétales
            Vector3 vMidL = rot * new Vector3(-stemWidth, midY, 0);
            Vector3 vMidR = rot * new Vector3(stemWidth, midY, 0);

            // Niveau 2 : Sommet des pétales (Évasé)
            Vector3 vTopL = rot * new Vector3(-topWidth, totalHeight, 0);
            Vector3 vTopR = rot * new Vector3(topWidth, totalHeight, 0);

            int startIdx = verts.Count;

            // Ajout Vertices
            verts.Add(vBaseL); // 0
            verts.Add(vBaseR); // 1
            verts.Add(vMidL);  // 2
            verts.Add(vMidR);  // 3
            verts.Add(vTopL);  // 4
            verts.Add(vTopR);  // 5

            // --- UVS ---
            // UV.y est critique pour le Shader (Vent et Couleur)
            // UV.x peut être utilisé pour la texture, centré à 0.5
            
            uvs.Add(new Vector2(0, 0));                 // Base
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, petalStartRatio));   // Mid
            uvs.Add(new Vector2(1, petalStartRatio));
            uvs.Add(new Vector2(0, 1));                 // Top
            uvs.Add(new Vector2(1, 1));

            // --- TRIANGLES ---
            // Segment Bas (Tige)
            tris.Add(startIdx + 0); tris.Add(startIdx + 2); tris.Add(startIdx + 1);
            tris.Add(startIdx + 2); tris.Add(startIdx + 3); tris.Add(startIdx + 1);

            // Segment Haut (Pétales)
            tris.Add(startIdx + 2); tris.Add(startIdx + 4); tris.Add(startIdx + 3);
            tris.Add(startIdx + 4); tris.Add(startIdx + 5); tris.Add(startIdx + 3);
            
            // Note: Cull Off est actif dans le shader, donc pas besoin de doubler les faces
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

#if UNITY_EDITOR
    // --- EDITEUR POUR SAUVEGARDER LE MESH ---
    public void SaveMeshAsAsset()
    {
        Mesh meshToSave = mf.sharedMesh;
        if (meshToSave == null) return;

        string path = EditorUtility.SaveFilePanel("Save Flower Mesh", "Assets/", "RTS_Flower_Mesh", "asset");
        if (string.IsNullOrEmpty(path)) return;

        path = FileUtil.GetProjectRelativePath(path);

        AssetDatabase.CreateAsset(Instantiate(meshToSave), path);
        AssetDatabase.SaveAssets();
        Debug.Log($"Flower Mesh saved at: {path}");
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(FlowerTool))]
public class FlowerToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        FlowerTool script = (FlowerTool)target;

        GUILayout.Space(10);
        if (GUILayout.Button("Generate Now"))
        {
            script.Generate();
        }

        GUILayout.Space(5);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Save Mesh as Asset..."))
        {
            script.SaveMeshAsAsset();
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif