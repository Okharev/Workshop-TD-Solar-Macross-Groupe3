using UnityEditor;
using UnityEngine;

namespace Placement
{
    [CustomEditor(typeof(PlacementTester))]
    public class PlacementTesterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlacementTester tester = (PlacementTester)target;

            GUILayout.Space(10);
            GUILayout.Label("Quick Actions", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Test Tower 1")) tester.SelectTower(0);
                if (GUILayout.Button("Test Tower 2")) tester.SelectTower(1);
                if (GUILayout.Button("Test Tower 3")) tester.SelectTower(2);
                GUILayout.EndHorizontal();

                GUILayout.Space(5);
                if (GUILayout.Button("Stop Placement", GUILayout.Height(30)))
                {
                    var manager = tester.GetComponentInChildren<PlacementManager>(); 

                    Debug.Log("Use Backspace or Right Click in Game View to stop.");
                }
            }
            else
            {
                GUILayout.Box("Enter Play Mode to use these buttons.");
            }
        }
    }
}