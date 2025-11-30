using System;
using Towers.TargetingStrategies;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Towers.Architecture.Strategies
{
    [Serializable]
    public class WeaponShotgunRaycast : IWeaponStrategy
    {
        [Header("Shotgun Config")] [SerializeField]
        private int pelletCount = 6;

        [SerializeField] [Range(0f, 45f)] private float spreadAngle = 10f;

        [Header("Debug")]
        [Tooltip("Draws debug lines in the Scene view to show where rays are actually going.")]
        [SerializeField]
        private bool showDebugRays = true;

        [SerializeField] private float debugDuration = 2f; // How long lines stay visible

        [Header("Damage Logic")] [SerializeField]
        private bool splitDamage = true;

        [Header("Visuals")] [SerializeField] private GameObject tracerPrefab;

        [SerializeField] private GameObject impactVfx;

        private bool _isReloading;

        // Events
        public event Action OnFired;
        public event Action OnReloadStart;
        public event Action OnReloadComplete;

        public void Initialize(TowerEntity tower)
        {
            _isReloading = false;
        }

        public void Dispose(TowerEntity tower)
        {
        }

        public void UpdateWeapon(TowerEntity tower, float deltaTime)
        {
            if (tower.FireTimer > 0)
            {
                tower.FireTimer -= deltaTime;
                if (tower.FireTimer <= 0 && _isReloading)
                {
                    _isReloading = false;
                    OnReloadComplete?.Invoke();
                }
            }

            if (tower.currentTarget != null && tower.FireTimer <= 0)
                if (tower.isAligned)
                    Fire(tower);
        }

        private void Fire(TowerEntity tower)
        {
            var totalDamage = tower.damage.Value;
            var damagePerPellet = splitDamage ? totalDamage / pelletCount : totalDamage;

            tower.events.onFire?.Invoke(new UpgradeProvider.OnFireData
            {
                origin = tower.gameObject,
                target = tower.currentTarget.gameObject
            });

            for (var i = 0; i < pelletCount; i++) FireSingleRay(tower, damagePerPellet);

            var rate = tower.fireRate.Value > 0 ? tower.fireRate.Value : 0.5f;
            tower.FireTimer = 1f / rate;
            _isReloading = true;

            OnFired?.Invoke();
            OnReloadStart?.Invoke();
        }

        private void FireSingleRay(TowerEntity tower, float dmg)
        {
            var fp = tower.firePoint;
            var range = tower.range.Value;

            // 1. Calculate Spread
            var randomCircle = Random.insideUnitCircle * spreadAngle;
            var spreadRot = Quaternion.Euler(randomCircle.x, randomCircle.y, 0);
            var shootDir = fp.rotation * spreadRot * Vector3.forward;

            // 2. Physics Raycast
            var hitSomething = Physics.Raycast(fp.position, shootDir, out var hit, range, tower.EnemyLayer);

            // 3. DEBUG DRAWING (Scene View)
            if (showDebugRays)
            {
                var endPoint = hitSomething ? hit.point : fp.position + shootDir * range;
                var color = hitSomething ? Color.green : Color.red;

                // Draw a line from the FirePoint to exactly where the calculation went
                Debug.DrawLine(fp.position, endPoint, color, debugDuration);
            }

            // 4. Visuals (Game View Tracers)
            if (tracerPrefab)
            {
                var visualEndPoint = hitSomething ? hit.point : fp.position + shootDir * range;
                var tracerObj = Object.Instantiate(tracerPrefab, fp.position, Quaternion.LookRotation(shootDir));

                // if (tracerObj.TryGetComponent<Towers.Visuals.ShotgunTracerVisual>(out var tracerScript))
                // {
                //     tracerScript.Setup(fp.position, visualEndPoint);
                // }
            }

            // 5. Hit Logic
            if (hitSomething)
                if (impactVfx)
                    Object.Instantiate(impactVfx, hit.point, Quaternion.LookRotation(hit.normal));
            // if (hit.collider.TryGetComponent<HealthComponent>(out var health))
            // {
            //     tower.events.onHit?.Invoke(new UpgradeProvider.OnHitData
            //     {
            //         origin = tower.gameObject,
            //         target = hit.collider.gameObject,
            //         damage = dmg,
            //         damageType = UpgradeProvider.DamageType.Direct
            //     });
            //
            //     bool isDead = health.TakeDamage((int)dmg);
            //
            //     if (isDead)
            //     {
            //         tower.events.onKill?.Invoke(new UpgradeProvider.OnKillData
            //         {
            //             origin = tower.gameObject,
            //             target = hit.collider.gameObject,
            //             damage = dmg
            //         });
            //     }
            // }
        }
    }
}