using UnityEngine;
using UnityEditor;
using Pathing.Gameplay;
using System.Collections.Generic;
using Enemy;
using UnityEngine.Splines;

namespace Pathing.EditorTools
{
    [CustomEditor(typeof(WaveManager))]
    public class WaveManagerEditor : Editor
    {
        private WaveManager _target;
        
        // Static to keep selection between reloads
        private static int _selectedWaveIndex = 0;
        
        // Visual settings for Scene View handles
        private float _handleHeight = 4.0f;     
        private float _interactionDistance = 50f; 

        private void OnEnable()
        {
            _target = (WaveManager)target;
            SceneView.duringSceneGui += OnSceneGUIInternal;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUIInternal;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Wave Designer", EditorStyles.boldLabel);

            // Handle null lists
            if (_target.waves == null) _target.waves = new List<WaveProfile>();
            
            if (_target.waves.Count > 0)
            {
                // Safety check for index out of bounds
                if (_selectedWaveIndex >= _target.waves.Count) _selectedWaveIndex = 0;

                string[] waveNames = new string[_target.waves.Count];
                for (int i = 0; i < _target.waves.Count; i++)
                {
                    string n = _target.waves[i].waveName;
                    waveNames[i] = string.IsNullOrEmpty(n) ? $"Wave {i + 1}" : n;
                }

                int columns = 4; 
                GUILayout.BeginVertical("box");
                
                // Monitor tab changes to force repaint
                EditorGUI.BeginChangeCheck();
                _selectedWaveIndex = GUILayout.SelectionGrid(_selectedWaveIndex, waveNames, columns);
                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                }
                
                GUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("Create a wave in the default inspector below to start.", MessageType.Info);
            }

            // Draw Editor for Selected Wave
            if (_target.waves.Count > 0 && _selectedWaveIndex < _target.waves.Count)
            {
                DrawDropZone(_target.waves[_selectedWaveIndex]);
                DrawSelectedWaveSummary(_target.waves[_selectedWaveIndex]);
                
                GUILayout.Space(5);
                EditorGUILayout.HelpBox("TIP: Click the red/green spheres in the SCENE VIEW to toggle roads for this wave.", MessageType.Info);
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Default Inspector", EditorStyles.boldLabel);
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }

        // --- SCENE VIEW LOGIC (Interactive Roads) ---
        private void OnSceneGUIInternal(SceneView sceneView)
        {
            if (_target == null || _target.waves == null || _target.waves.Count == 0) return;
            if (_selectedWaveIndex >= _target.waves.Count) return;

            WaveProfile currentWave = _target.waves[_selectedWaveIndex];
            if (currentWave.unlockedRoadIndices == null) currentWave.unlockedRoadIndices = new List<int>();

            if (_target.roadGenerator == null || _target.roadGenerator.splineContainer == null) return;

            var container = _target.roadGenerator.splineContainer;
            var generatorTransform = _target.roadGenerator.transform;
            UnityEngine.Camera cam = SceneView.currentDrawingSceneView.camera;
            if (cam == null) return;

            for (int i = 0; i < container.Splines.Count; i++)
            {
                Spline spline = container.Splines[i];
                // if (spline.Knots == null) continue; // Some versions of Spline package don't need this check or use .Count

                Vector3 localPos = (Vector3)spline.EvaluatePosition(0.5f);
                Vector3 worldPos = generatorTransform.TransformPoint(localPos);
                Vector3 handlePos = worldPos + Vector3.up * _handleHeight;

                bool isUnlocked = currentWave.unlockedRoadIndices.Contains(i);

                // Dynamic Color
                Handles.color = isUnlocked ? Color.green : Color.red;
                
                float distToCam = Vector3.Distance(cam.transform.position, handlePos);
                float size = HandleUtility.GetHandleSize(handlePos) * 0.5f;

                // Interactive Button
                if (Handles.Button(handlePos, Quaternion.identity, size, size * 1.2f, Handles.SphereHandleCap))
                {
                    Undo.RecordObject(_target, "Toggle Road Lock");
                    
                    if (isUnlocked)
                    {
                        currentWave.unlockedRoadIndices.Remove(i);
                        Debug.Log($"⛔ Road {i} BLOCKED for {currentWave.waveName}");
                    }
                    else
                    {
                        currentWave.unlockedRoadIndices.Add(i);
                        Debug.Log($"✅ Road {i} UNLOCKED for {currentWave.waveName}");
                    }
                    
                    EditorUtility.SetDirty(_target);
                }

                // Labels (Only if close)
                if (distToCam < _interactionDistance)
                {
                    GUIStyle style = new GUIStyle();
                    style.normal.textColor = isUnlocked ? Color.green : new Color(1f, 0.5f, 0.5f);
                    style.fontStyle = FontStyle.Bold;
                    style.fontSize = 12;
                    style.alignment = TextAnchor.MiddleCenter;

                    string status = isUnlocked ? "OPEN" : "LOCKED";
                    Handles.Label(handlePos + Vector3.up * 1f, $"Road {i}\n{status}", style);
                    
                    Handles.color = isUnlocked ? new Color(0,1,0,0.5f) : new Color(1,0,0,0.5f);
                    Handles.DrawLine(worldPos, handlePos);
                }

                // Green Highlight on Road
                if (isUnlocked)
                {
                    DrawPathHighlight(spline, generatorTransform);
                }
            }
        }

        private void DrawPathHighlight(Spline spline, Transform t)
        {
            Handles.color = new Color(0, 1, 0, 0.4f);
            Vector3 prev = t.TransformPoint(spline.EvaluatePosition(0f));
            for(float s=0.1f; s<=1.0f; s+=0.1f)
            {
                Vector3 next = t.TransformPoint(spline.EvaluatePosition(s));
                Handles.DrawLine(prev, next, 5f); 
                prev = next;
            }
        }

        // --- DROP ZONE (Updated for References) ---
        private void DrawDropZone(WaveProfile wave)
        {
            GUILayout.Space(10);
            var dropRect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "DRAG & DROP ZONE\n(Drag Spawners or AirPaths here)", EditorStyles.helpBox);

            var evt = Event.current;
            if (dropRect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
                else if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is GameObject go) HandleDroppedObject(go, wave);
                    }
                    evt.Use();
                }
            }
        }

        private void HandleDroppedObject(GameObject go, WaveProfile wave)
        {
            Undo.RecordObject(_target, "Modified Wave Profile");
            
            // 1. Check for GROUND SPAWNER
            var spawner = go.GetComponent<EnemySpawner>();
            if (spawner != null)
            {
                if (wave.groundSegments == null) wave.groundSegments = new List<GroundWaveSegment>();
                
                wave.groundSegments.Add(new GroundWaveSegment
                {
                    targetSpawner = spawner, // <--- DIRECT REFERENCE
                    count = 5, 
                    spawnInterval = 1f
                });
                
                Debug.Log($"[+] Ground Segment added for: {spawner.name}");
                EditorUtility.SetDirty(_target);
                return;
            }

            // 2. Check for AIR PATH
            var airPath = go.GetComponent<AirPath>();
            if (airPath != null)
            {
                if (wave.airSegments == null) wave.airSegments = new List<AirWaveSegment>();
                
                wave.airSegments.Add(new AirWaveSegment
                {
                    targetPath = airPath, // <--- DIRECT REFERENCE
                    count = 3, 
                    spawnInterval = 2f
                });
                
                Debug.Log($"[+] Air Segment added for: {airPath.name}");
                EditorUtility.SetDirty(_target);
            }
        }

        private void DrawSelectedWaveSummary(WaveProfile wave)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Editing: {wave.waveName}", EditorStyles.boldLabel);
            int roadCount = wave.unlockedRoadIndices?.Count ?? 0;
            int groundCount = wave.groundSegments?.Count ?? 0;
            int airCount = wave.airSegments?.Count ?? 0;
            EditorGUILayout.HelpBox($"🔓 Roads: {roadCount} | 🚜 Ground: {groundCount} | ✈️ Air: {airCount}", MessageType.None);
            GUILayout.EndVertical();
        }
    }
}