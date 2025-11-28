using System.Collections.Generic;
using Economy;
using UnityEngine;

namespace Placement
{
    public class DistrictGenerator : MonoBehaviour
    {
        [Header("Map Settings")] public float mapRadius = 50f;

        public float height = 10f;
        public int capacityPerDistrict = 100;
        public int smoothness = 30;

        [Header("Alignment")] [Range(0, 360)] public float rotationOffset = 210f;

        [Header("Configuration")] public LayerMask districtLayer;

        private void Start()
        {
            GenerateDistricts();
        }

        private void GenerateDistricts()
        {
            var angleStep = 360f / 3f;

            for (var i = 0; i < 3; i++)
            {
                var startAngle = i * angleStep + rotationOffset;

                var name = $"District_{i + 1}";
                CreateDistrictSector(name, startAngle, angleStep);
            }
        }

        private void CreateDistrictSector(string name, float startAngle, float totalAngle)
        {
            var sectorObj = new GameObject(name);
            sectorObj.transform.parent = transform;
            
            sectorObj.transform.localPosition = Vector3.zero; 

            var layerIndex = 0;
            var layerValue = districtLayer.value;

            while (layerValue > 0)
            {
                layerValue >>= 1;
                layerIndex++;
            }

            sectorObj.layer = layerIndex - 1;

            var mesh = GenerateSectorMesh(startAngle, totalAngle);

            var col = sectorObj.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = true;
            col.isTrigger = true;

            var source = sectorObj.AddComponent<EnergyProducer>();
            source.maxCapacity = capacityPerDistrict;
            source.isMobileGenerator = false;
            
            source.broadcastRadius = mapRadius; 
        }

        private Mesh GenerateSectorMesh(float startAngle, float totalAngle)
        {
            var mesh = new Mesh();
            var segments = smoothness;
            var vertices = new Vector3[2 + (segments + 1) * 2];

            vertices[0] = Vector3.zero; 
            vertices[1] = new Vector3(0, height, 0); 

            var angleIncrement = totalAngle / segments;

            for (var i = 0; i <= segments; i++)
            {
                var currentAngle = startAngle + angleIncrement * i;
                var rad = Mathf.Deg2Rad * currentAngle;

                var x = Mathf.Cos(rad) * mapRadius;
                var z = Mathf.Sin(rad) * mapRadius;

                var vertIndex = 2 + i * 2;

                vertices[vertIndex] = new Vector3(x, 0, z); 
                vertices[vertIndex + 1] = new Vector3(x, height, z); 
            }

            mesh.vertices = vertices;

            var tris = new List<int>();

            for (var i = 0; i < segments; i++)
            {
                var baseIdx = 2 + i * 2;
                var nextIdx = 2 + (i + 1) * 2;

                tris.Add(0); tris.Add(nextIdx); tris.Add(baseIdx);
                tris.Add(1); tris.Add(baseIdx + 1); tris.Add(nextIdx + 1);

                tris.Add(baseIdx); tris.Add(nextIdx); tris.Add(nextIdx + 1);
                tris.Add(baseIdx); tris.Add(nextIdx + 1); tris.Add(baseIdx + 1);
            }

            var lastIdx = 2 + segments * 2;

            tris.Add(0); tris.Add(2); tris.Add(3);
            tris.Add(0); tris.Add(3); tris.Add(1);

            tris.Add(0); tris.Add(lastIdx + 1); tris.Add(lastIdx);
            tris.Add(0); tris.Add(1); tris.Add(lastIdx + 1);

            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();

            return mesh;
        }
    }
}