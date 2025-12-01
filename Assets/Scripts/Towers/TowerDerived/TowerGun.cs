using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Towers.TowerDerived
{
    public class TowerGun : BaseTower
    {
        private readonly Collider[] _colliderCache = new Collider[32];

        protected override void Fire()
        {
            if (Physics.Raycast(firePoint.position, currentTarget.transform.position, out var hit, range.Value.CurrentValue, targetLayer))
                Debug.DrawLine(firePoint.position, hit.point, Color.green, 0.2f);
            // Assuming you have an IDamageable or similar interface
            /*
                if (hit.collider.TryGetComponent<IDamageable>(out var victim))
                {
                    victim.TakeDamage(dmg);
                }
                */
        }

        protected override void AcquireTarget()
        {
            var hits = Physics.OverlapSphereNonAlloc(transform.position, range.Value.CurrentValue, _colliderCache, targetLayer);
            Transform bestTarget = null;
            var bestDist = float.MaxValue;

            foreach (var hit in _colliderCache.AsSpan(0, hits))
            {
                if (Physics.Linecast(firePoint.position, hit.transform.position, visionBlockerLayer))
                    if (hit.gameObject != gameObject)
                    {
                        continue;
                    }

                var dist = (hit.transform.position - transform.position).sqrMagnitude;

                if (!(dist < bestDist)) continue;

                bestTarget = hit.transform;
                bestDist = dist;
            }

            currentTarget = bestTarget;
        }
    }
}