using System;
using System.Collections.Generic;
using Economy;
using Enemy;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Towers.TowerDerived
{
    public sealed class TowerShotgun : BaseTower
    {
        [Header("Shotgun Config")] public int pelletCount = 6;
        public float knockbackForce = 2f;
        public float knockbackDuration = 0.2f;

        [Tooltip("Horizontal spread in degrees (Width)")]
        public float horizontalSpreadAngle = 30f;

        [Tooltip("Vertical spread multiplier (0.0 = flat line, 1.0 = circle)")] [Range(0f, 1f)]
        public float pelletsThickness = 0.1f;

        [Tooltip("Vertical spread multiplier (0.0 = flat line, 1.0 = circle)")] [Range(0f, 1f)]
        public float verticalSpreadFactor = 0.2f;

        private readonly Collider[] _colliderCache = new Collider[32];

        private readonly Dictionary<EnemyController, int> _hitTracker = new();

        protected override void Fire()
        {
            var totalDamage = damage.Value;
            var damagePerPellet = totalDamage / pelletCount;

            Events.OnFire?.Invoke(new UpgradeProvider.OnFireData
            {
                Origin = gameObject,
                Target = currentTarget ? currentTarget.gameObject : null
            });

            _hitTracker.Clear();

            for (var i = 0; i < pelletCount; i++) FireSingleRayAndTrack(damagePerPellet);

            ApplyAccumulatedKnockback();
        }

        private void ApplyAccumulatedKnockback()
        {
            foreach (var (enemy, hitCount) in _hitTracker)
            {
                var totalForce = knockbackForce * hitCount;


                enemy.ApplyKnockback(transform.position, totalForce, knockbackDuration);
            }
        }

        protected override void AcquireTarget()
        {
            var hits = Physics.OverlapSphereNonAlloc(transform.position, range.Value, _colliderCache, targetLayer);
            Transform bestTarget = null;
            var bestDist = float.MaxValue;

            foreach (var hit in _colliderCache.AsSpan(0, hits))
            {
                if (Physics.Linecast(firePoint.position, hit.transform.position, visionBlockerLayer))
                    continue;

                var dist = (hit.transform.position - transform.position).sqrMagnitude;

                if (!(dist < bestDist)) continue;

                bestTarget = hit.transform;
                bestDist = dist;
            }

            currentTarget = bestTarget;
        }

        private void FireSingleRayAndTrack(float dmg)
        {
            var fp = firePoint;
            var randomCircle = Random.insideUnitCircle;
            randomCircle.y *= verticalSpreadFactor;

            var xAngle = randomCircle.x * (horizontalSpreadAngle * 0.5f);
            var yAngle = randomCircle.y * (horizontalSpreadAngle * 0.5f);
            var spreadRot = Quaternion.Euler(-yAngle, xAngle, 0);
            var shootDir = fp.rotation * spreadRot * Vector3.forward;

            PlayShootVFX();
            
            if (Physics.BoxCast(
                    fp.position,
                    new Vector3(pelletsThickness, pelletsThickness, pelletsThickness),
                    shootDir,
                    out var hit,
                    Quaternion.identity,
                    range.Value,
                    targetLayer
                ))
            {
                Debug.DrawRay(fp.position, shootDir * range.Value, Color.green, 0.2f);

                if (!hit.collider.TryGetComponent<HealthComponent>(out var victim)) return;
                Events.OnHit?.Invoke(new UpgradeProvider.OnHitData
                {
                    Origin = gameObject,
                    Target = victim.gameObject
                });
                
                
                 SpawnImpactVFX(hit.point, hit.normal);

                if (victim.gameObject.TryGetComponent<EnemyController>(out var movement))
                    if (!_hitTracker.TryAdd(movement, 1))
                        _hitTracker[movement]++;

                if (victim.TakeDamage(Mathf.RoundToInt(damage.Value)))
                    Events.OnKill?.Invoke(new UpgradeProvider.OnKillData
                    {
                        Origin = gameObject,
                        Target = gameObject
                    });
            }
            else
            {
                Debug.DrawRay(fp.position, shootDir * range.Value, Color.red, 0.2f);
            }
        }
    }
}