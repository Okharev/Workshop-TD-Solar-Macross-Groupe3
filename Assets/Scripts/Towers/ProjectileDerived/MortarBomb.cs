using UnityEngine;

namespace Towers.ProjectileDerived
{
    [RequireComponent(typeof(Rigidbody))]
    public class MortarBomb : BaseProjectile
    {
        public Rigidbody rigidbody;
        private float explosionRange;

        private bool hasExploded;

        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
            Destroy(gameObject, 15f);
        }

        public void Initialize(BaseTower tower, float damageRange)
        {
            explosionRange = damageRange;
            source = tower;
            hasExploded = false;
        }

        protected override void HandleImpact(Collider other)
        {
            if (hasExploded) return;
            hasExploded = true;

            var hits = Physics.OverlapSphereNonAlloc(transform.position, explosionRange, collidersCache,
                source.targetLayer);

            Debug.Log($"Hit enemy count: {hits}");

            for (var i = 0; i < hits; i++)
            {
                var obj = collidersCache[i];
                if (obj == null) continue;

                /*
                if (!obj.TryGetComponent<HealthComponent>(out var health)) continue;

                firingTower.events.onHit?.Invoke(new UpgradeProvider.OnHitData
                {
                    damage = damage,
                    damageType = UpgradeProvider.DamageType.AreaOfEffect,
                    origin = firingTower.gameObject,
                    target = obj.gameObject
                });

                if (health.TakeDamage(damage))
                    firingTower.events.onKill?.Invoke(new UpgradeProvider.OnKillData
                    {
                        damage = damage,
                        origin = firingTower.gameObject,
                        target = obj.gameObject
                    });
                */
            }

            Destroy(gameObject);
        }

        protected override bool IsValidHit(Collider hitObject)
        {
            return true;
        }
    }
}