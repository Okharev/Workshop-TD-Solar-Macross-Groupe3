using System.Collections.Generic;
using UnityEngine;

public class SimpleTreeGenerator : MonoBehaviour
{
    [Header("Materials")] public Material trunkMaterial;

    public Material leafMaterial;

    [Header("Settings")] public int randomSeed;

    [Header("Trunk Settings")] public float trunkHeight = 4f;

    public float trunkWidthBase = 0.8f;
    public float trunkWidthTop = 0.4f;

    [Header("Foliage Settings")] public int leafCount = 1000;

    public float crownRadius = 3.5f;
    public float leafSize = 1.0f;
    public bool useSphericalNormals = true;

    [Header("AO Settings")] [Range(0, 1)] public float coreDarkness = 0.5f;

    [ContextMenu("Generate Tree")]
    public void GenerateTree()
    {
        while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
        Random.InitState(randomSeed);

        // Create Container
        var treeObj = new GameObject("GenshinTree");
        treeObj.transform.SetParent(transform);
        treeObj.transform.localPosition = Vector3.zero;

        // Add Instance Component for Runtime
        treeObj.AddComponent<GenshinTreeInstance>();

        var mf = treeObj.AddComponent<MeshFilter>();
        var mr = treeObj.AddComponent<MeshRenderer>();

        // Assign Submesh Materials
        mr.materials = new[]
        {
            trunkMaterial != null ? trunkMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit")),
            leafMaterial != null ? leafMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"))
        };

        // Data Lists
        var allVerts = new List<Vector3>();
        var allUVs = new List<Vector2>();
        var allNormals = new List<Vector3>();
        var allColors = new List<Color>();
        var trunkTris = new List<int>();
        var leafTris = new List<int>();

        // 1. Generate Trunk
        GenerateTrunkGeometry(allVerts, allUVs, allNormals, allColors, trunkTris);

        // 2. Generate Foliage
        GenerateFoliageGeometry(allVerts, allUVs, allNormals, allColors, leafTris);

        // 3. Build Mesh
        var mesh = new Mesh();
        mesh.SetVertices(allVerts);
        mesh.SetUVs(0, allUVs);
        mesh.SetNormals(allNormals);
        mesh.SetColors(allColors);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(trunkTris, 0);
        mesh.SetTriangles(leafTris, 1);

        // Finalize
        mf.mesh = mesh;
    }

    private void GenerateTrunkGeometry(List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals,
        List<Color> colors, List<int> tris)
    {
        var segments = 8;
        var heightSegments = 4;
        var vertOffset = verts.Count;

        for (var ySeg = 0; ySeg <= heightSegments; ySeg++)
        {
            var t = (float)ySeg / heightSegments;
            var currentHeight = t * trunkHeight;
            var currentWidth = Mathf.Lerp(trunkWidthBase, trunkWidthTop, t);

            // COLOR PACKING (Trunk):
            // R = Height (Wind)
            // G = 0 (No Flutter)
            // B = 1 (No AO)
            // A = 0 (Rigid Trunk)
            var segmentColor = new Color(t, 0f, 1f, 0f);

            for (var i = 0; i <= segments; i++)
            {
                var angle = (float)i / segments * Mathf.PI * 2;
                var x = Mathf.Cos(angle) * currentWidth;
                var z = Mathf.Sin(angle) * currentWidth;
                verts.Add(new Vector3(x, currentHeight, z));
                uvs.Add(new Vector2((float)i / segments, t));
                normals.Add(new Vector3(x, 0, z).normalized);
                colors.Add(segmentColor);
            }
        }

        // Triangles logic
        for (var ySeg = 0; ySeg < heightSegments; ySeg++)
        for (var i = 0; i < segments; i++)
        {
            var stride = segments + 1;
            var b = vertOffset + ySeg * stride + i;
            var t = vertOffset + (ySeg + 1) * stride + i;
            tris.Add(b);
            tris.Add(t);
            tris.Add(b + 1);
            tris.Add(b + 1);
            tris.Add(t);
            tris.Add(t + 1);
        }
    }

    private void GenerateFoliageGeometry(List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals,
        List<Color> colors, List<int> tris)
    {
        var crownCenter = new Vector3(0, trunkHeight * 0.8f, 0);
        var vertOffset = verts.Count;

        for (var i = 0; i < leafCount; i++)
        {
            var randomPos = Random.insideUnitSphere * crownRadius;
            if (randomPos.y < 0) randomPos.y *= 0.5f;
            var center = crownCenter + randomPos;

            // COLOR PACKING (Leaves):
            // R = Height (Synced with Trunk)
            // G = 1 (Flutter On)
            // B = AO (Darkness)
            // A = 0 (Attached to Trunk - normal bend) OR 1 (Attached to Branch - double bend)
            var height = Mathf.Clamp01(center.y / trunkHeight);
            var dist = Mathf.Clamp01(Vector3.Distance(center, crownCenter) / crownRadius);
            var ao = Mathf.Lerp(coreDarkness, 1.0f, dist);

            var col = new Color(height, 1f, ao, 0f); // Set Alpha to 1 if you add branches logic!

            // Quad Generation
            var s = verts.Count;
            var rot = Random.rotation;
            var h = leafSize * 0.5f;
            verts.Add(center + rot * new Vector3(-h, -h, 0));
            verts.Add(center + rot * new Vector3(h, -h, 0));
            verts.Add(center + rot * new Vector3(-h, h, 0));
            verts.Add(center + rot * new Vector3(h, h, 0));
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));

            var n = useSphericalNormals ? (center - crownCenter).normalized : rot * Vector3.forward;
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            colors.Add(col);
            colors.Add(col);
            colors.Add(col);
            colors.Add(col);
            tris.Add(s);
            tris.Add(s + 2);
            tris.Add(s + 1);
            tris.Add(s + 2);
            tris.Add(s + 3);
            tris.Add(s + 1);
        }
    }
}