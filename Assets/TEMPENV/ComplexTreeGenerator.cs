using UnityEngine;
using System.Collections.Generic;

public class ComplexTreeGenerator : MonoBehaviour
{
    [Header("References")]
    public Material trunkMaterial;
    public Material leafMaterial;

    [Header("General Settings")]
    public int randomSeed = 0;
    public float totalTreeHeight = 15.0f; // Plus haut pour un grand arbre

    [Header("Trunk")]
    public float trunkHeight = 8f;
    public float trunkBaseRadius = 1.5f; // Base large pour le soutenir
    public float trunkTopRadius = 0.6f;
    public int trunkSegments = 10;
    public int trunkHeightSegments = 8;

    [Header("Branches - Distribution")]
    public int branchCount = 12; // Plus de branches
    public float minBranchHeight = 3.5f; // Commencer haut (pour que les unités passent dessous)
    public float branchLength = 6.0f; // Longues branches
    public float branchBaseRadius = 0.5f;
    public float branchTopRadius = 0.2f;

    [Header("Branches - Shaping")]
    [Tooltip("Angle min (bas de l'arbre) et max (haut de l'arbre)")]
    public float minBranchAngle = 80f; // Presque horizontal
    public float maxBranchAngle = 40f; // Plus vertical vers la cime
    
    [Tooltip("Courbure de la branche (Affaissement). 0 = au départ, 1 = au bout.")]
    public AnimationCurve branchCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, -0.5f), new Keyframe(1, 0.2f));
    public float curveStrength = 2.0f; // Intensité de la courbe

    [Header("Foliage Settings")]
    public int leavesPerBranch = 40; // Dense !
    public float leafClusterRadius = 2.5f; // Gros nuages
    [Range(0, 1)] public float coreDarkness = 0.3f; // Cœur très sombre pour le volume

    [Header("Leaf Variations")]
    public float minLeafSize = 1.0f;
    public float maxLeafSize = 1.8f;

    // -- Internal Data --
    private List<Vector3> verts;
    private List<Vector2> uvs;
    private List<Vector3> normals;
    private List<Color> colors;
    private List<int> trunkTris;
    private List<int> leafTris;
    private List<Vector3> leafAnchors;

    [ContextMenu("Generate Tree")]
    public void GenerateTree()
    {
        while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
        Random.InitState(randomSeed);
        InitializeLists();

        GameObject treeObj = new GameObject("GenshinBigTree");
        treeObj.transform.SetParent(transform);
        treeObj.transform.localPosition = Vector3.zero;

        MeshFilter mf = treeObj.AddComponent<MeshFilter>();
        MeshRenderer mr = treeObj.AddComponent<MeshRenderer>();
        
        mr.materials = new Material[] { 
            trunkMaterial != null ? trunkMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit")),
            leafMaterial != null ? leafMaterial : new Material(Shader.Find("Custom/GenshinFoliageWind_Responsive")) 
        };

        GenerateTrunk();
        GenerateBranches();
        GenerateFoliage();

        Mesh mesh = new Mesh();
        mesh.name = "GeneratedTreeMesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Supporte beaucoup de sommets

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        
        mesh.subMeshCount = 2;
        mesh.SetTriangles(trunkTris, 0);
        mesh.SetTriangles(leafTris, 1);
        
        mf.mesh = mesh;
    }

    void InitializeLists()
    {
        verts = new List<Vector3>();
        uvs = new List<Vector2>();
        normals = new List<Vector3>();
        colors = new List<Color>();
        trunkTris = new List<int>();
        leafTris = new List<int>();
        leafAnchors = new List<Vector3>();
    }

    void GenerateTrunk()
    {
        GenerateTube(Vector3.zero, Vector3.up * trunkHeight, trunkBaseRadius, trunkTopRadius, 
                     trunkSegments, trunkHeightSegments, false);
    }

    void GenerateBranches()
    {
        for (int i = 0; i < branchCount; i++)
        {
            float t = (float)i / (branchCount - 1);
            float h = Mathf.Lerp(minBranchHeight, trunkHeight * 0.95f, t);
            
            // Randomisation de la hauteur pour éviter l'effet "échelle"
            h += Random.Range(-0.3f, 0.3f); 

            Vector3 startPos = Vector3.up * h;

            // Rotation autour du tronc (Golden Ratio pour une belle distribution)
            float rotY = (i * 137.5f) % 360f; 
            
            // Variation d'Angle : Plus bas = Plus horizontal (90), Plus haut = Plus vertical (30)
            float angle = Mathf.Lerp(minBranchAngle, maxBranchAngle, t);
            
            Quaternion rot = Quaternion.Euler(angle, rotY, 0); 
            Vector3 dir = rot * Vector3.up; 
            
            // Calcul de la fin théorique (sans courbe) pour l'axe
            Vector3 endPos = startPos + (dir * branchLength);

            GenerateTube(startPos, endPos, branchBaseRadius, branchTopRadius, 6, 8, true); // Plus de segments pour la courbe

            // Points d'ancrage des feuilles (suit la courbe si possible, ici approximation linéaire corrigée plus tard)
            // Note: Pour faire simple, on garde les ancres sur l'axe, mais visuellement la branche sera courbée
            leafAnchors.Add(endPos + Vector3.down * (curveStrength * 0.5f)); // On baisse un peu l'ancre du bout
            leafAnchors.Add(Vector3.Lerp(startPos, endPos, 0.7f));
        }
    }

    void GenerateTube(Vector3 start, Vector3 end, float startRad, float endRad, int radialSegs, int heightSegs, bool isBranch)
    {
        int vertOffset = verts.Count;
        Vector3 axis = (end - start);
        float length = axis.magnitude;
        axis.Normalize();
        Quaternion orientation = Quaternion.FromToRotation(Vector3.up, axis);

        for (int y = 0; y <= heightSegs; y++)
        {
            float t = (float)y / heightSegs;
            float currentRad = Mathf.Lerp(startRad, endRad, t);
            
            // Position linéaire de base
            Vector3 centerPos = Vector3.Lerp(start, end, t);

            // --- NOUVEAU : COURBURE (GRAVITY) ---
            if (isBranch)
            {
                // On lit la courbe d'animation
                float curveVal = branchCurve.Evaluate(t);
                // On applique le décalage vers le bas (Local Y qui devient World Y globalement si on fait simple)
                // Ici on applique simplement un offset global en Y pour simuler la gravité
                centerPos.y += curveVal * curveStrength;
            }

            // COLOR DATA
            float heightRatio = Mathf.Clamp01(centerPos.y / totalTreeHeight);
            float fragility = isBranch ? 1.0f : 0.0f;
            Color col = new Color(heightRatio, 0f, 1f, fragility);

            for (int x = 0; x <= radialSegs; x++)
            {
                float angle = (float)x / radialSegs * Mathf.PI * 2;
                Vector3 circlePos = new Vector3(Mathf.Cos(angle) * currentRad, 0, Mathf.Sin(angle) * currentRad);
                
                Vector3 finalPos = centerPos + (orientation * circlePos);
                
                // Recalcul simple de la normale (approximatif pour les branches courbées mais suffisant)
                Vector3 normal = (orientation * circlePos).normalized;

                verts.Add(finalPos);
                normals.Add(normal);
                uvs.Add(new Vector2((float)x / radialSegs, t));
                colors.Add(col);
            }
        }

        // Triangles generation (inchangé)
        for (int y = 0; y < heightSegs; y++)
        {
            for (int x = 0; x < radialSegs; x++)
            {
                int stride = radialSegs + 1;
                int b = vertOffset + y * stride + x;
                int t = vertOffset + (y + 1) * stride + x;
                trunkTris.Add(b); trunkTris.Add(t); trunkTris.Add(b + 1);
                trunkTris.Add(b + 1); trunkTris.Add(t); trunkTris.Add(t + 1);
            }
        }
    }

    // --- FOLIAGE (Même logique d'Atlas, juste ajustée pour la densité) ---
    void GenerateFoliage()
    {
        // Grosse couronne au sommet
        Vector3 mainCrownCenter = Vector3.up * trunkHeight;
        GenerateCluster(mainCrownCenter, leavesPerBranch * 2, leafClusterRadius * 1.5f, false);

        foreach (Vector3 anchor in leafAnchors)
        {
            GenerateCluster(anchor, leavesPerBranch, leafClusterRadius, true);
        }
    }

    void GenerateCluster(Vector3 center, int count, float radius, bool onBranch)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 rnd = Random.insideUnitSphere * radius;
            Vector3 pos = center + rnd;
            float distRatio = rnd.magnitude / radius;

            float heightRatio = Mathf.Clamp01(pos.y / totalTreeHeight);
            float ao = Mathf.Lerp(coreDarkness, 1.0f, distRatio);
            float fragility = onBranch ? 1.0f : 0.0f;
            Color col = new Color(heightRatio, 1.0f, ao, fragility);

            AddLeafQuad(pos, col, center, distRatio);
        }
    }

    void AddLeafQuad(Vector3 center, Color col, Vector3 clusterCenter, float distRatio)
    {
        int vIndex = verts.Count;
        float randomSize = Random.Range(minLeafSize, maxLeafSize);
        float s = (randomSize * 0.5f) * Mathf.Lerp(1.2f, 0.7f, distRatio); 

        float uOff = 0; float vOff = 0;
        // Logique Atlas (Voir réponse précédente pour détail)
        if (distRatio > 0.75f) { uOff = 0.5f; vOff = 0.5f; } // Sparse
        else if (distRatio > 0.4f) {
            if(Random.value > 0.3f) { uOff = 0.0f; vOff = 0.0f; } else { uOff = 0.5f; vOff = 0.0f; }
        } else {
            if (Random.value > 0.5f) { uOff = 0.0f; vOff = 0.5f; } else { uOff = 0.5f; vOff = 0.0f; }
        }

        Quaternion rot = Random.rotation;
        // On oriente légèrement les feuilles vers le haut pour un grand arbre, ça capte mieux la lumière
        // rot = Quaternion.Lerp(rot, Quaternion.LookRotation(Vector3.up), 0.3f); 

        verts.Add(center + rot * new Vector3(-s, -s, 0));
        verts.Add(center + rot * new Vector3(s, -s, 0));
        verts.Add(center + rot * new Vector3(-s, s, 0));
        verts.Add(center + rot * new Vector3(s, s, 0));

        uvs.Add(new Vector2(uOff, vOff));               
        uvs.Add(new Vector2(uOff + 0.5f, vOff));        
        uvs.Add(new Vector2(uOff, vOff + 0.5f));        
        uvs.Add(new Vector2(uOff + 0.5f, vOff + 0.5f)); 

        Vector3 n = (center - clusterCenter).normalized;
        normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
        colors.Add(col); colors.Add(col); colors.Add(col); colors.Add(col);
        leafTris.Add(vIndex); leafTris.Add(vIndex+2); leafTris.Add(vIndex+1);
        leafTris.Add(vIndex+2); leafTris.Add(vIndex+3); leafTris.Add(vIndex+1);
    }
}