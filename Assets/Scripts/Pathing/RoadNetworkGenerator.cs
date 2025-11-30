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
    // We removed RequireComponent MeshFilter/Renderer because the main object 
    // is now just a container for the child segments.
    public class RoadNetworkGenerator : MonoBehaviour
    {
        [Header("Terrain Texturing")] public bool applySplatting;

        public int terrainLayerIndex;
        public float splatWidthBuffer = 2.0f;

        [Header("Main Settings")] public SplineContainer splineContainer;

        public float groundOffset = 0.05f;
        public bool useSplineTwist = true;

        [Header("Navigation Logic")] public int roadEdgeAreaID = 3; // High Cost

        public int roadCenterAreaID = 4; // Low Cost

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

        // Material references to apply to generated objects
        public Material roadMaterial;
        public Material navDebugMaterial; // Optional, to see the center strip

        [Header("Per-Spline Configuration")] public List<RoadProfile> roadProfiles = new();

        private readonly Dictionary<int, SplineConnectionIds> _connectionMap = new();

        // New map to store the spatial data of seams instead of indices
        private readonly Dictionary<int, (SeamData StartSeam, SeamData EndSeam)> _seamPositionMap = new();

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

            // 1. Cleanup Children
            // We loop backwards to destroy safely
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
                _connectionMap[i] = new SplineConnectionIds(); // Still used for logic logic
                _seamPositionMap[i] = (new SeamData(), new SeamData());
            }

            // 3. Generate Junctions (Separate Objects)
            var junctionCount = 0;
            foreach (var node in nodes)
                if (node.Connections.Count > 2)
                    GenerateJunctionObject(node, splineTrims, junctionCount++);

            // 4. Generate Road Strips (Separate Objects)
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

        // --------------------------------------------------------------------------------
        // NEW: Object Generation Helpers
        // --------------------------------------------------------------------------------

        private void GenerateJunctionObject(Node node, Dictionary<int, Vector2> trims, int index)
        {
            var meshData = new MeshData();

            // Logic similar to original, but we populate meshData locally
            GenerateJunctionGeometry(node, meshData, trims);

            if (meshData.verts.Count == 0) return;

            var go = CreateMeshObject($"Junction_{index}", meshData, roadEdgeAreaID);
            go.transform.SetParent(transform, false);
        }

        private void GenerateRoadObject(Spline spline, int splineIndex, float tStart, float tEnd, RoadProfile profile)
        {
            // Calculate the U-values where the road splits
            // e.g. if center is 50%, split is at 0.25 and 0.75
            var halfCenter = centerLaneWidthPercent * 0.5f;
            var seamLeft = 0.5f - halfCenter;
            var seamRight = 0.5f + halfCenter;

            // 1. Generate the Three Strips
            // We use a helper method that generates a specific slice of the road (from uStart to uEnd)

            // Left Shoulder (0.0 to seamLeft)
            var leftData = new MeshData();
            GenerateRoadRibbon(spline, splineIndex, tStart, tEnd, profile, 0.0f, seamLeft, leftData);

            // Right Shoulder (seamRight to 1.0)
            var rightData = new MeshData();
            GenerateRoadRibbon(spline, splineIndex, tStart, tEnd, profile, seamRight, 1.0f, rightData);

            // Center Lane (seamLeft to seamRight)
            var centerData = new MeshData();
            GenerateRoadRibbon(spline, splineIndex, tStart, tEnd, profile, seamLeft, seamRight, centerData);

            // 2. Combine Left and Right into the Main Road Mesh
            // This creates the "Visual" road with a gap in the middle
            var combinedRoad = new MeshData();

            // Add Left
            combinedRoad.verts.AddRange(leftData.verts);
            combinedRoad.colors.AddRange(leftData.colors);
            combinedRoad.tris
                .AddRange(leftData.tris); // Indices are already 0-based for this list? No, we need to offset triangles.

            // Add Right (Offsetting triangle indices)
            var vertOffset = leftData.verts.Count;
            combinedRoad.verts.AddRange(rightData.verts);
            combinedRoad.colors.AddRange(rightData.colors);
            foreach (var t in rightData.tris) combinedRoad.tris.Add(t + vertOffset);

            // Create Main Road Object
            if (combinedRoad.verts.Count > 0)
            {
                var roadGO = CreateMeshObject($"Road_{splineIndex}", combinedRoad, roadEdgeAreaID);
                roadGO.transform.SetParent(transform, false);

#if UNITY_EDITOR
                GameObjectUtility.SetStaticEditorFlags(roadGO, StaticEditorFlags.NavigationStatic);
#endif

                // 3. Create the Center Lane Object (Fills the gap)
                if (centerData.verts.Count > 0)
                {
                    var centerGO = new GameObject("Nav_Center");
                    centerGO.transform.SetParent(roadGO.transform, false);
                    centerGO.transform.localPosition = Vector3.zero;

                    var mf = centerGO.AddComponent<MeshFilter>();
                    var mr = centerGO.AddComponent<MeshRenderer>();

                    // Assign the SAME material so it looks like one solid road
                    mr.sharedMaterial = roadMaterial;

                    // Or use your Debug material if you want to hide it, 
                    // but for the Mosaic method, it's usually better to render it as part of the road.
                    // If you want it invisible, the gap in the road will show the terrain underneath.

                    var centerMesh = new Mesh();
                    centerMesh.name = $"CenterMesh_{splineIndex}";
                    centerMesh.SetVertices(centerData.verts);
                    centerMesh.SetTriangles(centerData.tris, 0);
                    centerMesh.SetColors(centerData.colors); // Keep vertex colors for texture blending
                    centerMesh.RecalculateNormals();
                    centerMesh.RecalculateTangents(); // Important for lighting matching
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

            // We adjust resolution based on how wide this ribbon is compared to full road
            // This keeps vertex density consistent
            var widthPercent = uMax - uMin;
            var resolution = Mathf.Max(1, Mathf.RoundToInt(profile.crossResolution * widthPercent));

            var prevRowIndices = new List<int>();

            for (var i = 0; i <= segments; i++)
            {
                var tRaw = i / (float)segments;
                var t = Mathf.Lerp(tStart, tEnd, tRaw);
                var currentWidth = GetNaturalWidth(t, splineLength, profile.width);

                // Procedural Calculation
                spline.Evaluate(t, out var rawPos, out var rawTangent, out var rawUp);
                var splinePos = (Vector3)rawPos;
                var forward = ((Vector3)rawTangent).normalized;
                if (forward == Vector3.zero) forward = Vector3.forward;
                var localUp = useSplineTwist ? ((Vector3)rawUp).normalized : Vector3.up;
                var right = Vector3.Cross(localUp, forward).normalized;

                var currentRowIndices = new List<int>();

                for (var x = 0; x <= resolution; x++)
                {
                    // Local u (0 to 1) within this ribbon
                    var uLocal = x / (float)resolution;

                    // Global U (0 to 1) relative to the whole road width
                    var uGlobal = Mathf.Lerp(uMin, uMax, uLocal);

                    // Calculate position using Global U
                    var offsetMultiplier = uGlobal - 0.5f;
                    var posRaw = splinePos + right * (offsetMultiplier * currentWidth);
                    var posSnapped = SnapToGround(posRaw); // Everything snaps to same ground -> No Holes

                    data.verts.Add(posSnapped);
                    data.colors.Add(CalculateVertexColor(currentWidth,
                        uGlobal)); // Pass uGlobal for texture consistency
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
            var go = new GameObject(name);
            go.layer = gameObject.layer;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mc = go.AddComponent<MeshCollider>();
            var mod = go.AddComponent<NavMeshModifier>();

            mr.sharedMaterial = roadMaterial;

            var mesh = new Mesh();
            mesh.name = name + "_Mesh";
            mesh.indexFormat = IndexFormat.UInt32;
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
        // MODIFIED: Geometry Generation (Adapting to local MeshData instead of global Lists)
        // --------------------------------------------------------------------------------

        private void GenerateCenterStripGeometry(Spline spline, float tStart, float tEnd, RoadProfile profile,
            MeshData data)
        {
            var splineLength = spline.GetLength();
            var activeLength = (tEnd - tStart) * splineLength;
            if (activeLength < 0.01f) return;

            // Reduce resolution slightly for performance, but keep it decent
            var centerResolution = Mathf.Max(2.0f, profile.vertexSpacing * 2f);
            var segments = Mathf.CeilToInt(activeLength / centerResolution);

            var centerWidth = profile.width * centerLaneWidthPercent;

            // FIX: Lift the NavMesh strip slightly higher than the visual road
            // If groundOffset is 0.05, make this 0.15 or 0.2
            var navHeightOffset = 0.4f;

            var prevLeft = -1;
            var prevRight = -1;

            for (var i = 0; i <= segments; i++)
            {
                var tRaw = i / (float)segments;
                var t = Mathf.Lerp(tStart, tEnd, tRaw);

                spline.Evaluate(t, out var rawPos, out var rawTangent, out var rawUp);
                var splinePos = (Vector3)rawPos;
                var forward = ((Vector3)rawTangent).normalized;
                if (forward == Vector3.zero) forward = Vector3.forward;
                var localUp = useSplineTwist ? ((Vector3)rawUp).normalized : Vector3.up;
                var right = Vector3.Cross(localUp, forward).normalized;

                // Apply the lift here
                var verticalLift = Vector3.up * navHeightOffset;

                // Calculate positions
                // Note: We use SnapToGround + Lift
                var basePos = SnapToGround(splinePos);
                var leftPos = basePos - right * (centerWidth * 0.5f) + verticalLift;
                var rightPos = basePos + right * (centerWidth * 0.5f) + verticalLift;

                data.verts.Add(leftPos);
                data.verts.Add(rightPos);

                var currentLeft = data.verts.Count - 2;
                var currentRight = data.verts.Count - 1;

                if (i > 0)
                {
                    data.tris.Add(prevLeft);
                    data.tris.Add(currentLeft);
                    data.tris.Add(prevRight);

                    data.tris.Add(prevRight);
                    data.tris.Add(currentLeft);
                    data.tris.Add(currentRight);
                }

                prevLeft = currentLeft;
                prevRight = currentRight;
            }
        }

        private void GenerateRoadGeometry(Spline spline, int splineIndex, float tStart, float tEnd, RoadProfile profile,
            MeshData data)
        {
            var splineLength = spline.GetLength();
            var activeLength = (tEnd - tStart) * splineLength;
            if (activeLength < 0.01f) return;

            var segments = Mathf.CeilToInt(activeLength / Mathf.Max(0.1f, profile.vertexSpacing));
            var resolution = Mathf.Max(1, profile.crossResolution);

            // Fetch seam data (positions calculated during Junction generation)
            var startSeam = _seamPositionMap[splineIndex].StartSeam;
            var endSeam = _seamPositionMap[splineIndex].EndSeam;

            var prevRowIndices = new List<int>();

            for (var i = 0; i <= segments; i++)
            {
                var tRaw = i / (float)segments;
                var t = Mathf.Lerp(tStart, tEnd, tRaw);
                var currentWidth = GetNaturalWidth(t, splineLength, profile.width);

                var currentRowIndices = new List<int>();

                // Check if we are at a welded end
                var isStartWeld = i == 0 && startSeam.Positions.Count == resolution + 1;
                var isEndWeld = i == segments && endSeam.Positions.Count == resolution + 1;

                if (isStartWeld)
                {
                    // Use exact positions from the junction to avoid gaps
                    for (var x = 0; x < startSeam.Positions.Count; x++)
                    {
                        data.verts.Add(startSeam.Positions[x]);

                        var u = x / (float)resolution;
                        data.colors.Add(CalculateVertexColor(currentWidth, u));
                        currentRowIndices.Add(data.verts.Count - 1);
                    }
                }
                else if (isEndWeld)
                {
                    for (var x = 0; x < endSeam.Positions.Count; x++)
                    {
                        data.verts.Add(endSeam.Positions[x]);

                        var u = x / (float)resolution;
                        data.colors.Add(CalculateVertexColor(currentWidth, u));
                        currentRowIndices.Add(data.verts.Count - 1);
                    }
                }
                else
                {
                    // Standard Procedural Generation
                    spline.Evaluate(t, out var rawPos, out var rawTangent, out var rawUp);
                    var splinePos = (Vector3)rawPos;
                    var forward = ((Vector3)rawTangent).normalized;
                    if (forward == Vector3.zero) forward = Vector3.forward;
                    var localUp = useSplineTwist ? ((Vector3)rawUp).normalized : Vector3.up;
                    var right = Vector3.Cross(localUp, forward).normalized;

                    for (var x = 0; x <= resolution; x++)
                    {
                        var u = x / (float)resolution;
                        var offsetMultiplier = u - 0.5f;
                        var posRaw = splinePos + right * (offsetMultiplier * currentWidth);
                        var posSnapped = SnapToGround(posRaw);

                        data.verts.Add(posSnapped);
                        data.colors.Add(CalculateVertexColor(currentWidth, u));
                        currentRowIndices.Add(data.verts.Count - 1);
                    }
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

        private void GenerateJunctionGeometry(Node node, MeshData data, Dictionary<int, Vector2> trims)
        {
            node.Connections.Sort((a, b) =>
            {
                var angleA = Mathf.Atan2(a.Tangent.x, a.Tangent.z);
                var angleB = Mathf.Atan2(b.Tangent.x, b.Tangent.z);
                return angleA.CompareTo(angleB);
            });

            var centerPos = SnapToGround(node.Position);

            var rightEdges = new List<JunctionEdge>();
            var leftEdges = new List<JunctionEdge>();

            // 1. Generate Arms & Capture Seam Data
            foreach (var conn in node.Connections)
            {
                var profile = roadProfiles[conn.SplineIndex];
                var spline = splineContainer.Splines[conn.SplineIndex];
                var splineLen = spline.GetLength();

                var effectiveWidth = profile.width * (intersectionBulge ? bulgeMultiplier : 1.0f);
                var retractMeters = effectiveWidth * 0.5f * profile.junctionRetractMultiplier;
                var tOffset = splineLen > 0.001f ? Mathf.Clamp01(retractMeters / splineLen) : 0;

                // Update trims
                if (conn.IsStart)
                {
                    var current = trims[conn.SplineIndex];
                    trims[conn.SplineIndex] = new Vector2(Mathf.Max(current.x, tOffset), current.y);
                }
                else
                {
                    var current = trims[conn.SplineIndex];
                    trims[conn.SplineIndex] = new Vector2(current.x, Mathf.Min(current.y, 1f - tOffset));
                }

                // Calculation logic
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
                var seamPositions = new List<Vector3>(); // Store seam positions here

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

                        // If this is the outer edge (s==steps), store the positions for the road to match
                        if (s == steps) seamPositions.Add(posSnapped);
                    }

                    if (s > 0)
                        for (var x = 0; x < crossRes; x++)
                        {
                            var c = currentRowIndices[x + 1];
                            var d = currentRowIndices[x];
                            var b = prevRowIndices[x];
                            var a = prevRowIndices[x + 1];
                            // Triangulation order based on direction (visuals)
                            if (conn.IsStart)
                            {
                                data.tris.Add(a);
                                data.tris.Add(b);
                                data.tris.Add(d);
                                data.tris.Add(a);
                                data.tris.Add(d);
                                data.tris.Add(c);
                            }
                            else
                            {
                                data.tris.Add(a);
                                data.tris.Add(d);
                                data.tris.Add(b);
                                data.tris.Add(a);
                                data.tris.Add(c);
                                data.tris.Add(d);
                            }
                        }

                    prevRowIndices = currentRowIndices;
                }

                // Store Seam Data
                var sd = new SeamData { Positions = seamPositions, Tangent = outDirection };
                if (conn.IsStart) _seamPositionMap[conn.SplineIndex] = (sd, _seamPositionMap[conn.SplineIndex].EndSeam);
                else _seamPositionMap[conn.SplineIndex] = (_seamPositionMap[conn.SplineIndex].StartSeam, sd);

                var finalLeft = data.verts[prevRowIndices[0]];
                var finalRight = data.verts[prevRowIndices[crossRes]];

                if (conn.IsStart)
                {
                    leftEdges.Add(new JunctionEdge
                        { position = finalLeft, tangentDir = outDirection, indices = armLeftIndices });
                    rightEdges.Add(new JunctionEdge
                        { position = finalRight, tangentDir = outDirection, indices = armRightIndices });
                }
                else
                {
                    leftEdges.Add(new JunctionEdge
                        { position = finalRight, tangentDir = outDirection, indices = armRightIndices });
                    rightEdges.Add(new JunctionEdge
                        { position = finalLeft, tangentDir = outDirection, indices = armLeftIndices });
                }
            }

            // 2. Generate Corners (Same logic as before, just filling local data)
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

                        if (isStartWeld && r < startEdge.indices.Count) currentIdx = startEdge.indices[r];
                        else if (isEndWeld && r < endEdge.indices.Count) currentIdx = endEdge.indices[r];

                        if (currentIdx == -1)
                        {
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

                        if (s > 0 && r > 0)
                        {
                            var c = currentIdx;
                            var d = currentColumnIndices[r - 1];
                            var b = prevColumnIndices[r];
                            var a = prevColumnIndices[r - 1];
                            data.tris.Add(a);
                            data.tris.Add(b);
                            data.tris.Add(c);
                            data.tris.Add(a);
                            data.tris.Add(c);
                            data.tris.Add(d);
                        }
                    }

                    prevColumnIndices = currentColumnIndices;
                }
            }
        }

        // --- Utilities ---
        // (Copy paste your original utilities: GetNaturalWidth, CalculateVertexColor, SyncProfiles, 
        // ApplyTerrainSplatting, SnapToGround, BuildNodeGraph, AddConnectionToGraph, CalculateCubicBezierPoint)

        // ... PASTE YOUR EXISTING UTILITIES HERE ...
        // For brevity in the answer, I assume these are unchanged from your original file.
        // If you need me to paste them back in, let me know, but they are identical.

        // [Existing Utility Implementations required for the code to compile]
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

        // Data classes for mesh storage
        private class MeshData
        {
            public readonly List<Color> colors = new();
            public readonly List<int> tris = new();
            public readonly List<Vector3> verts = new();
        }

        // Helper struct to track seam positions for lining up separate meshes
        private class SeamData
        {
            public List<Vector3> Positions = new();
            public Vector3 Tangent;
        }

        // --- Data Classes ---

        [Serializable]
        public class RoadProfile
        {
            public string name = "Road Settings";
            [Min(0.1f)] public float width = 3.0f;
            [Range(0.1f, 10f)] public float vertexSpacing = 1.0f;
            [Range(1, 8)] public int crossResolution = 4;
            [Range(0.5f, 12f)] public float junctionRetractMultiplier = 2.5f;
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
            public List<int> indices;
        }
    }
}