using Placement;
using UnityEngine;

namespace Pathing.Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {

        [Header("Objectives")]
        // Changement de GameObject -> DestructibleObjective pour accéder à la logique de santé
        [Tooltip("L'objectif spécifique à cette 'Lane' (ex: Nexus du Nord)")]
        public DestructibleObjective localObjective; 

        [Tooltip("L'objectif final si le local est détruit (ex: Base Principale)")]
        public DestructibleObjective mainBaseObjective;

        public void Spawn(GameObject prefab)
        {
            if (!prefab) return;
            
            // Instanciation
            GameObject newEnemy = Instantiate(prefab, transform.position, transform.rotation);

            // --- INJECTION DES DÉPENDANCES ---
            // On cherche le tracker sur l'ennemi fraîchement créé
            var tracker = newEnemy.GetComponent<EnemyObjectiveTracker>();
            if (tracker != null)
            {
                // On lui donne ses ordres : Attaque le Local, sinon le Main
                tracker.Initialize(localObjective, mainBaseObjective);
            }
            else
            {
                Debug.LogWarning($"[EnemySpawner] L'ennemi {newEnemy.name} n'a pas de composant 'EnemyObjectiveTracker' !");
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