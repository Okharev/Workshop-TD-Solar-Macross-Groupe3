using UnityEngine;

namespace Pathing.Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        [Tooltip("Unique ID to reference this spawner in the Wave Manager (e.g., 'NorthGate', 'Spline0_End')")]
        public string spawnerID;

        public void Spawn(GameObject prefab)
        {
            if (!prefab)
            {
                Debug.LogWarning($"[EnemySpawner] {name} tried to spawn a null prefab.");
                return;
            }
            Instantiate(prefab, transform.position, transform.rotation);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 1f);
            
            // Draws a label in scene view to help you see IDs
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, spawnerID);
#endif
        }
    }
}