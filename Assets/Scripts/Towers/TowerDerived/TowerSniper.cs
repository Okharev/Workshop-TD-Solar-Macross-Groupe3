using System;
using System.Collections.Generic;
using UnityEngine;

namespace Towers.TowerDerived
{
    public class TowerSniper : BaseTower
    {
        [Header("Sniper Config")] [Tooltip("Minimum distance to shoot (Dead zone radius)")]
        public float minRange = 5f;

        [Tooltip("Half-size of the projectile box.")]
        public float projectileThickness = 0.05f;

        private readonly Collider[] _colliderCache = new Collider[64];

        [Header("Performance")] private readonly RaycastHit[] _piercingHitsCache = new RaycastHit[32];

        private void OnDrawGizmosSelected()
        {
            if (!firePoint) return;

            // Visualisation du Donut
            Gizmos.color = new Color(1, 0, 0, 0.3f); // Rouge = Zone Morte
            Gizmos.DrawWireSphere(transform.position, minRange);

            Gizmos.color = new Color(0, 1, 0, 0.3f); // Vert = Portée Max
            if (range != null && range.Value != null)
                Gizmos.DrawWireSphere(transform.position, range.Value);

            // Visualisation de l'épaisseur du tir
            Gizmos.color = Color.magenta;
            Gizmos.matrix = Matrix4x4.TRS(firePoint.position + firePoint.forward * 2f, firePoint.rotation, Vector3.one);
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

            FirePiercingShot();
        }

        private void FirePiercingShot()
        {
            var currentRange = range.Value;
            var damageAmount = damage.Value;

            var halfExtents = new Vector3(projectileThickness, projectileThickness, projectileThickness);
            var shootDirection = firePoint.forward;

            // 1. BoxCastAll pour tout toucher sur la ligne (NonAlloc pour la performance)
            var hitCount = Physics.BoxCastNonAlloc(
                firePoint.position,
                halfExtents,
                shootDirection,
                _piercingHitsCache,
                firePoint.rotation,
                1000f,
                targetLayer | visionBlockerLayer
            );

            // 2. Il faut TRIER les résultats par distance. 
            // BoxCastAll ne garantit pas l'ordre, et on ne veut pas tuer un ennemi derrière un mur.
            // On utilise un tri simple sur la portion utilisée du tableau.
            Array.Sort(_piercingHitsCache, 0, hitCount, new RaycastHitDistanceComparer());

            // Debug visuel du laser
            Debug.DrawRay(firePoint.position, shootDirection * currentRange, Color.yellow, 0.5f);


            Debug.Log($"hit {hitCount} in a single shot");
            // 3. Itération sur les touches triées
            for (var i = 0; i < hitCount; i++)
            {
                var hit = _piercingHitsCache[i];

                // A. Si on touche un mur (Vision Blocker), le tir s'arrête net.
                // (Assure-toi que tes murs sont bien sur le layer défini dans 'visionBlockerLayer')
                if (((1 << hit.collider.gameObject.layer) & visionBlockerLayer) != 0)
                {
                    // Le tir a touché un mur, on arrête la balle ici.
                    Debug.DrawLine(firePoint.position, hit.point, Color.red, 0.5f);
                    break;
                }

                // B. Si on touche un ennemi (Target Layer)
                if (((1 << hit.collider.gameObject.layer) & targetLayer) != 0)
                {
                    // Logique de dégâts
                    // TODO: Remplace 'IDamageable' par ton vrai script de vie (ex: EnemyHealth)
                    /*
                    if (hit.collider.TryGetComponent<IDamageable>(out var victim))
                    {
                        victim.TakeDamage(damageAmount);
                        // Effet visuel d'impact ici
                    }
                    */

                    // On ne fait PAS de break ici, car c'est un tir perforant !
                    // La balle continue vers le prochain ennemi dans la liste.
                }
            }
        }

        protected override void AcquireTarget()
        {
            var hits = Physics.OverlapSphereNonAlloc(transform.position, range.Value, _colliderCache, targetLayer);
            Transform bestTarget = null;
            var bestDist = float.MaxValue;

            // TODO(Florian) Add health component check
            foreach (var hit in _colliderCache.AsSpan(0, hits))
            {
                if (Physics.Linecast(firePoint.position, hit.transform.position, visionBlockerLayer))
                    continue;

                var dist = (hit.transform.position - transform.position).sqrMagnitude;

                // In dead zone 
                if (dist < minRange * minRange) continue;


                if (dist < bestDist)
                {
                    bestTarget = hit.transform;
                    bestDist = dist;
                }
            }

            currentTarget = bestTarget;
        }

        // Petit helper pour trier les RaycastHits par distance
        private struct RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit x, RaycastHit y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }
    }
}