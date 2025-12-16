using UnityEditor;
using UnityEngine;

public class FastGrassGenerator : EditorWindow
{
    [MenuItem("Tools/Generate OPAQUE RTS Grass")]
    public static void CreateMesh()
    {
        var mesh = new Mesh
        {
            name = "RTS_OpaqueCluster"
        };

        // Configuration
        var bladeCount = 3; // 3 blades per clump
        var height = 1.0f; // Height of grass
        var width = 0.2f; // Width of base of blade
        var spread = 0.2f; // How far apart blades are

        var vertices = new Vector3[bladeCount * 3];
        var uvs = new Vector2[bladeCount * 3];
        var triangles = new int[bladeCount * 3];

        for (var i = 0; i < bladeCount; i++)
        {
            // Calculate rotation for this blade (evenly distributed)
            var angle = i * (360f / bladeCount);
            var rot = Quaternion.Euler(0, angle, 0);

            // Random variance to make it look natural
            var offset = rot * new Vector3(0, 0, spread * 0.5f);

            // Define the 3 points of a grass blade (Triangle)
            // Relative to (0,0,0)
            var vLeft = new Vector3(-width * 0.5f, 0, 0);
            var vRight = new Vector3(width * 0.5f, 0, 0);
            var vTop = new Vector3(0, height, 0); // Tapers to a point!

            // Apply rotation and offset
            var vIndex = i * 3;
            vertices[vIndex + 0] = rot * vLeft + offset; // Bottom Left
            vertices[vIndex + 1] = rot * vRight + offset; // Bottom Right
            vertices[vIndex + 2] = rot * vTop + offset; // Top Tip

            // UVs (For gradient coloring)
            uvs[vIndex + 0] = new Vector2(0, 0);
            uvs[vIndex + 1] = new Vector2(1, 0);
            uvs[vIndex + 2] = new Vector2(0.5f, 1); // Tip is at UV top

            // Triangles
            triangles[vIndex + 0] = vIndex + 0;
            triangles[vIndex + 1] = vIndex + 2;
            triangles[vIndex + 2] = vIndex + 1;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;


        mesh.normals = null;
        mesh.tangents = null;

        mesh.RecalculateBounds();

        var path = "Assets/RTS_Opaque_Grass.asset";
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = mesh;
        Debug.Log("Optimized Opaque Mesh Generated: " + path);
    }
}