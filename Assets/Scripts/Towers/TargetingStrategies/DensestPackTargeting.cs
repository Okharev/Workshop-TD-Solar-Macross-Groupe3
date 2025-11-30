using System;
using System.Collections;
using UnityEngine;

namespace Towers.TargetingStrategies
{
    public class DensestPackTargeting
    {
        private readonly Collider[] _candidates = new Collider[32];
        private readonly Collider[] _neighbors = new Collider[16];

        [Header("Cluster Settings")] [Tooltip("Rayon d'explosion du mortier (pour vérifier la densité)")]
        private readonly float explosionRadius = 3f;

        private readonly float scanInterval = 0.5f;

        private Coroutine _coroutine;
        [SerializeField] private LayerMask enemyLayer;

        // Events
        public event Action<Transform> OnTargetAcquired;
        public event Action OnTargetLost;

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
            var count = Physics.OverlapSphereNonAlloc(tower.transform.position, tower.range.Value, _candidates,
                enemyLayer);

            if (count == 0)
            {
                tower.currentTarget = null;
                OnTargetLost?.Invoke();
                return;
            }

            var bestClusterCenter = Vector3.zero;
            var maxNeighbors = -1;
            Transform bestRefTarget = null;


            for (var i = 0; i < count; i++)
            {
                var candidate = _candidates[i].transform;

                var neighborCount =
                    Physics.OverlapSphereNonAlloc(candidate.position, explosionRadius, _neighbors, enemyLayer);

                if (neighborCount > maxNeighbors)
                {
                    maxNeighbors = neighborCount;
                    bestRefTarget = candidate;

                    var centroidSum = Vector3.zero;
                    for (var n = 0; n < neighborCount; n++) centroidSum += _neighbors[n].transform.position;
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