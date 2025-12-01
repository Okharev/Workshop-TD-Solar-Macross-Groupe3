using UnityEngine;

namespace Towers.ProjectileDerived
{
    [RequireComponent(typeof(Rigidbody))]
    public class MortarBomb : BaseProjectile
    {
        public Rigidbody rigidbody;
        private float _explosionRange;

        private bool _hasExploded;

        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
            Destroy(gameObject, 15f);
        }

        public void Initialize(BaseTower tower, float damageRange)
        {
            _explosionRange = damageRange;
            source = tower;
            _hasExploded = false;
        }

        protected override void HandleImpact(Collider other)
        {
            if (_hasExploded) return;
            _hasExploded = true;

            var hits = Physics.OverlapSphereNonAlloc(transform.position, _explosionRange, CollidersCache,
                source.targetLayer);

            Debug.Log($"Hit enemy count: {hits}");

            for (var i = 0; i < hits; i++)
            {
                var obj = CollidersCache[i];
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