using Enemy;
using Placement;
using UnityEngine;

namespace Pathing.Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Objectives Defaults")] public DestructibleObjective localObjective;

        public DestructibleObjective mainBaseObjective;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 1f);

            // Visualisation des liens dans l'éditeur
            if (localObjective != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, localObjective.transform.position);
            }

            if (mainBaseObjective != null)
            {
                Gizmos.color = Color.magenta;
                // Juste une petite ligne pour montrer la direction du backup
                var dir = (mainBaseObjective.transform.position - transform.position).normalized;
                Gizmos.DrawLine(transform.position, transform.position + dir * 5f);
            }
        }

        // Modification de la signature : ajout de 'targetOverride'
        public void Spawn(GameObject prefab, DestructibleObjective targetOverride = null)
        {
            if (!prefab) return;

            var newEnemy = Instantiate(prefab, transform.position, transform.rotation);

            var tracker = newEnemy.GetComponent<EnemyObjectiveTracker>();
            if (tracker != null)
            {
                // Logique de priorité : Override > Default
                var targetToUse = targetOverride != null ? targetOverride : localObjective;

                tracker.Initialize(targetToUse, mainBaseObjective);
            }
            else
            {
                Debug.LogWarning($"[EnemySpawner] {newEnemy.name} n'a pas de tracker !");
            }
        }

        public GameObject SpawnAndReturn(GameObject prefab, DestructibleObjective targetOverride = null)
        {
            if (!prefab) return null;

            var newEnemy = Instantiate(prefab, transform.position, transform.rotation);

            var tracker = newEnemy.GetComponent<EnemyObjectiveTracker>();
            if (tracker != null)
            {
                var targetToUse = targetOverride != null ? targetOverride : localObjective;
                tracker.Initialize(targetToUse, mainBaseObjective);
            }

            return newEnemy; // Retourne l'objet pour que le Manager puisse y ajouter des events
        }
    }
}