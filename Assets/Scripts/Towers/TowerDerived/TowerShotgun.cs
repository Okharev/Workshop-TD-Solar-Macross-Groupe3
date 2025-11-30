using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Towers.TowerDerived
{
    public class TowerShotgun : BaseTower
    {
        [Header("Shotgun Config")] public int pelletCount = 6;

        [Tooltip("Horizontal spread in degrees (Width)")]
        public float horizontalSpreadAngle = 30f;

        [Tooltip("Vertical spread multiplier (0.0 = flat line, 1.0 = circle)")] [Range(0f, 1f)]
        public float verticalSpreadFactor = 0.2f;

        private readonly Collider[] colliderCache = new Collider[32];

        protected override void Fire()
        {
            var totalDamage = damage.Value;
            var damagePerPellet = totalDamage / pelletCount;

            events.onFire?.Invoke(new UpgradeProvider.OnFireData
            {
                origin = gameObject,
                target = currentTarget.gameObject
            });

            for (var i = 0; i < pelletCount; i++) FireSingleRay(damagePerPellet);

            var rate = fireRate.Value > 0 ? fireRate.Value : 0.5f;
        }

        protected override void AcquireTarget()
        {
            var hits = Physics.OverlapSphereNonAlloc(transform.position, range.Value, colliderCache, targetLayer);
            Transform bestTarget = null;
            var bestDist = float.MaxValue;

            foreach (var hit in colliderCache.AsSpan(0, hits))
            {
                // If there is a blocker to the closest target
                if (Physics.Linecast(firePoint.position, hit.transform.position, visionBlockerLayer))
                    continue;

                var dist = (hit.transform.position - transform.position).sqrMagnitude;

                if (!(dist < bestDist)) continue;

                bestTarget = hit.transform;
                bestDist = dist;
            }

            currentTarget = bestTarget;
        }


        private void FireSingleRay(float dmg)
        {
            var fp = firePoint;
            
            // 1. Get a random point inside a unit circle (Uniform distribution)
            var randomCircle = Random.insideUnitCircle;

            // 2. Flatten the Y component
            // If factor is 0.1, the spread is 10% high compared to its width
            randomCircle.y *= verticalSpreadFactor;

            // 3. Convert to rotation angles
            // We map the unit circle (-1 to 1) to our desired angle half-width
            var xAngle = randomCircle.x * (horizontalSpreadAngle * 0.5f);
            var yAngle = randomCircle.y * (horizontalSpreadAngle * 0.5f);

            // 4. Create the rotation relative to the gun's forward direction
            var spreadRot = Quaternion.Euler(-yAngle, xAngle, 0);

            var shootDir = fp.rotation * spreadRot * Vector3.forward;
            
            if (Physics.Raycast(fp.position, shootDir, out var hit, range.Value, targetLayer))
                Debug.DrawLine(fp.position, hit.point, Color.green, 0.2f);
            // Assuming you have an IDamageable or similar interface
            /*
                if (hit.collider.TryGetComponent<IDamageable>(out var victim))
                {
                    victim.TakeDamage(dmg);
                }
                */
            else
                Debug.DrawRay(fp.position, shootDir * range.Value, Color.red, 0.2f);
        }
    }
}