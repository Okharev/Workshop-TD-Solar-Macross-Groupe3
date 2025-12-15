using UnityEngine;
using UnityEditor;
using System.IO;

public class ToonRampGenerator : EditorWindow
{
    // Le dégradé que vous pourrez éditer visuellement
    public Gradient toonGradient;
    public int width = 256;
    public int height = 16;
    public string fileName = "GenshinRamp";

    [MenuItem("Tools/Generate Toon Ramp")]
    public static void ShowWindow()
    {
        GetWindow<ToonRampGenerator>("Toon Ramp Gen");
    }

    private void OnEnable()
    {
        // Initialisation d'un dégradé par défaut (Style Genshin)
        if (toonGradient == null)
        {
            toonGradient = new Gradient();

            // Clés de couleur (Color Keys)
            var colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 0.0f), // Ombre (pas noir pur pour garder de la teinte)
                new GradientColorKey(Color.white, 0.5f),                 // Transition vers la lumière
                new GradientColorKey(Color.white, 1.0f)                  // Pleine lumière
            };

            // Clés d'alpha (toujours 1)
            var alphaKeys = new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(1.0f, 1.0f)
            };

            toonGradient.SetKeys(colorKeys, alphaKeys);
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Configuration du Ramp Lighting", EditorStyles.boldLabel);

        // Champ pour éditer le dégradé
        toonGradient = EditorGUILayout.GradientField("Gradient Lumière", toonGradient);
        
        // Options de taille
        width = EditorGUILayout.IntField("Largeur", width);
        height = EditorGUILayout.IntField("Hauteur", height);
        fileName = EditorGUILayout.TextField("Nom du Fichier", fileName);

        GUILayout.Space(20);

        if (GUILayout.Button("Générer la Texture"))
        {
            BakeTexture();
        }
    }

    private void BakeTexture()
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        
        // On parcourt chaque pixel horizontalement
        for (int x = 0; x < width; x++)
        {
            // On évalue la couleur du gradient à cette position (t)
            float t = (float)x / (width - 1);
            Color col = toonGradient.Evaluate(t);

            // On remplit verticalement (la couleur est la même de bas en haut)
            for (int y = 0; y < height; y++)
            {
                texture.SetPixel(x, y, col);
            }
        }

        texture.Apply();

        // Sauvegarde du fichier
        byte[] bytes = texture.EncodeToPNG();
        string path = Application.dataPath + "/Textures/"; // Assurez-vous que ce dossier existe ou changez le chemin
        
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        
        string fullPath = path + fileName + ".png";
        File.WriteAllBytes(fullPath, bytes);

        AssetDatabase.Refresh();
        
        // Configuration automatique de l'import (Important pour le style Toon)
        string assetPath = "Assets/Textures/" + fileName + ".png";
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Clamp; // CRUCIAL : Évite que la lumière boucle du blanc au noir
            importer.filterMode = FilterMode.Bilinear; // Bilinear pour doux, Point pour dur
            importer.SaveAndReimport();
        }

        Debug.Log("Texture Ramp générée : " + assetPath);
    }
}