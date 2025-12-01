using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

// Namespace of your generator

namespace Pathing
{
    [CustomEditor(typeof(RoadNetworkGenerator))]
    public class RoadNetworkDebugEditor : Editor
    {
        // Settings for the debug view
        private static bool showIds = true;
        private static float labelSize = 1.0f;
        private static Color labelColor = Color.cyan;
        private static Color connectionColor = Color.yellow;

        public override void OnInspectorGUI()
        {
            // Draw the default inspector (so you don't lose your existing fields)
            DrawDefaultInspector();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);

            // Toggle for Scene View Labels
            EditorGUI.BeginChangeCheck();
            showIds = EditorGUILayout.Toggle("Show Road IDs", showIds);
            
            if (showIds)
            {
                labelSize = EditorGUILayout.Slider("Label Scale", labelSize, 0.5f, 3.0f);
                labelColor = EditorGUILayout.ColorField("Label Color", labelColor);
            }

            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Force Regenerate (Safe)"))
            {
                var script = (RoadNetworkGenerator)target;
                script.Generate();
            }
        }

        private void OnSceneGUI()
        {
            if (!showIds) return;

            var generator = (RoadNetworkGenerator)target;
            var container = generator.GetComponent<SplineContainer>();

            if (container == null) return;

            // Setup Style
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = labelColor;
            style.fontSize = Mathf.RoundToInt(12 * labelSize);
            style.alignment = TextAnchor.MiddleCenter;

            // Iterate all splines to draw IDs
            for (int i = 0; i < container.Splines.Count; i++)
            {
                var spline = container.Splines[i];
                if (spline.Knots.Count() < 2) continue;

                // 1. Get the middle point of the spline
                float midT = 0.5f;
                Vector3 worldPos = generator.transform.TransformPoint(spline.EvaluatePosition(midT));
                
                // Lift it up slightly so it floats over the road mesh
                worldPos += Vector3.up * 2.0f;

                // 2. Draw the Label
                Handles.Label(worldPos, $"Road ID: {i}", style);

                // 3. Draw a small dot or sphere to anchor it visually
                Handles.color = labelColor;
                Handles.SphereHandleCap(0, worldPos - Vector3.up * 0.5f, Quaternion.identity, 0.5f, EventType.Repaint);

                // Optional: Draw Direction Arrow to know which way is Forward
                Vector3 forward = Vector3.Normalize(generator.transform.TransformDirection(spline.EvaluateTangent(midT)));
                Handles.color = connectionColor;
                Handles.ArrowHandleCap(0, worldPos, Quaternion.LookRotation(forward), 2.0f, EventType.Repaint);
            }
        }
    }
}