using System;
using System.Collections;
using UnityEngine;

namespace Towers.TargetingStrategies
{
    public class DensestPackTargeting
    {
        [Header("Cluster Settings")]
        [Tooltip("Rayon d'explosion du mortier (pour vérifier la densité)")]
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private float scanInterval = 0.5f;
        [SerializeField] private LayerMask enemyLayer;

        // Events
        public event Action<Transform> OnTargetAcquired;
        public event Action OnTargetLost;

        private Coroutine _coroutine;
        private readonly Collider[] _candidates = new Collider[32];
        private readonly Collider[] _neighbors = new Collider[16];

        public void Initialize(TowerEntity tower)
        {
            Dispose(tower);
            _coroutine = tower.StartCoroutine(SearchRoutine(tower));
        }

        public void Dispose(TowerEntity tower)
        {
            if (_coroutine != null && tower != null) tower.StopCoroutine(_coroutine);
        }

        private IEnumerator SearchRoutine(TowerEntity tower)
        {
            var wait = new WaitForSeconds(scanInterval);
            while (true)
            {
                CalculateBestCluster(tower);
                yield return wait;
            }
        }

        private void CalculateBestCluster(TowerEntity tower)
        {
            int count = Physics.OverlapSphereNonAlloc(tower.transform.position, tower.range.Value, _candidates, enemyLayer);
            
            if (count == 0)
            {
                tower.currentTarget = null;
                OnTargetLost?.Invoke();
                return;
            }

            Vector3 bestClusterCenter = Vector3.zero;
            int maxNeighbors = -1;
            Transform bestRefTarget = null;


            for (int i = 0; i < count; i++)
            {
                var candidate = _candidates[i].transform;
                
                int neighborCount = Physics.OverlapSphereNonAlloc(candidate.position, explosionRadius, _neighbors, enemyLayer);
                
                if (neighborCount > maxNeighbors)
                {
                    maxNeighbors = neighborCount;
                    bestRefTarget = candidate;
                    
                    Vector3 centroidSum = Vector3.zero;
                    for (int n = 0; n < neighborCount; n++)
                    {
                        centroidSum += _neighbors[n].transform.position;
                    }
                    bestClusterCenter = centroidSum / neighborCount;
                }
            }

            tower.aimPoint = bestClusterCenter;
            
            if (tower.currentTarget != bestRefTarget)
            {
                tower.currentTarget = bestRefTarget;
                if (bestRefTarget) OnTargetAcquired?.Invoke(bestRefTarget);
            }
        }
    }
}