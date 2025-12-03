using Enemy;
using Placement;
using UnityEngine;

namespace Pathing.Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {

        [Header("Objectives Defaults")]
        public DestructibleObjective localObjective; 
        public DestructibleObjective mainBaseObjective;

        // Modification de la signature : ajout de 'targetOverride'
        public void Spawn(GameObject prefab, DestructibleObjective targetOverride = null)
        {
            if (!prefab) return;
            
            GameObject newEnemy = Instantiate(prefab, transform.position, transform.rotation);
            
            var tracker = newEnemy.GetComponent<EnemyObjectiveTracker>();
            if (tracker != null)
            {
                // Logique de priorité : Override > Default
                DestructibleObjective targetToUse = (targetOverride != null) ? targetOverride : localObjective;

                tracker.Initialize(targetToUse, mainBaseObjective);
            }
            else
            {
                Debug.LogWarning($"[EnemySpawner] {newEnemy.name} n'a pas de tracker !");
            }
        }

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
                Vector3 dir = (mainBaseObjective.transform.position - transform.position).normalized;
                Gizmos.DrawLine(transform.position, transform.position + dir * 5f);
            }
        }
    }
}