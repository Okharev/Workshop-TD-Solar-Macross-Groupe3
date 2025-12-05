using System;
using UnityEngine;

namespace Towers.TowerDerived
{
    public class TowerGun : BaseTower
    {
        [Header("Gun Config")] [Tooltip("Half-size of the projectile box. 0.1 means a box of 0.2x0.2 size.")]
        public float projectileThickness = 0.1f;

        private readonly Collider[] _colliderCache = new Collider[32];

        private void OnDrawGizmosSelected()
        {
            if (!firePoint) return;

            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS(firePoint.position + firePoint.forward * 1f, firePoint.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero,
                new Vector3(projectileThickness * 2, projectileThickness * 2, projectileThickness * 2));
        }

        protected override void Fire()
        {
            Events.OnFire?.Invoke(new UpgradeProvider.OnFireData
            {
                Origin = gameObject,
                Target = currentTarget ? currentTarget.gameObject : null
            });

            FireSingleBoxProjectile();
        }

        private void FireSingleBoxProjectile()
        {
            var currentRange = range.Value;
            var damageAmount = damage.Value;

            var halfExtents = new Vector3(projectileThickness, projectileThickness, projectileThickness);

            var shootDirection = firePoint.forward;
            var orientation = firePoint.rotation;

            var hasHit = Physics.BoxCast(
                firePoint.position,
                halfExtents,
                shootDirection,
                out var hit,
                orientation,
                currentRange,
                targetLayer
            );

            if (hasHit)
                Debug.DrawLine(firePoint.position, hit.point, Color.green, 0.2f);
            /* Do damage
                if (hit.collider.TryGetComponent<IDamageable>(out var victim))
                {
                    victim.TakeDamage(damageAmount)
                }
                */
            else
                Debug.DrawRay(firePoint.position, shootDirection * currentRange, Color.red, 0.2f);
        }

        protected override void AcquireTarget()
        {
            var hits = Physics.OverlapSphereNonAlloc(transform.position, range.Value, _colliderCache, targetLayer);
            Transform bestTarget = null;
            var bestDist = float.MaxValue;

            foreach (var hit in _colliderCache.AsSpan(0, hits))
            {
                if (hit) continue;

                if (Physics.Linecast(firePoint.position, hit.transform.position, visionBlockerLayer))
                    continue;

                var dist = (hit.transform.position - transform.position).sqrMagnitude;

                if (dist < bestDist)
                {
                    bestTarget = hit.transform;
                    bestDist = dist;
                }
            }

            currentTarget = bestTarget;
        }
    }
}