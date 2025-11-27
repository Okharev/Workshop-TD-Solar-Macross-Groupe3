using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Splines;

namespace Pathing
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class RoadNetworkGenerator : MonoBehaviour
    {
        [SerializeField] [HideInInspector] private List<GameObject> spawnedBlockagesList = new();
        private Dictionary<int, GameObject> _blockageMap = new();
    
        [Header("Props & Decor")] public GameObject blockagePrefab; 
        
        [Header("Terrain Texturing")] 
        public bool applySplatting = false;
        [Tooltip("L'index de la couche de texture (Layer) du terrain à peindre sous la route (0, 1, 2...).")]
        public int terrainLayerIndex = 0; 
        [Tooltip("Largeur supplémentaire à peindre autour de la route.")]
        public float splatWidthBuffer = 2.0f;

        [SerializeField] [HideInInspector] private List<GameObject> _spawnedProps = new();

        [Header("Debug Tools")] 
        [Range(0.1f, 1f)] public float gizmoSize = 0.3f;
        [Range(1, 10)] public int gizmoSkip = 1; 
    
        [Header("Main Settings")] public SplineContainer splineContainer;

        public float groundOffset = 0.05f;
        public bool useSplineTwist = true;

        [Header("Junction Corners")] [Tooltip("1 = Straight Line, 5+ = Smooth Curve")] [Range(1, 10)]
        public int cornerResolution = 5;

        [Tooltip("How much the corner curves outward. 0.5 is standard.")]
        public float cornerCurveStrength = 0.5f;

        [Header("Junction Quality")]
        [Tooltip("Number of rings from center to rim. Higher = better ground snapping.")]
        [Range(1, 10)]
        public int junctionResolution = 4;

        [Header("Editor Performance")] public bool autoUpdate;
        [Header("Terrain Snapping")] public bool snapToTerrain = true;

        public LayerMask groundLayer;
        public float raycastDistance = 100f;

        [Header("Cozy / Organic Feel")] public bool enableWobble = true;

        public float wobbleScale = 0.5f;
        public float wobbleAmount = 0.8f;
        public bool intersectionBulge = true;
        public float bulgeMultiplier = 1.3f;

        [Header("Material Blending")] public float maxStoneWidth = 8.0f;

        public float minDirtWidth = 2.0f;

        [Header("Per-Spline Configuration")] public List<RoadProfile> roadProfiles = new();

        [HideInInspector] public Mesh generatedMesh;

        private readonly Dictionary<int, SplineConnectionIds> _connectionMap = new();
        [SerializeField] private List<Vector3> debugJunctionPoints = new();
 
        public void ResetBlockages()
        {
            var count = Mathf.Min(splineContainer.Splines.Count, roadProfiles.Count);
            for (var i = 0; i < count; i++)
            {
                roadProfiles[i].isBlocked = true;
            }


            GenerateBlockages();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (autoUpdate) 
                EditorApplication.delayCall += () =>
                {
                    if (this != null && !Application.isPlaying) Generate();
                };
#endif
        }


        private Dictionary<int, GameObject> _blockageInstances = new();


        private void GenerateBlockages()
        {
            // 1. CLEANUP PHASE
            // Check the serialized list first (Editor safety)
            if (spawnedBlockagesList != null)
            {
                for (int i = spawnedBlockagesList.Count - 1; i >= 0; i--)
                {
                    var obj = spawnedBlockagesList[i];
                    if (obj != null)
                    {
                        if (Application.isPlaying) Destroy(obj);
                        else DestroyImmediate(obj);
                    }
                }
            }
        
            // Also check the runtime dictionary just in case
            foreach (var kvp in _blockageMap)
            {
                if (kvp.Value != null)
                {
                    if (Application.isPlaying) Destroy(kvp.Value);
                    else DestroyImmediate(kvp.Value);
                }
            }

            // Reset containers
            spawnedBlockagesList = new List<GameObject>();
            _blockageMap.Clear();

            if (!blockagePrefab) return;

            // 2. SPAWN PHASE
            var count = Mathf.Min(splineContainer.Splines.Count, roadProfiles.Count);
            for (var i = 0; i < count; i++)
            {
                if (roadProfiles[i].isBlocked)
                {
                    SpawnBlockageForSpline(i);
                }
            }
        }

        private void SpawnBlockageForSpline(int index)
        {
            var spline = splineContainer.Splines[index];
            spline.Evaluate(0.5f, out var localPos, out var localTan, out var localUp);

            var worldPos = splineContainer.transform.TransformPoint(localPos);
            var worldTan = splineContainer.transform.TransformDirection(localTan);
            var worldUp = splineContainer.transform.TransformDirection(localUp);

            var finalPos = SnapToGround(worldPos);
            var rot = Quaternion.LookRotation(worldTan, worldUp);

            GameObject obj;
#if UNITY_EDITOR
            if (!Application.isPlaying &&
                PrefabUtility.GetPrefabAssetType(blockagePrefab) != PrefabAssetType.NotAPrefab)
                obj = (GameObject)PrefabUtility.InstantiatePrefab(blockagePrefab, transform);
            else
#endif
                obj = Instantiate(blockagePrefab, transform);

            obj.transform.position = finalPos;
            obj.transform.rotation = rot;
            obj.name = $"Blockage_Spline_{index}";

            // Add to BOTH containers
            spawnedBlockagesList.Add(obj); // Keeps reference safe in Editor
            _blockageMap[index] = obj;      // Allows fast access in Game
        }

        // [CHANGE 3] Public API for the Wave Manager
        public void UnlockRoad(int splineIndex)
        {
            // Failsafe: Rebuild dictionary if empty (happens if you start game without regenerating)
            if (_blockageMap.Count == 0 && spawnedBlockagesList.Count > 0)
            {
                RebuildBlockageMap();
            }

            if (splineIndex < 0 || splineIndex >= roadProfiles.Count) return;

            // 1. Update Data so it doesn't come back on regeneration
            roadProfiles[splineIndex].isBlocked = false;

            // 2. Remove Visuals
            if (_blockageMap.TryGetValue(splineIndex, out GameObject prop))
            {
                if (prop != null)
                {
                    // Remove from the Serialized List too so it doesn't cause null errors later
                    if (spawnedBlockagesList.Contains(prop)) spawnedBlockagesList.Remove(prop);

                    if (Application.isPlaying) Destroy(prop);
                    else DestroyImmediate(prop);
                }
                _blockageMap.Remove(splineIndex);
            }
        }

        // Helper to map the list back to dictionary at Runtime Start
        private void RebuildBlockageMap()
        {
            _blockageMap.Clear();
            foreach (var obj in spawnedBlockagesList)
            {
                if (obj == null) continue;
            
                // Extract ID from name "Blockage_Spline_3"
                string[] parts = obj.name.Split('_');
                if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int id))
                {
                    _blockageMap[id] = obj;
                }
            }
        }

        private void Awake()
        {
            // Ensure map is ready when game starts
            RebuildBlockageMap();
        }

        [ContextMenu("Force Generate Road")]
        public void Generate()
        {
            debugJunctionPoints.Clear();
            _connectionMap.Clear();

            if (!splineContainer) splineContainer = GetComponent<SplineContainer>();
            if (!splineContainer) return;

            SyncProfiles();

            GenerateBlockages();

            if (!generatedMesh) generatedMesh = new Mesh();
            generatedMesh.Clear();
            generatedMesh.name = "ProceduralRoadNetwork";
            generatedMesh.indexFormat = IndexFormat.UInt32;

            var allVerts = new List<Vector3>();
            var allTris = new List<int>();
            // allUvs removed
            var allColors = new List<Color>();

            var nodes = BuildNodeGraph();

            var splineTrims = new Dictionary<int, Vector2>();
            for (var i = 0; i < splineContainer.Splines.Count; i++)
            {
                splineTrims[i] = new Vector2(0f, 1f);
                _connectionMap[i] = new SplineConnectionIds(); 
            }

            foreach (var node in nodes)
                if (node.Connections.Count > 2)
                    GenerateJunctionMesh(node, allVerts, allTris, allColors, splineTrims);

            var splineIndex = 0;
            foreach (var spline in splineContainer.Splines)
            {
                var trim = splineTrims[splineIndex];
                var profile = roadProfiles[splineIndex];
                GenerateRoadStrip(spline, splineIndex, trim.x, trim.y, profile, allVerts, allTris, allColors);
                splineIndex++;
            }

            generatedMesh.SetVertices(allVerts);
            generatedMesh.SetTriangles(allTris, 0);
            generatedMesh.SetColors(allColors);

            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateTangents();
            generatedMesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = generatedMesh;
            MeshCollider mc = GetComponent<MeshCollider>();
            if (mc) 
            {
                mc.sharedMesh = null;
                mc.sharedMesh = generatedMesh;
            }

            ApplyTerrainSplatting();
        }
    
        private float GetNaturalWidth(float t, float splineLength, float baseWidth)
        {
            var currentWidth = baseWidth;
            if (enableWobble)
            {
                var noise = Mathf.PerlinNoise(t * splineLength * wobbleScale, 0f);
                var offset = (noise * 2.0f - 1.0f) * wobbleAmount;
                currentWidth += offset;
            }

            if (intersectionBulge)
            {
                var distFromEnd = Mathf.Min(t, 1.0f - t);
                if (distFromEnd < 0.1f)
                {
                    var bulgeFactor = 1.0f - distFromEnd / 0.1f;
                    bulgeFactor = bulgeFactor * bulgeFactor * (3f - 2f * bulgeFactor);
                    currentWidth = Mathf.Lerp(currentWidth, baseWidth * bulgeMultiplier, bulgeFactor);
                }
            }

            return currentWidth;
        }

        private Color CalculateVertexColor(float currentWidth, float uAcross)
        {
            float roadTypeStrength = Mathf.InverseLerp(minDirtWidth, maxStoneWidth, currentWidth);
            float distFromCenter = Mathf.Abs(uAcross - 0.5f) * 2f;
            float shoulderMask = 1f - Mathf.SmoothStep(0.6f, 0.95f, distFromCenter);
            float finalRed = roadTypeStrength * shoulderMask;

            float alpha = 1f;
            if (distFromCenter > 0.95f)
            {
                alpha = Mathf.InverseLerp(1.0f, 0.95f, distFromCenter);
            }

            return new Color(finalRed, 0, 0, alpha);
        }

        private void SyncProfiles()
        {
            var targetCount = splineContainer.Splines.Count;
            if (roadProfiles.Count > targetCount) roadProfiles.RemoveRange(targetCount, roadProfiles.Count - targetCount);
            while (roadProfiles.Count < targetCount)
                roadProfiles.Add(new RoadProfile { name = $"Spline {roadProfiles.Count}", width = 6.0f });
        }
        
        private void ApplyTerrainSplatting()
{
    if (!applySplatting || splineContainer == null) return;

    Terrain terrain = Terrain.activeTerrain;
    if (terrain == null) return;

    TerrainData tData = terrain.terrainData;
    int alphamapW = tData.alphamapWidth;
    int alphamapH = tData.alphamapHeight;
    
    // On récupère toutes les données de texture (Lourd, mais nécessaire)
    // float[y, x, layer]
    float[,,] splatmapData = tData.GetAlphamaps(0, 0, alphamapW, alphamapH);
    int numLayers = tData.alphamapLayers;

    // Sécurité : Vérifier que l'index existe
    if (terrainLayerIndex >= numLayers)
    {
        Debug.LogWarning("Index de layer terrain invalide !");
        return;
    }

    // Parcourir chaque Spline
    for (int i = 0; i < splineContainer.Splines.Count; i++)
    {
        var spline = splineContainer.Splines[i];
        var profile = roadProfiles[Mathf.Min(i, roadProfiles.Count - 1)];
        float totalLen = spline.GetLength();
        
        // On avance le long de la spline tous les X mètres
        float stepSize = 1.0f; // Précision de la peinture
        int steps = Mathf.CeilToInt(totalLen / stepSize);

        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            Vector3 worldPos = (Vector3)spline.EvaluatePosition(t);
            // Convertir la position Monde en position Terrain Local (0 à 1)
            worldPos = transform.TransformPoint(worldPos); // Si la spline est locale
            
            // Calcul des coordonnées sur la splatmap
            Vector3 terrainPos = worldPos - terrain.transform.position;
            float normX = terrainPos.x / tData.size.x;
            float normZ = terrainPos.z / tData.size.z;

            int mapX = Mathf.RoundToInt(normX * alphamapW);
            int mapZ = Mathf.RoundToInt(normZ * alphamapH);

            // Calcul de la largeur de peinture en pixels de texture
            float roadWidthWorld = profile.width + splatWidthBuffer;
            // Conversion largeur Monde -> largeur Pixels Texture
            int brushRadius = Mathf.RoundToInt((roadWidthWorld / tData.size.x) * alphamapW * 0.5f);

            // Peinture du carré autour du point (plus rapide qu'un cercle)
            for (int x = -brushRadius; x <= brushRadius; x++)
            {
                for (int y = -brushRadius; y <= brushRadius; y++)
                {
                    int finalX = mapX + x;
                    int finalY = mapZ + y;

                    // Vérifier si on est dans les limites du terrain
                    if (finalX >= 0 && finalX < alphamapW && finalY >= 0 && finalY < alphamapH)
                    {
                        // On met notre layer cible à 1 (100% opacité)
                        splatmapData[finalY, finalX, terrainLayerIndex] = 1f;

                        // On met tous les autres layers à 0 pour ce pixel
                        for (int l = 0; l < numLayers; l++)
                        {
                            if (l != terrainLayerIndex)
                            {
                                splatmapData[finalY, finalX, l] = 0f;
                            }
                        }
                    }
                }
            }
        }
    }

    // Appliquer les changements au terrain
    tData.SetAlphamaps(0, 0, splatmapData);
}

        private Vector3 SnapToGround(Vector3 localPos)
        {
            if (!snapToTerrain) return localPos;

            // 1. Conversion Local -> World pour que le Raycast parte du bon endroit réel
            Vector3 worldPos = transform.TransformPoint(localPos);

            // 2. On part de très haut au-dessus de ce point
            var startPos = worldPos + Vector3.up * (raycastDistance * 0.5f);

            if (Physics.Raycast(startPos, Vector3.down, out var hit, raycastDistance, groundLayer))
            {
                // 3. On a touché le sol
                // Optionnel : Tu peux utiliser hit.normal pour orienter l'offset si tu veux être précis sur les pentes raides
                Vector3 finalWorldPos = hit.point + Vector3.up * groundOffset;

                // 4. Conversion World -> Local pour le Mesh (car le MeshFilter est local)
                return transform.InverseTransformPoint(finalWorldPos);
            }

            // Fallback : Si on ne touche rien, on garde la position théorique avec l'offset
            return localPos + Vector3.up * groundOffset;
        }
    
        private List<Node> BuildNodeGraph()
        {
            var nodes = new List<Node>();
            var mergeDistance = 0.5f;
            for (var i = 0; i < splineContainer.Splines.Count; i++)
            {
                var spline = splineContainer.Splines[i];
                var startPos = (Vector3)spline.EvaluatePosition(0f);
                var endPos = (Vector3)spline.EvaluatePosition(1f);
                AddConnectionToGraph(nodes, startPos, i, true, spline, mergeDistance);
                if (!spline.Closed) AddConnectionToGraph(nodes, endPos, i, false, spline, mergeDistance);
            }

            return nodes;
        }

        private void AddConnectionToGraph(List<Node> nodes, Vector3 pos, int splineIdx, bool isStart, Spline spline,
            float dist)
        {
            var node = nodes.FirstOrDefault(n => Vector3.Distance(n.Position, pos) < dist);
            if (node == null)
            {
                node = new Node { Position = pos };
                nodes.Add(node);
            }

            float3 tan, up, p;
            if (isStart) spline.Evaluate(0f, out p, out tan, out up);
            else spline.Evaluate(1f, out p, out tan, out up);
            Vector3 tangentDir = math.normalizesafe(tan);
            var outDir = isStart ? -tangentDir : tangentDir;
            node.Connections.Add(new Connection { SplineIndex = splineIdx, IsStart = isStart, Tangent = outDir });
        }
    
        private void GenerateJunctionMesh(Node node, List<Vector3> verts, List<int> tris, List<Color> colors, Dictionary<int, Vector2> trims)
        {
            node.Connections.Sort((a, b) => {
                var angleA = Mathf.Atan2(a.Tangent.x, a.Tangent.z);
                var angleB = Mathf.Atan2(b.Tangent.x, b.Tangent.z);
                return angleA.CompareTo(angleB);
            });

            var centerPos = SnapToGround(node.Position);
            debugJunctionPoints.Add(centerPos);

            var rightEdges = new List<JunctionEdge>();
            var leftEdges = new List<JunctionEdge>();

            // PASSE 1 : Road Arms
            foreach (var conn in node.Connections)
            {
                var profile = roadProfiles[conn.SplineIndex];
                var spline = splineContainer.Splines[conn.SplineIndex];
                var splineLen = spline.GetLength();

                var effectiveWidth = profile.width * (intersectionBulge ? bulgeMultiplier : 1.0f);
                var retractMeters = effectiveWidth * 0.5f * profile.junctionRetractMultiplier;
                var tOffset = splineLen > 0.001f ? Mathf.Clamp01(retractMeters / splineLen) : 0;
    
                if (conn.IsStart) {
                    var current = trims[conn.SplineIndex];
                    trims[conn.SplineIndex] = new Vector2(Mathf.Max(current.x, tOffset), current.y);
                } else {
                    var current = trims[conn.SplineIndex];
                    trims[conn.SplineIndex] = new Vector2(current.x, Mathf.Min(current.y, 1f - tOffset));
                }

                var evalT = conn.IsStart ? tOffset : 1f - tOffset;
                spline.Evaluate(evalT, out var pos, out var tan, out var up);

                Vector3 fwd = math.normalizesafe(tan);
                if (fwd == Vector3.zero) fwd = conn.IsStart ? Vector3.forward : Vector3.back;
                var outDirection = conn.IsStart ? -fwd : fwd;
                var right = Vector3.Cross(Vector3.up, fwd).normalized;
                var rawPos = (Vector3)pos;

                var steps = Mathf.Max(1, junctionResolution);
                var crossRes = Mathf.Max(1, profile.crossResolution);

                var prevRowIndices = new List<int>();
                var armLeftIndices = new List<int>();
                var armRightIndices = new List<int>();

                for (var s = 0; s <= steps; s++)
                {
                    var tLength = s / (float)steps;
                    var currentHalfWidth = Mathf.Lerp(0f, effectiveWidth * 0.5f, tLength);
                    var rayPos = Vector3.Lerp(centerPos, rawPos, tLength);
                    var currentRowIndices = new List<int>();

                    for (var x = 0; x <= crossRes; x++)
                    {
                        // u is still used for GEOMETRY positioning (width), but not for UVs
                        var u = x / (float)crossRes;
                        var widthOffset = (u - 0.5f) * 2f * currentHalfWidth;
                        var posSnapped = SnapToGround(rayPos + right * widthOffset);

                        var newIdx = verts.Count;
                        verts.Add(posSnapped);
                        currentRowIndices.Add(newIdx);

                        // UV addition removed

                        // Color Logic
                        float ringWidth = Mathf.Lerp(0f, effectiveWidth, tLength);
                        Color col = CalculateVertexColor(ringWidth, u);
                        col.a = 1.0f; 
                        colors.Add(col);

                        if (x == 0) {
                            armLeftIndices.Add(newIdx);
                        }
                        if (x == crossRes) {
                            armRightIndices.Add(newIdx);
                        }

                        if (s == steps) {
                            if (conn.IsStart) _connectionMap[conn.SplineIndex].StartIndices.Add(newIdx);
                            else _connectionMap[conn.SplineIndex].EndIndices.Add(newIdx);
                        }
                    }
                
                    if (s > 0)
                        for (var x = 0; x < crossRes; x++) {
                            var c = currentRowIndices[x + 1]; var d = currentRowIndices[x];
                            var b = prevRowIndices[x]; var a = prevRowIndices[x + 1];
                            if (conn.IsStart) {
                                tris.Add(a); tris.Add(b); tris.Add(d); tris.Add(a); tris.Add(d); tris.Add(c);
                            } else {
                                tris.Add(a); tris.Add(d); tris.Add(b); tris.Add(a); tris.Add(c); tris.Add(d);
                            }
                        }
                    prevRowIndices = currentRowIndices;
                }

                var finalLeft = verts[prevRowIndices[0]];
                var finalRight = verts[prevRowIndices[crossRes]];

                // Removed uValue and armLength from JunctionEdge creation
                if (conn.IsStart) {
                    leftEdges.Add(new JunctionEdge { position = finalLeft, tangentDir = outDirection, indices = armLeftIndices });
                    rightEdges.Add(new JunctionEdge { position = finalRight, tangentDir = outDirection, indices = armRightIndices });
                } else {
                    leftEdges.Add(new JunctionEdge { position = finalRight, tangentDir = outDirection, indices = armRightIndices });
                    rightEdges.Add(new JunctionEdge { position = finalLeft, tangentDir = outDirection, indices = armLeftIndices });
                }
            }

            // PASSE 2 : Corners
            var count = node.Connections.Count;
            for (var i = 0; i < count; i++)
            {
                var startEdge = rightEdges[i];
                var endEdge = leftEdges[(i + 1) % count];

                var p0 = startEdge.position;
                var p3 = endEdge.position;
                var dist = Vector3.Distance(p0, p3);
                var p1 = p0 + startEdge.tangentDir * dist * cornerCurveStrength;
                var p2 = p3 + endEdge.tangentDir * dist * cornerCurveStrength;

                var prevColumnIndices = new List<int>();
                var curveSteps = Mathf.Max(1, cornerResolution);
                var ringSteps = Mathf.Max(1, junctionResolution);

                for (var s = 0; s <= curveSteps; s++)
                {
                    var tCurve = s / (float)curveSteps;
                    var rimPos = CalculateCubicBezierPoint(tCurve, p0, p1, p2, p3);
                    var currentColumnIndices = new List<int>();

                    var isStartWeld = s == 0;
                    var isEndWeld = s == curveSteps;
                
                    for (var r = 0; r <= ringSteps; r++)
                    {
                        var currentIdx = -1;

                        if (isStartWeld) {
                            if (r < startEdge.indices.Count) currentIdx = startEdge.indices[r];
                        } else if (isEndWeld) {
                            if (r < endEdge.indices.Count) currentIdx = endEdge.indices[r];
                        }

                        if (currentIdx == -1)
                        {
                            var tRing = r / (float)ringSteps;
                            var posRaw = Vector3.Lerp(centerPos, rimPos, tRing);
                            if (r == ringSteps) posRaw = rimPos; 
                            var posSnapped = SnapToGround(posRaw);

                            currentIdx = verts.Count;
                            verts.Add(posSnapped);

                            // UV addition removed

                            // Color Logic
                            float redChannel = 1f - Mathf.SmoothStep(0.5f, 1.0f, tRing);
                            float alpha = (r == ringSteps) ? 0f : 1f;
                            colors.Add(new Color(redChannel, 0, 0, alpha));
                        }
                        currentColumnIndices.Add(currentIdx);

                        if (s > 0 && r > 0) {
                            var c = currentIdx; var d = currentColumnIndices[r - 1];
                            var b = prevColumnIndices[r]; var a = prevColumnIndices[r - 1];
                            tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d);
                        }
                    }
                    prevColumnIndices = currentColumnIndices;
                }
            }
        }
        
        private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var u = 1 - t;
            var tt = t * t;
            var uu = u * u;
            var uuu = uu * u;
            var ttt = tt * t;

            var p = uuu * p0;
            p += 3 * uu * t * p1;
            p += 3 * u * tt * p2;
            p += ttt * p3;

            return p;
        }

        private void GenerateRoadStrip(Spline spline, int splineIndex, float tStart, float tEnd, RoadProfile profile,
            List<Vector3> verts, List<int> tris, List<Color> colors)
        {
            var splineLength = spline.GetLength();
            var activeLength = (tEnd - tStart) * splineLength;
            if (activeLength < 0.01f) return;

            var segments = Mathf.CeilToInt(activeLength / Mathf.Max(0.1f, profile.vertexSpacing));
            var resolution = Mathf.Max(1, profile.crossResolution);

            var connIds = _connectionMap.TryGetValue(splineIndex, out var value) ? value : new SplineConnectionIds();

            var prevRowIndices = new List<int>();

            for (var i = 0; i <= segments; i++)
            {
                var tRaw = i / (float)segments;
                var t = Mathf.Lerp(tStart, tEnd, tRaw);

                spline.Evaluate(t, out var rawPos, out var rawTangent, out var rawUp);
                var splinePos = (Vector3)rawPos;
                var forward = ((Vector3)rawTangent).normalized;
                if (forward == Vector3.zero) forward = Vector3.forward;
                var up = ((Vector3)rawUp).normalized;
                var localUp = useSplineTwist ? up : Vector3.up;
                var right = Vector3.Cross(localUp, forward).normalized;
                var currentWidth = GetNaturalWidth(t, splineLength, profile.width);

                var currentRowIndices = new List<int>();

                var isStartWeld = i == 0 && connIds.StartIndices.Count == resolution + 1;
                var isEndWeld = i == segments && connIds.EndIndices.Count == resolution + 1;

                for (var x = 0; x <= resolution; x++)
                    if (isStartWeld)
                    {
                        currentRowIndices.Add(connIds.StartIndices[x]);
                    }
                    else if (isEndWeld)
                    {
                        currentRowIndices.Add(connIds.EndIndices[x]);
                    }
                    else
                    {
                        var u = x / (float)resolution;
                        var offsetMultiplier = u - 0.5f;
                        var posRaw = splinePos + right * (offsetMultiplier * currentWidth);
                        var posSnapped = SnapToGround(posRaw);

                        var newIdx = verts.Count;
                        verts.Add(posSnapped);
                        currentRowIndices.Add(newIdx);

                        // UV addition removed
                    
                        colors.Add(CalculateVertexColor(currentWidth, u));
                    }

                if (i > 0)
                    for (var x = 0; x < resolution; x++)
                    {
                        var currentLeft = currentRowIndices[x];
                        var currentRight = currentRowIndices[x + 1];
                        var prevLeft = prevRowIndices[x];
                        var prevRight = prevRowIndices[x + 1];

                        tris.Add(prevLeft);
                        tris.Add(currentLeft);
                        tris.Add(prevRight);
                        tris.Add(prevRight);
                        tris.Add(currentLeft);
                        tris.Add(currentRight);
                    }

                prevRowIndices = currentRowIndices;
            }
        }
    
        public bool showSplineIndices = true;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showSplineIndices || splineContainer == null) return;

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.yellow;
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;

            for (int i = 0; i < splineContainer.Splines.Count; i++)
            {
                var spline = splineContainer.Splines[i];
                // Draw the ID at the center of the spline
                Vector3 centerPos = (Vector3)spline.EvaluatePosition(0.5f);
                centerPos = splineContainer.transform.TransformPoint(centerPos) + Vector3.up * 2f;
            
                Handles.Label(centerPos, $"ID: {i}", style);
            }
        }
#endif

        [Serializable]
        public class RoadProfile
        {
            public string name = "Road Settings";
            [Min(0.1f)] public float width = 3.0f;
            [Range(0.1f, 10f)] public float vertexSpacing = 1.0f;
            [Range(1, 8)] public int crossResolution = 4;
            [Range(0.5f, 12f)] public float junctionRetractMultiplier = 2.5f;

            [Header("Traffic")] public bool isBlocked = true;
        }

        private class Node
        {
            public readonly List<Connection> Connections = new();
            public Vector3 Position;
        }

        private struct Connection
        {
            public bool IsStart;
            public int SplineIndex;
            public Vector3 Tangent;
        }

        private class SplineConnectionIds
        {
            public readonly List<int> EndIndices = new(); 
            public readonly List<int> StartIndices = new();
        }


        private struct JunctionEdge
        {
            public Vector3 position;
            public Vector3 tangentDir;
            public int centerVertIndex;
            public List<int> indices; 
        }

    }
}