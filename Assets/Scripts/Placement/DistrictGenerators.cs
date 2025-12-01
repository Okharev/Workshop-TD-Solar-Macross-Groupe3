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

        // TRACKING LIST
        private readonly List<GameObject> _generatedSectors = new();
        private bool _hasGenerated;

        private void Start()
        {
            if (!_hasGenerated) GenerateDistricts();
        }

        private void OnEnable()
        {
            // If we have already generated them, just turn them back on
            if (_hasGenerated)
                foreach (var sector in _generatedSectors)
                    if (sector)
                        sector.SetActive(true);
        }

        private void OnDisable()
        {
            // When this script (or object) is disabled, turn off the children
            foreach (var sector in _generatedSectors)
                if (sector)
                    sector.SetActive(false);
        }

        private void GenerateDistricts()
        {
            // Clear old if any (in case of manual re-run)
            foreach (var old in _generatedSectors) Destroy(old);
            _generatedSectors.Clear();

            const float angleStep = 360f / 3f;
            for (var i = 0; i < 3; i++)
            {
                var startAngle = i * angleStep + rotationOffset;
                CreateDistrictSector($"District_{i + 1}", startAngle, angleStep);
            }

            _hasGenerated = true;
        }

        private void CreateDistrictSector(string districtName, float startAngle, float totalAngle)
        {
            var sectorObj = new GameObject(districtName);
            sectorObj.transform.SetParent(transform, false); // Keep local position zero
            sectorObj.transform.localPosition = Vector3.zero;

            // Add to list so we can control it later
            _generatedSectors.Add(sectorObj);

            // Layer logic
            var layerIndex = 0;
            var layerValue = districtLayer.value;
            while (layerValue > 0)
            {
                layerValue >>= 1;
                layerIndex++;
            }

            sectorObj.layer = layerIndex - 1;

            // Mesh
            var mesh = GenerateSectorMesh(startAngle, totalAngle);
            var col = sectorObj.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = true;
            col.isTrigger = true;

            // Energy Producer
            var source = sectorObj.AddComponent<EnergyProducer>();
            source.MaxCapacity.Value = capacityPerDistrict;
            source.BroadcastRadius.Value = mapRadius;
            source.isMobileGenerator = false;
        }

        // [GenerateSectorMesh remains exactly the same as before...]
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
                tris.Add(0);
                tris.Add(nextIdx);
                tris.Add(baseIdx);
                tris.Add(1);
                tris.Add(baseIdx + 1);
                tris.Add(nextIdx + 1);
                tris.Add(baseIdx);
                tris.Add(nextIdx);
                tris.Add(nextIdx + 1);
                tris.Add(baseIdx);
                tris.Add(nextIdx + 1);
                tris.Add(baseIdx + 1);
            }

            var lastIdx = 2 + segments * 2;
            tris.Add(0);
            tris.Add(2);
            tris.Add(3);
            tris.Add(0);
            tris.Add(3);
            tris.Add(1);
            tris.Add(0);
            tris.Add(lastIdx + 1);
            tris.Add(lastIdx);
            tris.Add(0);
            tris.Add(1);
            tris.Add(lastIdx + 1);
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}