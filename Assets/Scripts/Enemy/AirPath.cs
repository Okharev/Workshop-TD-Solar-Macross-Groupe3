using System.Collections.Generic;
using UnityEngine;
using Pathing.Gameplay;
using Placement;

namespace Enemy
{
    public class AirPath : MonoBehaviour
    {
        [Header("Configuration")]
        public string pathID;
        public Color gizmoColor = Color.cyan;

        [Header("Mission Targets")]
        public DestructibleObjective localObjective; 
        public DestructibleObjective mainBaseObjective;

        [Header("Trajectory Points")]
        public List<Transform> waypoints = new List<Transform>();

        // --- La méthode Spawn (comme EnemySpawner) ---
        public void Spawn(GameObject prefab, DestructibleObjective targetOverride = null)
        {
            if (prefab == null) return;

            // 1. Positionnement
            Quaternion spawnRotation = transform.rotation;
            if (waypoints.Count > 0 && waypoints[0] != null)
            {
                spawnRotation = Quaternion.LookRotation(waypoints[0].position - transform.position);
            }

            GameObject newAirUnit = Instantiate(prefab, transform.position, spawnRotation);

            // 2. Initialiser le mouvement
            var boidAI = newAirUnit.GetComponent<FighterJetAi>();
            if (boidAI)
            {
                boidAI.Initialize(waypoints); 
            }

            // 3. Initialiser les objectifs avec l'OVERRIDE
            var tracker = newAirUnit.GetComponent<EnemyObjectiveTracker>();
            if (tracker)
            {
                // Logique de priorité
                DestructibleObjective targetToUse = (targetOverride != null) ? targetOverride : localObjective;
                
                tracker.Initialize(targetToUse, mainBaseObjective);
            }
        }

        [ContextMenu("Auto-Fill from Children")]
        public void AutoFillChildren()
        {
            waypoints.Clear();
            foreach (Transform child in transform)
            {
                waypoints.Add(child);
            }
        }

        private void OnDrawGizmos()
        {
            // Dessin du point de spawn (Root)
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 1f); 
            
            // Ligne du Spawn -> 1er Waypoint
            if (waypoints.Count > 0 && waypoints[0] != null)
            {
                Gizmos.DrawLine(transform.position, waypoints[0].position);
            }

            // Ligne du dernier Waypoint -> Objectif
            if (waypoints.Count > 0 && localObjective != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(waypoints[^1].position, localObjective.transform.position);
            }

            // Chemin entre les waypoints
            if (waypoints == null || waypoints.Count == 0) return;
            Gizmos.color = gizmoColor;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i+1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i+1].position);
                    Gizmos.DrawSphere(waypoints[i].position, 0.5f);
                }
            }
            if (waypoints.Count > 0 && waypoints[^1] != null) Gizmos.DrawSphere(waypoints[^1].position, 0.5f);
        }
    }
}