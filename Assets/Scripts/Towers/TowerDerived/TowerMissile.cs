using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Towers.TowerDerived
{
    public sealed class MissileTower : BaseTower
    {
        [Header("Missile Configuration")] [SerializeField]
        private HomingMissile missilePrefab;

        [SerializeField] private Transform[] launchPoints;

        [Header("Salvo Settings")] [SerializeField]
        private int missileCount = 6;

        [SerializeField] private float dispatchInterval = 0.15f;
        [SerializeField] private int maxMultiLockTargets = 4;

        [Header("Arc Settings")] [Tooltip("How far forward the arc extends before going up.")] [SerializeField]
        private float launchForwardBias = 2f; // Reduced from 4f

        [Tooltip("Height of the arc apex relative to the tower.")] [SerializeField]
        private float arcHeight = 8f; // Reduced from 15f for tighter loops

        [Tooltip("0.0 = Start, 0.5 = Apex, 1.0 = Target. Stop at 0.5 to let Homing take over at the top.")]
        [Range(0.1f, 1f)]
        [SerializeField]
        private float curvePathDuration = 0.5f;

        [SerializeField] private int arcResolution = 8;

        private readonly List<Transform> _lockedTargets = new();
        private readonly Collider[] _targetBuffer = new Collider[32];
        private WaitForSeconds _dispatchWait;

        protected override void Start()
        {
            base.Start();
            _dispatchWait = new WaitForSeconds(dispatchInterval);
            if (launchPoints == null || launchPoints.Length == 0) launchPoints = new[] { firePoint };
        }

        protected override void AcquireTarget()
        {
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, range.Value, _targetBuffer, targetLayer);
            if (hitCount == 0)
            {
                currentTarget = null;
                return;
            }

            Transform bestTarget = null;
            var bestSqrDist = float.MaxValue;

            for (var i = 0; i < hitCount; i++)
            {
                var sqrDist = (_targetBuffer[i].transform.position - transform.position).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    bestTarget = _targetBuffer[i].transform;
                }
            }

            currentTarget = bestTarget;
        }

        protected override void Fire()
        {
            if (isBusy) return;
            StartCoroutine(SalvoRoutine());
        }

        private IEnumerator SalvoRoutine()
        {
            isBusy = true;
            RefreshSalvoTargets();

            if (_lockedTargets.Count > 0)
                for (var i = 0; i < missileCount; i++)
                {
                    var target = _lockedTargets[i % _lockedTargets.Count];
                    if (!target) target = GetFirstAliveTarget();

                    if (target) FireSingleMissile(target, i);

                    yield return _dispatchWait;
                }

            isBusy = false;
        }

        private void FireSingleMissile(Transform target, int index)
        {
            var tube = launchPoints[index % launchPoints.Length];
            var missile = Instantiate(missilePrefab, tube.position, tube.rotation);

            // missile. = Mathf.RoundToInt(damage.Value);
            missile.Setup(this);

            var path = new List<Vector3>();

            // P0: Launch Tube
            var p0 = tube.position;

            // P2: Target Position (The end anchor of the math, even if we don't fly all the way there)
            var p2 = target.position;

            // P1: The Apex Control Point
            // We find the midpoint, raise it up by arcHeight.
            // We also add "launchForwardBias" so the missile flies OUT of the tube before going UP.
            var midPoint = Vector3.Lerp(p0, p2, 0.5f);

            var spread = Random.insideUnitSphere * 1.5f;
            spread.y = 0;

            var p1 = midPoint + Vector3.up * arcHeight + spread + tube.forward * launchForwardBias;

            // --- GENERATION LOOP ---
            // Key Fix: We multiply by 'curvePathDuration'. 
            // If curvePathDuration is 0.5, we only generate the path up to the peak.
            // This forces the missile to switch to Homing Mode exactly when it peaks.
            for (var i = 1; i <= arcResolution; i++)
            {
                var tNormalized = i / (float)arcResolution; // 0 to 1 within the loop
                var tActual = tNormalized * curvePathDuration; // 0 to 0.5 (if duration is 0.5)

                var point = EvaluateQuadraticBezier(p0, p1, p2, tActual);
                path.Add(point);
            }

            missile.Launch(path, target);
        }

        private Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            var u = 1 - t;
            var tt = t * t;
            var uu = u * u;
            return uu * p0 + 2 * u * t * p1 + tt * p2;
        }

        private void RefreshSalvoTargets()
        {
            _lockedTargets.Clear();
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, range.Value, _targetBuffer, targetLayer);
            if (hitCount == 0) return;

            var count = Mathf.Min(hitCount, maxMultiLockTargets);
            for (var i = 0; i < count; i++) _lockedTargets.Add(_targetBuffer[i].transform);
        }

        private Transform GetFirstAliveTarget()
        {
            foreach (var t in _lockedTargets)
                if (t != null && t.gameObject.activeInHierarchy)
                    return t;
            return null;
        }

        protected override void OnDrawGizmosTower()
        {
            Gizmos.color = Color.red;
            if (launchPoints != null)
                foreach (var lp in launchPoints)
                    if (lp)
                        Gizmos.DrawRay(lp.position, lp.forward * launchForwardBias);
        }
    }
}