using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Towers
{
    public class MissileTower : BaseTower
    {
        [Header("Missile Configuration")] [SerializeField]
        private HomingMissile missilePrefab;

        [SerializeField] private Transform[] launchPoints; // Array of launch tubes

        [Header("Salvo Settings")] [Tooltip("How many missiles to fire per attack cycle.")] [SerializeField]
        private int missileCount = 6;

        [Tooltip("Time between individual missiles in the salvo.")] [SerializeField]
        private float dispatchInterval = 0.15f;

        [Tooltip("Max enemies to lock onto simultaneously.")] [SerializeField]
        private int maxMultiLockTargets = 4;

        [Header("Visuals")]
        [Tooltip("The fixed vertical angle for the launcher (e.g. -45 for looking up).")]
        [SerializeField]
        private float launchPitchAngle = -45f;

        [Tooltip("How far out of the tube the missile flies before turning.")] [SerializeField]
        private float launchForceDistance = 4f;

        private readonly List<Transform> _lockedTargets = new();

        // Internal
        private readonly Collider[] _targetBuffer = new Collider[32];
        private WaitForSeconds _dispatchWait;

        protected override void Start()
        {
            base.Start();
            _dispatchWait = new WaitForSeconds(dispatchInterval);

            if (launchPoints == null || launchPoints.Length == 0)
                launchPoints = new[] { firePoint };
        }

        // --- Targeting Logic Override ---


        protected override void AcquireTarget()
        {
            // Standard logic: Find closest target just to orient the turret
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

        // --- Salvo Routine ---

        private IEnumerator SalvoRoutine()
        {
            isBusy = true; // Block BaseTower from firing again

            RefreshSalvoTargets();

            if (_lockedTargets.Count > 0)
                for (var i = 0; i < missileCount; i++)
                {
                    var target = _lockedTargets[i % _lockedTargets.Count];

                    // Cleanup check: if target died, try to find another
                    if (!target) target = GetFirstAliveTarget();

                    if (target) FireSingleMissile(target, i);
                    yield return _dispatchWait;
                }

            isBusy = false;
        }

        private void FireSingleMissile(Transform target, int index)
        {
            // 1. Pick a tube
            var tube = launchPoints[index % launchPoints.Length];

            // 2. Spawn Missile
            var missile = Instantiate(missilePrefab, tube.position, tube.rotation);

            // 3. Stats Setup
            missile.damage = Mathf.RoundToInt(damage.Value);
            missile.Setup(this);

            // 4. Calculate Flight Path (The Javelin Arc)
            var path = new List<Vector3>();

            // WAYPOINT 1: Ejection
            // Force the missile to fly straight out of the tilted tube for a set distance.
            // This sells the "Launch" effect.
            var ejectionPoint = tube.position + tube.forward * launchForceDistance;
            path.Add(ejectionPoint);

            // WAYPOINT 2: The Arc
            // We calculate a point high above the midpoint between tower and enemy
            var midPoint = Vector3.Lerp(transform.position, target.position, 0.5f);
            midPoint.y = Mathf.Max(midPoint.y, transform.position.y) + 15f; // Add significant height

            // Add some random spread to the arc so missiles don't fly in a perfect single file line
            var randomSpread = Random.insideUnitSphere * 2f;
            randomSpread.y = 0; // Keep spread horizontal

            path.Add(midPoint + randomSpread);

            // 5. Launch
            missile.Launch(path, target);
        }

        private void RefreshSalvoTargets()
        {
            _lockedTargets.Clear();
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, range.Value, _targetBuffer, targetLayer);
            if (hitCount == 0) return;

            // Simple Logic: just grab the first N targets found. 
            // For better logic, you can sort by Health (Weakest first) or Distance.
            var count = Mathf.Min(hitCount, maxMultiLockTargets);
            for (var i = 0; i < count; i++) _lockedTargets.Add(_targetBuffer[i].transform);
        }

        private Transform GetFirstAliveTarget()
        {
            foreach (var t in _lockedTargets)
                if (t)
                    return t;
            return null;
        }

        protected override void OnDrawGizmosTower()
        {
            Gizmos.color = Color.red;
            if (launchPoints != null)
                foreach (var lp in launchPoints)
                    if (lp)
                        Gizmos.DrawRay(lp.position, lp.forward * launchForceDistance);
        }
    }
}