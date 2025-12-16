#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(ComplexTreeGenerator))]
    public class TreeMeshSaverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ComplexTreeGenerator generator = (ComplexTreeGenerator)target;

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Export Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("💾 Save Mesh to Assets", GUILayout.Height(40)))
            {
                SaveMesh(generator);
            }
        }

        void SaveMesh(ComplexTreeGenerator generator)
        {
            // --- CORRECTION ICI ---
            // Le Mesh est sur l'enfant "GenshinComplexTree", pas sur le générateur lui-même.
            // On utilise GetComponentInChildren pour aller le chercher.
            MeshFilter mf = generator.GetComponentInChildren<MeshFilter>();
        
            if (mf == null || mf.sharedMesh == null)
            {
                EditorUtility.DisplayDialog("Erreur", "Aucun Mesh trouvé dans les enfants ! Avez-vous cliqué sur 'Generate Tree' ?", "OK");
                return;
            }

            Mesh meshToSave = mf.sharedMesh;

            // Ouvrir la fenêtre de sauvegarde
            string defaultName = "GenshinTree_New";
            string path = EditorUtility.SaveFilePanel("Save Tree Mesh", "Assets/", defaultName, "asset");

            if (string.IsNullOrEmpty(path)) return;

            // Conversion chemin absolu -> relatif
            path = FileUtil.GetProjectRelativePath(path);

            // Copie du mesh pour créer l'asset
            Mesh meshAsset = Instantiate(meshToSave);
            meshAsset.name = Path.GetFileNameWithoutExtension(path);

            // Sauvegarde physique
            AssetDatabase.CreateAsset(meshAsset, path);
            AssetDatabase.SaveAssets();

            // Reconnexion : On dit à l'enfant d'utiliser le fichier sur le disque
            mf.sharedMesh = meshAsset;

            Debug.Log($"✅ Mesh sauvegardé : {path}");
            EditorGUIUtility.PingObject(meshAsset);
        }
    }
}
#endif