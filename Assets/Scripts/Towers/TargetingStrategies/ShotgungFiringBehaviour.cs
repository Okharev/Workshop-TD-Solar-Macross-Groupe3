using System;
using UnityEngine;

using Towers.TargetingStrategies;
using Random = UnityEngine.Random;

namespace Towers.Architecture.Strategies
{
    [Serializable]
    public class WeaponShotgunRaycast : IWeaponStrategy
    {
        [Header("Shotgun Config")]
        [SerializeField] private int pelletCount = 6;
        [SerializeField] [Range(0f, 45f)] private float spreadAngle = 10f;
        
        [Header("Debug")]
        [Tooltip("Draws debug lines in the Scene view to show where rays are actually going.")]
        [SerializeField] private bool showDebugRays = true;
        [SerializeField] private float debugDuration = 2f; // How long lines stay visible

        [Header("Damage Logic")]
        [SerializeField] private bool splitDamage = true;

        [Header("Visuals")]
        [SerializeField] private GameObject tracerPrefab;
        [SerializeField] private GameObject impactVfx;

        // Events
        public event Action OnFired;
        public event Action OnReloadStart;
        public event Action OnReloadComplete;

        private bool _isReloading;

        public void Initialize(TowerEntity tower) 
        { 
            _isReloading = false; 
        }

        public void Dispose(TowerEntity tower) { }

        public void UpdateWeapon(TowerEntity tower, float deltaTime)
        {
            if (tower.fireTimer > 0)
            {
                tower.fireTimer -= deltaTime;
                if (tower.fireTimer <= 0 && _isReloading)
                {
                    _isReloading = false;
                    OnReloadComplete?.Invoke();
                }
            }

            if (tower.currentTarget != null && tower.fireTimer <= 0)
            {
                if (tower.isAligned)
                {
                    Fire(tower);
                }
            }
        }

        private void Fire(TowerEntity tower)
        {
            float totalDamage = tower.damage.Value;
            float damagePerPellet = splitDamage ? (totalDamage / pelletCount) : totalDamage;
            
            tower.events.onFire?.Invoke(new UpgradeProvider.OnFireData 
            { 
                origin = tower.gameObject, 
                target = tower.currentTarget.gameObject 
            });

            for (int i = 0; i < pelletCount; i++)
            {
                FireSingleRay(tower, damagePerPellet);
            }

            float rate = tower.fireRate.Value > 0 ? tower.fireRate.Value : 0.5f;
            tower.fireTimer = 1f / rate;
            _isReloading = true;

            OnFired?.Invoke();
            OnReloadStart?.Invoke();
        }

        private void FireSingleRay(TowerEntity tower, float dmg)
        {
            Transform fp = tower.firePoint;
            float range = tower.range.Value;

            // 1. Calculate Spread
            Vector2 randomCircle = Random.insideUnitCircle * spreadAngle;
            Quaternion spreadRot = Quaternion.Euler(randomCircle.x, randomCircle.y, 0);
            Vector3 shootDir = fp.rotation * spreadRot * Vector3.forward;

            // 2. Physics Raycast
            bool hitSomething = Physics.Raycast(fp.position, shootDir, out RaycastHit hit, range, tower.EnemyLayer);
            
            // 3. DEBUG DRAWING (Scene View)
            if (showDebugRays)
            {
                Vector3 endPoint = hitSomething ? hit.point : (fp.position + shootDir * range);
                Color color = hitSomething ? Color.green : Color.red;
                
                // Draw a line from the FirePoint to exactly where the calculation went
                Debug.DrawLine(fp.position, endPoint, color, debugDuration);
            }

            // 4. Visuals (Game View Tracers)
            if (tracerPrefab)
            {
                Vector3 visualEndPoint = hitSomething ? hit.point : (fp.position + shootDir * range);
                var tracerObj = UnityEngine.Object.Instantiate(tracerPrefab, fp.position, Quaternion.LookRotation(shootDir));
                
                // if (tracerObj.TryGetComponent<Towers.Visuals.ShotgunTracerVisual>(out var tracerScript))
                // {
                //     tracerScript.Setup(fp.position, visualEndPoint);
                // }
            }

            // 5. Hit Logic
            if (hitSomething)
            {
                if (impactVfx) UnityEngine.Object.Instantiate(impactVfx, hit.point, Quaternion.LookRotation(hit.normal));

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
}