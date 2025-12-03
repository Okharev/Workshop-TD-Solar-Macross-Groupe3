using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace Pathing
{
    public class RoadNetworkGenerator : MonoBehaviour
    {
        [Header("Terrain Texturing")] public bool applySplatting;
        public int terrainLayerIndex;
        public float splatWidthBuffer = 2.0f;

        [Header("Main Settings")] public SplineContainer splineContainer;
        public float groundOffset = 0.05f;
        public bool useSplineTwist = true;

        [Header("Navigation Logic")] 
        public int roadEdgeAreaID = 3; // High Cost
        public int roadCenterAreaID = 4; // Low Cost
        
        [Tooltip("Prefab with a NavMeshObstacle(Carve=True).")]
        public GameObject navigationBlockerPrefab;
        
        [Tooltip("Lifts the blocker up. Useful if your prefab pivot is in the center.")]
        public float blockerHeightOffset = 0.0f; // <--- NEW: Fixes "half in ground"

        [Range(0.1f, 0.9f)] public float centerLaneWidthPercent = 0.5f;

        [Header("Junction Corners")] [Range(1, 10)]
        public int cornerResolution = 5;
        public float cornerCurveStrength = 0.5f;

        [Header("Junction Quality")] [Range(1, 10)]
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

        public Material roadMaterial;
        public Material navDebugMaterial; 

        [Header("Per-Spline Configuration")] public List<RoadProfile> roadProfiles = new();

        private readonly Dictionary<int, SplineConnectionIds> _connectionMap = new();
        private readonly Dictionary<int, (SeamData StartSeam, SeamData EndSeam)> _seamPositionMap = new();
        
        // Registry to look up roads by index at runtime
        private readonly Dictionary<int, RoadSegmentController> _roadRegistry = new();

        private void Awake()
        {
            // CRITICAL FIX: Rebuild registry at runtime startup
            // because Dictionaries are not saved in the scene.
            _roadRegistry.Clear();
            var controllers = GetComponentsInChildren<RoadSegmentController>();
            foreach (var controller in controllers)
            {
                if (!_roadRegistry.ContainsKey(controller.SplineIndex))
                {
                    _roadRegistry.Add(controller.SplineIndex, controller);
                }
            }
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (autoUpdate && !Application.isPlaying)
                EditorApplication.delayCall += () =>
                {
                    if (this) Generate();
                };
        }
#endif

        [ContextMenu("Force Generate Road")]
        public void Generate()
        {
            if (!splineContainer) splineContainer = GetComponent<SplineContainer>();
            if (!splineContainer) return;

            SyncProfiles();
            _connectionMap.Clear();
            _seamPositionMap.Clear();
            _roadRegistry.Clear(); 

            // 1. Cleanup Children
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            // 2. Build Graph & Pre-calculate Trims
            var nodes = BuildNodeGraph();
            var splineTrims = new Dictionary<int, Vector2>();
            for (var i = 0; i < splineContainer.Splines.Count; i++)
            {
                splineTrims[i] = new Vector2(0f, 1f);
                _connectionMap[i] = new SplineConnectionIds();
                _seamPositionMap[i] = (new SeamData(), new SeamData());
            }

            // 3. Generate Junctions
            var junctionCount = 0;
            foreach (var node in nodes)
                if (node.Connections.Count > 2)
                    GenerateJunctionObject(node, splineTrims, junctionCount++);

            // 4. Generate Road Strips
            var splineIndex = 0;
            foreach (var spline in splineContainer.Splines)
            {
                var trim = splineTrims[splineIndex];
                var profile = roadProfiles[splineIndex];

                GenerateRoadObject(spline, splineIndex, trim.x, trim.y, profile);
                splineIndex++;
            }

            ApplyTerrainSplatting();
        }

        // --- API for External Scripts ---

        public void SetRoadBlocked(int splineIndex, bool isBlocked)
        {
            if (_roadRegistry.TryGetValue(splineIndex, out var controller))
            {
                // We call a specific Internal method to avoid infinite loops
                // if the controller tries to call back to us.
                controller.SetBlockedInternal(isBlocked);
            }
            else
            {
                Debug.LogWarning($"[RoadNetworkGenerator] Cannot find road for Spline Index {splineIndex}.");
            }
        }

        // --------------------------------------------------------------------------------
        // Object Generation Helpers
        // --------------------------------------------------------------------------------

        private void GenerateJunctionObject(Node node, Dictionary<int, Vector2> trims, int index)
        {
            var meshData = new MeshData();
            GenerateJunctionGeometry(node, meshData, trims);
            if (meshData.verts.Count == 0) return;

            var go = CreateMeshObject($"Junction_{index}", meshData, roadEdgeAreaID);
            go.transform.SetParent(transform, false);
        }

        private void GenerateRoadObject(Spline spline, int splineIndex, float tStart, float tEnd, RoadProfile profile)
        {
            var halfCenter = centerLaneWidthPercent * 0.5f;
            var seamLeft = 0.5f - halfCenter;
            var seamRight = 0.5f + halfCenter;

            // 1. Generate the Three Strips
            var leftData = new MeshData();
            GenerateRoadRibbon(spline, splineIndex, tStart, tEnd, profile, 0.0f, seamLeft, leftData);

            var rightData = new MeshData();
            GenerateRoadRibbon(spline, splineIndex, tStart, tEnd, profile, seamRight, 1.0f, rightData);

            var centerData = new MeshData();
            GenerateRoadRibbon(spline, splineIndex, tStart, tEnd, profile, seamLeft, seamRight, centerData);

            // 2. Combine Left and Right into the Main Road Mesh
            var combinedRoad = new MeshData();
            combinedRoad.verts.AddRange(leftData.verts);
            combinedRoad.colors.AddRange(leftData.colors);
            combinedRoad.tris.AddRange(leftData.tris);

            var vertOffset = leftData.verts.Count;
            combinedRoad.verts.AddRange(rightData.verts);
            combinedRoad.colors.AddRange(rightData.colors);
            foreach (var t in rightData.tris) combinedRoad.tris.Add(t + vertOffset);

            // Create Main Road Object
            if (combinedRoad.verts.Count > 0)
            {
                var roadGO = CreateMeshObject($"Road_{splineIndex}", combinedRoad, roadEdgeAreaID);
                roadGO.transform.SetParent(transform, false);

                // --- NEW: Setup Navigation Blocker with Profile Settings ---
                GameObject blockerInstance = null;
                bool initialBlockedState = profile.defaultBlocked; // Read from profile

                if (navigationBlockerPrefab != null)
                {
                    float midT = Mathf.Lerp(tStart, tEnd, 0.5f);
                    spline.Evaluate(midT, out var rawP, out var rawTan, out var rawUp);
                    
                    Vector3 snappedPos = SnapToGround((Vector3)rawP);
                    
                    // --- FIX: Apply Height Offset ---
                    snappedPos += Vector3.up * blockerHeightOffset;

                    Vector3 forward = math.normalizesafe(rawTan);
                    Vector3 up = useSplineTwist ? math.normalizesafe(rawUp) : Vector3.up;
                    if(forward == Vector3.zero) forward = Vector3.forward;

                    blockerInstance = Instantiate(navigationBlockerPrefab, roadGO.transform);
                    blockerInstance.name = "Nav_Blocker";
                    blockerInstance.transform.localPosition = snappedPos; 
                    blockerInstance.transform.rotation = Quaternion.LookRotation(forward, up);
                    
                    // Set initial state based on profile
                    blockerInstance.SetActive(initialBlockedState);
                }

                // Add Controller and Register
                var controller = roadGO.AddComponent<RoadSegmentController>();
                // --- FIX: Pass 'this' (RoadNetworkGenerator) to the controller ---
                controller.Initialize(this, splineIndex, blockerInstance, initialBlockedState);
                _roadRegistry[splineIndex] = controller;

#if UNITY_EDITOR
                GameObjectUtility.SetStaticEditorFlags(roadGO, StaticEditorFlags.NavigationStatic);
#endif

                // 3. Create the Center Lane Object
                if (centerData.verts.Count > 0)
                {
                    var centerGO = new GameObject("Nav_Center");
                    centerGO.transform.SetParent(roadGO.transform, false);
                    centerGO.transform.localPosition = Vector3.zero;

                    var mf = centerGO.AddComponent<MeshFilter>();
                    var mr = centerGO.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = roadMaterial;

                    var centerMesh = new Mesh();
                    centerMesh.name = $"CenterMesh_{splineIndex}";
                    centerMesh.SetVertices(centerData.verts);
                    centerMesh.SetTriangles(centerData.tris, 0);
                    centerMesh.SetColors(centerData.colors);
                    centerMesh.RecalculateNormals();
                    centerMesh.RecalculateTangents();
                    mf.sharedMesh = centerMesh;

                    var mod = centerGO.AddComponent<NavMeshModifier>();
                    mod.overrideArea = true;
                    mod.area = roadCenterAreaID;

#if UNITY_EDITOR
                    GameObjectUtility.SetStaticEditorFlags(centerGO, StaticEditorFlags.NavigationStatic);
#endif
                }
            }
        }

        private void GenerateRoadRibbon(Spline spline, int splineIndex, float tStart, float tEnd, RoadProfile profile,
            float uMin, float uMax, MeshData data)
        {
            var splineLength = spline.GetLength();
            var activeLength = (tEnd - tStart) * splineLength;
            if (activeLength < 0.01f) return;

            var segments = Mathf.CeilToInt(activeLength / Mathf.Max(0.1f, profile.vertexSpacing));
            var widthPercent = uMax - uMin;
            var resolution = Mathf.Max(1, Mathf.RoundToInt(profile.crossResolution * widthPercent));
            var prevRowIndices = new List<int>();

            for (var i = 0; i <= segments; i++)
            {
                var tRaw = i / (float)segments;
                var t = Mathf.Lerp(tStart, tEnd, tRaw);
                var currentWidth = GetNaturalWidth(t, splineLength, profile.width);

                spline.Evaluate(t, out var rawPos, out var rawTangent, out var rawUp);
                var splinePos = (Vector3)rawPos;
                var forward = ((Vector3)rawTangent).normalized;
                if (forward == Vector3.zero) forward = Vector3.forward;
                var localUp = useSplineTwist ? ((Vector3)rawUp).normalized : Vector3.up;
                var right = Vector3.Cross(localUp, forward).normalized;

                var currentRowIndices = new List<int>();

                for (var x = 0; x <= resolution; x++)
                {
                    var uLocal = x / (float)resolution;
                    var uGlobal = Mathf.Lerp(uMin, uMax, uLocal);
                    var offsetMultiplier = uGlobal - 0.5f;
                    var posRaw = splinePos + right * (offsetMultiplier * currentWidth);
                    var posSnapped = SnapToGround(posRaw); 

                    data.verts.Add(posSnapped);
                    data.colors.Add(CalculateVertexColor(currentWidth, uGlobal)); 
                    currentRowIndices.Add(data.verts.Count - 1);
                }

                if (i > 0)
                    for (var x = 0; x < resolution; x++)
                    {
                        var currentLeft = currentRowIndices[x];
                        var currentRight = currentRowIndices[x + 1];
                        var prevLeft = prevRowIndices[x];
                        var prevRight = prevRowIndices[x + 1];

                        data.tris.Add(prevLeft);
                        data.tris.Add(currentLeft);
                        data.tris.Add(prevRight);

                        data.tris.Add(prevRight);
                        data.tris.Add(currentLeft);
                        data.tris.Add(currentRight);
                    }

                prevRowIndices = currentRowIndices;
            }
        }

        private GameObject CreateMeshObject(string name, MeshData data, int areaID)
        {
            var go = new GameObject(name) { layer = gameObject.layer };
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mc = go.AddComponent<MeshCollider>();
            var mod = go.AddComponent<NavMeshModifier>();

            mr.sharedMaterial = roadMaterial;
            var mesh = new Mesh { name = name + "_Mesh", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(data.verts);
            mesh.SetTriangles(data.tris, 0);
            mesh.SetColors(data.colors);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;
            mc.sharedMesh = mesh;
            mod.overrideArea = true;
            mod.area = areaID;

            return go;
        }

        // --------------------------------------------------------------------------------
        // Geometry / Junction / Util Methods
        // --------------------------------------------------------------------------------

        private void GenerateJunctionGeometry(Node node, MeshData data, Dictionary<int, Vector2> trims)
        {
            node.Connections.Sort((a, b) => {
                var angleA = Mathf.Atan2(a.Tangent.x, a.Tangent.z);
                var angleB = Mathf.Atan2(b.Tangent.x, b.Tangent.z);
                return angleA.CompareTo(angleB);
            });
            var centerPos = SnapToGround(node.Position);
            var rightEdges = new List<JunctionEdge>();
            var leftEdges = new List<JunctionEdge>();

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
                var seamPositions = new List<Vector3>();

                for (var s = 0; s <= steps; s++)
                {
                    var tLength = s / (float)steps;
                    var currentHalfWidth = Mathf.Lerp(0f, effectiveWidth * 0.5f, tLength);
                    var rayPos = Vector3.Lerp(centerPos, rawPos, tLength);
                    var currentRowIndices = new List<int>();

                    for (var x = 0; x <= crossRes; x++)
                    {
                        var u = x / (float)crossRes;
                        var widthOffset = (u - 0.5f) * 2f * currentHalfWidth;
                        var posSnapped = SnapToGround(rayPos + right * widthOffset);
                        var newIdx = data.verts.Count;
                        data.verts.Add(posSnapped);
                        currentRowIndices.Add(newIdx);
                        var ringWidth = Mathf.Lerp(0f, effectiveWidth, tLength);
                        var col = CalculateVertexColor(ringWidth, u);
                        col.a = 1.0f;
                        data.colors.Add(col);

                        if (x == 0) armLeftIndices.Add(newIdx);
                        if (x == crossRes) armRightIndices.Add(newIdx);
                        if (s == steps) seamPositions.Add(posSnapped);
                    }
                    if (s > 0) {
                         for (var x = 0; x < crossRes; x++) {
                            var c = currentRowIndices[x + 1]; var d = currentRowIndices[x];
                            var b = prevRowIndices[x]; var a = prevRowIndices[x + 1];
                            if (conn.IsStart) { data.tris.Add(a); data.tris.Add(b); data.tris.Add(d); data.tris.Add(a); data.tris.Add(d); data.tris.Add(c); }
                            else { data.tris.Add(a); data.tris.Add(d); data.tris.Add(b); data.tris.Add(a); data.tris.Add(c); data.tris.Add(d); }
                        }
                    }
                    prevRowIndices = currentRowIndices;
                }
                var sd = new SeamData { Positions = seamPositions, Tangent = outDirection };
                if (conn.IsStart) _seamPositionMap[conn.SplineIndex] = (sd, _seamPositionMap[conn.SplineIndex].EndSeam);
                else _seamPositionMap[conn.SplineIndex] = (_seamPositionMap[conn.SplineIndex].StartSeam, sd);

                var finalLeft = data.verts[prevRowIndices[0]];
                var finalRight = data.verts[prevRowIndices[crossRes]];
                if (conn.IsStart) {
                    leftEdges.Add(new JunctionEdge { position = finalLeft, tangentDir = outDirection, indices = armLeftIndices });
                    rightEdges.Add(new JunctionEdge { position = finalRight, tangentDir = outDirection, indices = armRightIndices });
                } else {
                    leftEdges.Add(new JunctionEdge { position = finalRight, tangentDir = outDirection, indices = armRightIndices });
                    rightEdges.Add(new JunctionEdge { position = finalLeft, tangentDir = outDirection, indices = armLeftIndices });
                }
            }
            
            var count = node.Connections.Count;
            for (var i = 0; i < count; i++) {
                var startEdge = rightEdges[i];
                var endEdge = leftEdges[(i + 1) % count];
                var p0 = startEdge.position; var p3 = endEdge.position;
                var dist = Vector3.Distance(p0, p3);
                var p1 = p0 + startEdge.tangentDir * dist * cornerCurveStrength;
                var p2 = p3 + endEdge.tangentDir * dist * cornerCurveStrength;
                var prevColumnIndices = new List<int>();
                var curveSteps = Mathf.Max(1, cornerResolution);
                var ringSteps = Mathf.Max(1, junctionResolution);

                for (var s = 0; s <= curveSteps; s++) {
                    var tCurve = s / (float)curveSteps;
                    var rimPos = CalculateCubicBezierPoint(tCurve, p0, p1, p2, p3);
                    var currentColumnIndices = new List<int>();
                    var isStartWeld = s == 0; var isEndWeld = s == curveSteps;
                    for (var r = 0; r <= ringSteps; r++) {
                        var currentIdx = -1;
                        if (isStartWeld && r < startEdge.indices.Count) currentIdx = startEdge.indices[r];
                        else if (isEndWeld && r < endEdge.indices.Count) currentIdx = endEdge.indices[r];
                        if (currentIdx == -1) {
                            var tRing = r / (float)ringSteps;
                            var posRaw = Vector3.Lerp(centerPos, rimPos, tRing);
                            if (r == ringSteps) posRaw = rimPos;
                            var posSnapped = SnapToGround(posRaw);
                            currentIdx = data.verts.Count;
                            data.verts.Add(posSnapped);
                            var redChannel = 1f - Mathf.SmoothStep(0.5f, 1.0f, tRing);
                            var alpha = r == ringSteps ? 0f : 1f;
                            data.colors.Add(new Color(redChannel, 0, 0, alpha));
                        }
                        currentColumnIndices.Add(currentIdx);
                        if (s > 0 && r > 0) {
                            var c = currentIdx; var d = currentColumnIndices[r - 1];
                            var b = prevColumnIndices[r]; var a = prevColumnIndices[r - 1];
                            data.tris.Add(a); data.tris.Add(b); data.tris.Add(c); data.tris.Add(a); data.tris.Add(c); data.tris.Add(d);
                        }
                    }
                    prevColumnIndices = currentColumnIndices;
                }
            }
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
            var roadTypeStrength = Mathf.InverseLerp(minDirtWidth, maxStoneWidth, currentWidth);
            var distFromCenter = Mathf.Abs(uAcross - 0.5f) * 2f;
            var shoulderMask = 1f - Mathf.SmoothStep(0.6f, 0.95f, distFromCenter);
            var finalRed = roadTypeStrength * shoulderMask;
            var alpha = 1f;
            if (distFromCenter > 0.95f) alpha = Mathf.InverseLerp(1.0f, 0.95f, distFromCenter);
            return new Color(finalRed, 0, 0, alpha);
        }

        private void SyncProfiles()
        {
            var targetCount = splineContainer.Splines.Count;
            if (roadProfiles.Count > targetCount)
                roadProfiles.RemoveRange(targetCount, roadProfiles.Count - targetCount);
            while (roadProfiles.Count < targetCount)
                roadProfiles.Add(new RoadProfile { name = $"Spline {roadProfiles.Count}", width = 6.0f });
        }

        private void ApplyTerrainSplatting()
        {
            if (!applySplatting || !splineContainer) return;
            var terrain = Terrain.activeTerrain;
            if (!terrain) return;
            var tData = terrain.terrainData;
            var alphamapW = tData.alphamapWidth;
            var alphamapH = tData.alphamapHeight;
            var splatmapData = tData.GetAlphamaps(0, 0, alphamapW, alphamapH);
            var numLayers = tData.alphamapLayers;
            if (terrainLayerIndex >= numLayers) return;

            for (var i = 0; i < splineContainer.Splines.Count; i++)
            {
                var spline = splineContainer.Splines[i];
                var profile = roadProfiles[Mathf.Min(i, roadProfiles.Count - 1)];
                var totalLen = spline.GetLength();
                var steps = Mathf.CeilToInt(totalLen / 1.0f);
                for (var s = 0; s <= steps; s++)
                {
                    var t = s / (float)steps;
                    var worldPos = (Vector3)spline.EvaluatePosition(t);
                    worldPos = transform.TransformPoint(worldPos);
                    var terrainPos = worldPos - terrain.transform.position;
                    var normX = terrainPos.x / tData.size.x;
                    var normZ = terrainPos.z / tData.size.z;
                    var mapX = Mathf.RoundToInt(normX * alphamapW);
                    var mapZ = Mathf.RoundToInt(normZ * alphamapH);
                    var roadWidthWorld = profile.width + splatWidthBuffer;
                    var brushRadius = Mathf.RoundToInt(roadWidthWorld / tData.size.x * alphamapW * 0.5f);
                    for (var x = -brushRadius; x <= brushRadius; x++)
                    for (var y = -brushRadius; y <= brushRadius; y++)
                    {
                        var finalX = mapX + x;
                        var finalY = mapZ + y;
                        if (finalX >= 0 && finalX < alphamapW && finalY >= 0 && finalY < alphamapH)
                        {
                            splatmapData[finalY, finalX, terrainLayerIndex] = 1f;
                            for (var l = 0; l < numLayers; l++)
                                if (l != terrainLayerIndex)
                                    splatmapData[finalY, finalX, l] = 0f;
                        }
                    }
                }
            }
            tData.SetAlphamaps(0, 0, splatmapData);
        }

        private Vector3 SnapToGround(Vector3 localPos)
        {
            if (!snapToTerrain) return localPos;
            var worldPos = transform.TransformPoint(localPos);
            var startPos = worldPos + Vector3.up * (raycastDistance * 0.5f);
            if (Physics.Raycast(startPos, Vector3.down, out var hit, raycastDistance, groundLayer))
            {
                var finalWorldPos = hit.point + Vector3.up * groundOffset;
                return transform.InverseTransformPoint(finalWorldPos);
            }
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

        private void AddConnectionToGraph(List<Node> nodes, Vector3 pos, int splineIdx, bool isStart, Spline spline, float dist)
        {
            var node = nodes.FirstOrDefault(n => Vector3.Distance(n.Position, pos) < dist);
            if (node == null) { node = new Node { Position = pos }; nodes.Add(node); }
            float3 tan, up, p;
            if (isStart) spline.Evaluate(0f, out p, out tan, out up);
            else spline.Evaluate(1f, out p, out tan, out up);
            Vector3 tangentDir = math.normalizesafe(tan);
            var outDir = isStart ? -tangentDir : tangentDir;
            node.Connections.Add(new Connection { SplineIndex = splineIdx, IsStart = isStart, Tangent = outDir });
        }

        private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var u = 1 - t; var tt = t * t; var uu = u * u; var uuu = uu * u; var ttt = tt * t;
            var p = uuu * p0; p += 3 * uu * t * p1; p += 3 * u * tt * p2; p += ttt * p3;
            return p;
        }

        // --- Data Classes ---

        private class MeshData { public readonly List<Color> colors = new(); public readonly List<int> tris = new(); public readonly List<Vector3> verts = new(); }
        private class SeamData { public List<Vector3> Positions = new(); public Vector3 Tangent; }
        
        [Serializable]
        public class RoadProfile
        {
            public string name = "Road Settings";
            [Min(0.1f)] public float width = 3.0f;
            [Range(0.1f, 10f)] public float vertexSpacing = 1.0f;
            [Range(1, 8)] public int crossResolution = 4;
            [Range(0.5f, 12f)] public float junctionRetractMultiplier = 2.5f;

            [Header("Navigation")]
            [Tooltip("If true, the road will spawn with the NavBlocker active.")]
            public bool defaultBlocked = true; 
        }

        private class Node { public readonly List<Connection> Connections = new(); public Vector3 Position; }
        private struct Connection { public bool IsStart; public int SplineIndex; public Vector3 Tangent; }
        private class SplineConnectionIds { public readonly List<int> EndIndices = new(); public readonly List<int> StartIndices = new(); }
        private struct JunctionEdge { public Vector3 position; public Vector3 tangentDir; public List<int> indices; }
    }
}