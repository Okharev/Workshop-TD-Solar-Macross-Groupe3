using System;
using System.Collections;
using UnityEngine;

namespace Towers.TargetingStrategies
{
    [Serializable]
    public class TargetClosest : ITargetingBehaviours
    {
        [Header("Settings")] [SerializeField] private float scanInterval = 0.5f;

        // 1. CORRECTION : On expose le LayerMask dans l'inspecteur.
        // C'est plus sûr que de taper le nom en dur (string).
        [SerializeField] private LayerMask enemyLayer;

        // 2. AJOUT : Un masque séparé pour les obstacles (Murs), pas les ennemis.
        [SerializeField] private LayerMask obstacleLayer;
        private readonly Collider[] _cache = new Collider[32];

        private Coroutine _searchCoroutine;

        public event Action<Transform> OnTargetAcquired;
        public event Action OnTargetLost;

        public void Initialize(TowerEntity tower)
        {
            Dispose(tower);

            // Sécurité : Si le mask n'est pas réglé dans l'inspecteur, on tente de le trouver
            if (enemyLayer == 0) enemyLayer = LayerMask.GetMask("Enemy");
            if (obstacleLayer == 0) obstacleLayer = LayerMask.GetMask("Terrain", "PlacementBlockers");

            _searchCoroutine = tower.StartCoroutine(SearchRoutine(tower));
        }

        public void Dispose(TowerEntity tower)
        {
            if (_searchCoroutine != null && tower)
            {
                tower.StopCoroutine(_searchCoroutine);
                _searchCoroutine = null;
            }
        }

        private IEnumerator SearchRoutine(TowerEntity tower)
        {
            var waitForInterval = new WaitForSeconds(scanInterval);

            while (true)
            {
                // Si la tour est désactivée ou détruite, on arrête
                if (!tower) yield break;

                FindTarget(tower);
                yield return waitForInterval;
            }
        }

        private void FindTarget(TowerEntity tower)
        {
            // 3. Utilisation correcte du LayerMask (enemyLayer)
            var hits = Physics.OverlapSphereNonAlloc(tower.transform.position, tower.range.Value, _cache, enemyLayer);

            Transform bestTarget = null;
            var closestSqrDist = float.MaxValue;

            for (var i = 0; i < hits; i++)
            {
                var potentialTarget = _cache[i].transform;

                // 4. CORRECTION LINECAST :
                // On vérifie si un OBSTACLE bloque la vue, pas si un ennemi bloque la vue.
                // On surélève légèrement le point de départ (Vector3.up * 0.5f) pour ne pas toucher le sol immédiatement.
                var origin = tower.transform.position + Vector3.up * 0.5f;
                var targetPos = potentialTarget.position + Vector3.up * 0.5f; // On vise le centre/haut de l'ennemi

                if (Physics.Linecast(origin, targetPos, obstacleLayer))
                    continue; // Un mur bloque la vue

                var sqrDist = (potentialTarget.position - tower.transform.position).sqrMagnitude;

                if (sqrDist < closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    bestTarget = potentialTarget;
                }
            }

            if (bestTarget != tower.currentTarget)
            {
                tower.currentTarget = bestTarget;

                if (bestTarget)
                    OnTargetAcquired?.Invoke(bestTarget);
                else
                    OnTargetLost?.Invoke();
            }
        }
    }
}