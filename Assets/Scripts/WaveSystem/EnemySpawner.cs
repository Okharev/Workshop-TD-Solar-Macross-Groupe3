using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using WaveSystem;

namespace DynamicWaveSystem
{

    public class EnemySpawner : MonoBehaviour
    {
        public string spawnerID = "Spawner_A";
        public SplineContainer splineContainer;
        [Tooltip("Which spline index inside the container does this spawner start at?")]
        public int startSplineIndex = 0;

        public void ExecuteSegments(List<WaveSegment> segments)
        {
            StartCoroutine(SpawnRoutine(segments));
        }

        private IEnumerator SpawnRoutine(List<WaveSegment> segments)
        {
            foreach (var seg in segments)
            {
                if (seg.preDelay > 0) yield return new WaitForSeconds(seg.preDelay);

                float interval = seg.duration / seg.count;

                for (int i = 0; i < seg.count; i++)
                {
                    SpawnEnemy(seg.enemyPrefab);
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        private void SpawnEnemy(GameObject prefab)
        {
            GameObject enemy = Instantiate(prefab, transform);
        }
        
        private void OnDrawGizmos()
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.red;
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            
            Handles.Label(new Vector3(transform.position.x, transform.position.y + 2f, transform.position.z), spawnerID, style);
            
        }
    }
}