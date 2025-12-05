using System;
using System.Collections.Generic;
using Economy;
using UnityEngine;
// Nécessaire pour le Dictionary
using Random = UnityEngine.Random;

namespace Towers.TowerDerived
{
    public class TowerShotgun : BaseTower
    {
        [Header("Shotgun Config")] public int pelletCount = 6;

        public float knockbackForce = 2f; // Force par plomb
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

            // 1. On nettoie le tracker avant de tirer
            _hitTracker.Clear();

            // 2. On tire tous les plombs et on enregistre qui est touché
            for (var i = 0; i < pelletCount; i++) FireSingleRayAndTrack(damagePerPellet);

            // 3. On applique le Knockback CUMULÉ
            ApplyAccumulatedKnockback();
        }

        private void ApplyAccumulatedKnockback()
        {
            foreach (var entry in _hitTracker)
            {
                var enemy = entry.Key;
                var hitCount = entry.Value;

                // La force est multipliée par le nombre de plombs reçus !
                // Si l'ennemi prend 6 plombs, il recule 6x plus fort.
                var totalForce = knockbackForce * hitCount;

                // On peut aussi augmenter légèrement la durée si on veut, 
                // mais augmenter la force est souvent plus "punchy".
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

                // --- Logique de dégâts ---
                // Tu devrais décommenter et adapter ceci selon ton système de vie
                /*
                if (hit.collider.TryGetComponent<IDamageable>(out var victim))
                {
                    victim.TakeDamage(dmg);
                }
                */

                // --- Logique de Tracking pour le Knockback ---
                // On essaie de récupérer le script de mouvement sur l'ennemi touché
                var movement = hit.collider.GetComponentInParent<EnemyController>();
                if (movement)
                    if (!_hitTracker.TryAdd(movement, 1))
                        _hitTracker[movement]++;
            }
            else
            {
                Debug.DrawRay(fp.position, shootDir * range.Value, Color.red, 0.2f);
            }
        }
    }
}