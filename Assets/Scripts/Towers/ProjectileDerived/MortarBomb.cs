using Enemy;
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

            var hits = Physics.OverlapSphereNonAlloc(transform.position, _explosionRange, collidersCache,
                source.targetLayer);

            Debug.Log($"Hit enemy count: {hits}");

            for (var i = 0; i < hits; i++)
            {
                var obj = collidersCache[i];

                if (!obj.TryGetComponent<HealthComponent>(out var victim)) return;
                source.Events.OnHit?.Invoke(new UpgradeProvider.OnHitData()
                {
                    Origin = gameObject,
                    Target = gameObject
                });


                if (victim.TakeDamage(Mathf.RoundToInt(source.damage.Value)))
                {
                    source.Events.OnKill?.Invoke(new UpgradeProvider.OnKillData()
                    {
                        Origin = gameObject,
                        Target = gameObject
                    });
                }
            }

            Destroy(gameObject);
        }

        protected override bool IsValidHit(Collider hitObject)
        {
            return true;
        }
    }
}