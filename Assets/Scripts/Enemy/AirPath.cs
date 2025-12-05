using System.Collections.Generic;
using Placement;
using UnityEngine;

namespace Enemy
{
    public class AirPath : MonoBehaviour
    {
        [Header("Configuration")] public string pathID;

        public Color gizmoColor = Color.cyan;

        [Header("Mission Targets")] public DestructibleObjective localObjective;

        public DestructibleObjective mainBaseObjective;

        [Header("Trajectory Points")] public List<Transform> waypoints = new();

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 1f);

            if (waypoints.Count > 0 && waypoints[0] != null) Gizmos.DrawLine(transform.position, waypoints[0].position);

            if (waypoints.Count > 0 && localObjective != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(waypoints[^1].position, localObjective.transform.position);
            }

            if (waypoints == null || waypoints.Count == 0) return;
            Gizmos.color = gizmoColor;
            for (var i = 0; i < waypoints.Count - 1; i++)
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                    Gizmos.DrawSphere(waypoints[i].position, 0.5f);
                }

            if (waypoints.Count > 0 && waypoints[^1] != null) Gizmos.DrawSphere(waypoints[^1].position, 0.5f);
        }

        public void Spawn(GameObject prefab, DestructibleObjective targetOverride = null)
        {
            if (prefab == null) return;

            var spawnRotation = transform.rotation;
            if (waypoints.Count > 0 && waypoints[0] != null)
                spawnRotation = Quaternion.LookRotation(waypoints[0].position - transform.position);

            var newAirUnit = Instantiate(prefab, transform.position, spawnRotation);

            var boidAI = newAirUnit.GetComponent<FighterJetAi>();
            if (boidAI) boidAI.Initialize(waypoints);

            var tracker = newAirUnit.GetComponent<EnemyObjectiveTracker>();
            if (tracker)
            {
                // Logique de priorité
                var targetToUse = targetOverride != null ? targetOverride : localObjective;

                tracker.Initialize(targetToUse, mainBaseObjective);
            }
        }

        [ContextMenu("Auto-Fill from Children")]
        public void AutoFillChildren()
        {
            waypoints.Clear();
            foreach (Transform child in transform) waypoints.Add(child);
        }
    }
}