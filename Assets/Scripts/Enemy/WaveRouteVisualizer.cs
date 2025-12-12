using System.Collections.Generic;
using Pathing;
using UnityEngine;
using UnityEngine.Splines;
// Namespace de ton RoadNetworkGenerator

// Namespace de WaveManager et AirPath

namespace Enemy
{
    public class WaveRouteVisualizer : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Laisser vide si WaveManager a un Singleton, sinon glisser la référence.")]
        public WaveManager waveManager;
        [Tooltip("Nécessaire pour accéder aux Splines des routes.")]
        public RoadNetworkGenerator roadGenerator;

        [Header("Visual Settings")]
        public float lineWidth = 1.0f;
        [Tooltip("Hauteur ajoutée aux lignes pour qu'elles ne traversent pas le sol.")]
        public float heightOffset = 1.0f;

        [Header("Ground Routes")]
        public Material groundLineMaterial;
        public Color groundColor = Color.yellow;
        [Range(0.01f, 1f)] public float splineResolution = 0.05f; // Plus petit = plus lisse

        [Header("Air Routes")]
        public Material airLineMaterial;
        public Color airColor = Color.cyan;

        // Stocke les objets LineRenderer créés pour pouvoir les supprimer
        private List<GameObject> _activeLines = new List<GameObject>();

        private void Start()
        {
            // Récupération automatique des dépendances si non assignées
            if (waveManager == null) waveManager = WaveManager.Instance;
            if (roadGenerator == null) roadGenerator = FindAnyObjectByType<RoadNetworkGenerator>();

            if (waveManager != null)
            {
                // S'abonner aux événements
                waveManager.OnWaveFinished += ShowNextRoutes;
                waveManager.OnWaveStarted += HideRoutes;
                waveManager.OnAllWavesCompleted += HideRoutesNoArg; // Nettoyage final

                // Si le jeu commence et qu'aucune vague n'est active, on montre la première
                if (!waveManager.IsWaveActive)
                {
                    ShowNextRoutes();
                }
            }
        }

        private void OnDestroy()
        {
            if (waveManager != null)
            {
                waveManager.OnWaveFinished -= ShowNextRoutes;
                waveManager.OnWaveStarted -= HideRoutes;
                waveManager.OnAllWavesCompleted -= HideRoutesNoArg;
            }
        }

        // Wrapper pour l'événement qui n'a pas d'arguments
        private void HideRoutesNoArg() => HideRoutes(0, "");

        /// <summary>
        /// Cache toutes les lignes actives.
        /// </summary>
        private void HideRoutes(int waveIndex, string waveName)
        {
            foreach (var line in _activeLines)
            {
                if (line) Destroy(line);
            }
            _activeLines.Clear();
        }

        /// <summary>
        /// Calcule et affiche les routes de la PROCHAINE vague.
        /// </summary>
        private void ShowNextRoutes()
        {
            HideRoutes(0, ""); // Nettoyage de sécurité

            int nextWaveIndex = waveManager.CurrentWaveIndex + 1;

            // Vérifie si la prochaine vague existe
            if (nextWaveIndex >= waveManager.waves.Count) return;

            var waveProfile = waveManager.waves[nextWaveIndex];
        
            Debug.Log($"[Visualizer] Visualisation des routes pour la vague : {waveProfile.waveName}");

            DrawGroundRoutes(waveProfile);
            DrawAirRoutes(waveProfile);
        }

        // --- Logique Routes Terrestres (Splines) ---
        private void DrawGroundRoutes(WaveProfile wave)
        {
            if (!roadGenerator || roadGenerator.GetComponent<SplineContainer>() == null) return;

            var container = roadGenerator.GetComponent<SplineContainer>();

            // Parcourt les IDs de routes débloquées pour cette vague
            foreach (int roadIndex in wave.unlockedRoadIndices)
            {
                if (roadIndex < 0 || roadIndex >= container.Splines.Count) continue;

                Spline spline = container.Splines[roadIndex];
            
                // Création de l'objet Ligne
                GameObject lineObj = CreateLineObject($"GroundRoute_{roadIndex}", groundColor, groundLineMaterial);
                LineRenderer lr = lineObj.GetComponent<LineRenderer>();

                // Échantillonnage de la spline pour créer les points de la ligne
                List<Vector3> points = new List<Vector3>();
                for (float t = 0; t <= 1.0001f; t += splineResolution)
                {
                    // Convertir position spline (locale) -> Monde
                    Vector3 worldPos = container.transform.TransformPoint(spline.EvaluatePosition(t));
                    // Ajouter l'offset hauteur
                    worldPos += Vector3.up * heightOffset;
                    points.Add(worldPos);
                }

                lr.positionCount = points.Count;
                lr.SetPositions(points.ToArray());
            }
        }

        // --- Logique Routes Aériennes (Waypoints) ---
        private void DrawAirRoutes(WaveProfile wave)
        {
            if (wave.airSegments == null) return;

            // Parcourt chaque segment aérien configuré
            foreach (var segment in wave.airSegments)
            {
                if (segment.targetPath == null) continue;

                AirPath path = segment.targetPath;
            
                // Création de l'objet Ligne
                GameObject lineObj = CreateLineObject($"AirRoute_{path.name}", airColor, airLineMaterial);
                LineRenderer lr = lineObj.GetComponent<LineRenderer>();

                List<Vector3> points = new List<Vector3>();

                // 1. Point de départ
                points.Add(path.transform.position);

                // 2. Waypoints intermédiaires
                foreach (Transform t in path.waypoints)
                {
                    if (t != null) points.Add(t.position);
                }

                // 3. Objectif final (Logique identique à ton script AirPath)
                // Priorité : Override Vague > Objectif Local > Objectif Base
                Vector3? finalTarget = null;

                if (segment.specificTarget) 
                    finalTarget = segment.specificTarget.transform.position;
                else if (path.localObjective) 
                    finalTarget = path.localObjective.transform.position;
                else if (path.mainBaseObjective) 
                    finalTarget = path.mainBaseObjective.transform.position;

                if (finalTarget.HasValue)
                {
                    points.Add(finalTarget.Value);
                }

                lr.positionCount = points.Count;
                lr.SetPositions(points.ToArray());
            }
        }

        // --- Utilitaire ---
        private GameObject CreateLineObject(string name, Color color, Material mat)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(this.transform);
        
            LineRenderer lr = obj.AddComponent<LineRenderer>();
        
            // Configuration visuelle basique
            lr.useWorldSpace = true;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.startColor = color;
            lr.endColor = color;
        
            // Assignation d'un matériel par défaut si aucun n'est fourni
            if (mat != null)
                lr.material = mat;
            else
                lr.material = new Material(Shader.Find("Sprites/Default")); // Shader simple et visible

            _activeLines.Add(obj);
            return obj;
        }
    }
}