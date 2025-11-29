using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Towers
{
    public interface IUpgradeInstance
    {
        void Enable();
        void Disable();
    }

    public abstract class UpgradeSo : ScriptableObject
    {
        public Sprite icon;

        public string upgradeName;
        public string description;

        public abstract IUpgradeInstance CreateInstance(TowerEntity tower);
    }

    [CreateAssetMenu(menuName = "Upgrades/Bleed")]
    public class BleedUpgradeSo : UpgradeSo
    {
        public float stackDuration = 3f;
        public float tickRate = 0.5f;
        public int damagePerStack = 5;
        
        public override IUpgradeInstance CreateInstance(TowerEntity tower)
        {
            return new BleedInstance(this, tower);
        }

        [Serializable]
        private class BleedInstance : IUpgradeInstance
        {
            private readonly BleedUpgradeSo config;
            private readonly TowerEntity tower;

            public BleedInstance(BleedUpgradeSo config, TowerEntity tower)
            {
                this.config = config;
                this.tower = tower;
            }

            public void Enable()
            {
                if (tower.events != null)
                    tower.events.onHit += TryApplyBleed;
            }

            public void Disable()
            {
                if (tower.events != null)
                    tower.events.onHit -= TryApplyBleed;
            }

            private void TryApplyBleed(UpgradeProvider.OnHitData data)
            {
                if (!data.target) return;

                if (data.damageType == UpgradeProvider.DamageType.DoT) return;

                if (!data.target.TryGetComponent<StatusHandler>(out var statusHandler)) return;

                var effect = new BleedStatus(config, tower, data.target);
                statusHandler.ApplyStatus(effect);
            }
        }

        [Serializable]
        private class BleedStatus : IStatusEffect
        {
            private readonly BleedUpgradeSo config;
            private readonly TowerEntity sourceTower;

            private readonly List<float> stackExpirations = new(16);
            private readonly GameObject target;

            public BleedStatus(BleedUpgradeSo config, TowerEntity sourceTower, GameObject target)
            {
                this.config = config;
                this.sourceTower = sourceTower;
                this.target = target;
            }

            public void OnApply(StatusHandler host)
            {
                AddStack();

                // VFXManager.Instance.Spawn(config.impactEffect, target.transform.position, Quaternion.identity);
            }

            public void Reapply(IStatusEffect newInstance)
            {
                AddStack();
            }

            public void OnEnd()
            {
                stackExpirations.Clear();
            }

            public IEnumerator Process(StatusHandler host)
            {
                var waiter = new WaitForSeconds(config.tickRate);

                while (true)
                {
                    yield return waiter;

                    var currentTime = Time.time;

                    for (var i = stackExpirations.Count - 1; i >= 0; i--)
                        if (currentTime >= stackExpirations[i])
                            stackExpirations.RemoveAt(i);

                    if (stackExpirations.Count == 0)
                    {
                        yield return null;

                        if (stackExpirations.Count == 0) yield break;

                        continue;
                    }

                    if (!target) yield break;
                    //TODO if (!target.TryGetComponent<HealthComponent>(out var hp)) continue;

                    var totalDamage = stackExpirations.Count * config.damagePerStack;

                    sourceTower.events.onHit?.Invoke(new UpgradeProvider.OnHitData
                    {
                        origin = sourceTower.gameObject,
                        target = target,
                        damage = totalDamage,
                        damageType = UpgradeProvider.DamageType.DoT
                    });

                    // var isDead = hp.TakeDamage(totalDamage);

                    // if (!isDead) continue;
// 
                    // sourceTower.events.onKill?.Invoke(new UpgradeProvider.OnKillData
                    // {
                    //     origin = sourceTower.gameObject,
                    //     target = target,
                    //     damage = totalDamage
                    // });
// 
                    // yield break;
                }
            }

            public Sprite GetIcon()
            {
                return config.icon;
            }

            public float GetDurationRatio()
            {
                if (stackExpirations.Count == 0 || config.stackDuration <= 0) return 0;

                var maxExpiration = 0f;
                foreach (var exp in stackExpirations)
                    if (exp > maxExpiration)
                        maxExpiration = exp;

                var remaining = maxExpiration - Time.time;
                return Mathf.Clamp01(remaining / config.stackDuration);
            }

            public int GetStackCount()
            {
                return stackExpirations.Count;
            }

            private void AddStack()
            {
                stackExpirations.Add(Time.time + config.stackDuration);
            }
        }
    }

    [CreateAssetMenu(menuName = "Upgrades/ExplosionOnDeath")]
    public class ExplosionUpgradeSo : UpgradeSo
    {
        public float radius = 3f;

        public int explosionDamage = 50;

        public float explosionDelay = 0.1f;
        public LayerMask enemyLayer;

        // [SerializeField] private VFXDefinition impactEffect;


        public override IUpgradeInstance CreateInstance(TowerEntity tower)
        {
            return new ExplosionInstance(this, tower);
        }

        [Serializable]
        private class ExplosionInstance : IUpgradeInstance
        {
            private readonly ExplosionUpgradeSo config;
            private readonly TowerEntity tower;

            // Pre-allocated buffer for physics hits
            private Collider[] _colliders = new Collider[16];

            public ExplosionInstance(ExplosionUpgradeSo config, TowerEntity tower)
            {
                this.config = config;
                this.tower = tower;
            }

            public void Enable()
            {
                if (tower.events != null)
                    tower.events.onKill += QueueExplosion;
            }

            public void Disable()
            {
                if (tower.events != null)
                    tower.events.onKill -= QueueExplosion;
            }

            private void QueueExplosion(UpgradeProvider.OnKillData data)
            {
                var explosionCenter = data.target.transform.position;

                tower.StartCoroutine(ExplosionRoutine(explosionCenter, data.target));
            }

            private IEnumerator ExplosionRoutine(Vector3 center, GameObject originalVictim)
            {
                if (config.explosionDelay > 0)
                    yield return new WaitForSeconds(config.explosionDelay);
                else
                    yield return null; 

                // VFXManager.Instance.Spawn(config.impactEffect, center, Quaternion.identity);

                var hits = Physics.OverlapSphereNonAlloc(center, config.radius, _colliders, config.enemyLayer);

                for (var i = 0; i < hits; i++)
                {
                    var hit = _colliders[i];

                    if (hit.gameObject == originalVictim) continue;

                    // if (!hit.TryGetComponent<HealthComponent>(out var hp)) continue;

                    // if (hp.currentHealth <= 0) continue;

                    tower.events.onHit?.Invoke(new UpgradeProvider.OnHitData
                    {
                        origin = tower.gameObject,
                        target = hit.gameObject,
                        damage = config.explosionDamage,
                        damageType = UpgradeProvider.DamageType.AreaOfEffect
                    });

                    // if (hp.TakeDamage(config.explosionDamage))
                    //     tower.events.onKill?.Invoke(new UpgradeProvider.OnKillData
                    //     {
                    //         origin = tower.gameObject,
                    //         target = hit.gameObject,
                    //         damage = config.explosionDamage
                    //     });
                }
            }
        }
    }

    [CreateAssetMenu(menuName = "Upgrades/Execute")]
    public class ExecuteUpgradeSo : UpgradeSo
    {
        public float executeThreshold = 0.15f;

        // [SerializeField] private VFXDefinition impactEffect;

        public override IUpgradeInstance CreateInstance(TowerEntity tower)
        {
            return new ExecuteInstance(this, tower);
        }

        [Serializable]
        private class ExecuteInstance : IUpgradeInstance
        {
            private readonly ExecuteUpgradeSo config;
            private readonly TowerEntity tower;

            public ExecuteInstance(ExecuteUpgradeSo config, TowerEntity tower)
            {
                this.config = config;
                this.tower = tower;
            }

            public void Enable()
            {
                if (tower.events != null)
                    tower.events.onHit += TryExecuteTarget;
            }

            public void Disable()
            {
                if (tower.events != null)
                    tower.events.onHit -= TryExecuteTarget;
            }

            private void TryExecuteTarget(UpgradeProvider.OnHitData data)
            {
                if (!data.target) return;

                // if (!data.target.TryGetComponent<HealthComponent>(out var healthComp)) return;

                // if (healthComp.currentHealth <= 0) return;
// 
                // var healthPercent = (float)healthComp.currentHealth / healthComp.maxHealth;
// 
                // if (!(healthPercent <= config.executeThreshold)) return;
// 
                // Debug.Log("playedSlash");
                // VFXManager.Instance.Spawn(config.impactEffect, data.target.transform.position, Quaternion.identity);
// 
                // healthComp.Die();
// 
                // var killData = new UpgradeProvider.OnKillData
                // {
                //     origin = tower.gameObject,
                //     target = data.target,
                //     damage = healthComp.currentHealth
                // };
// 
                // tower.events.onKill?.Invoke(killData);
            }
        }
    }

    [CreateAssetMenu(menuName = "Upgrades/Poison")]
    public class PoisonUpgradeSo : UpgradeSo
    {
        public float duration = 6f;

        public float tickRate = 1f;

        public int damagePerTick = 5;

        // private VFXDefinition impactEffect;

        public override IUpgradeInstance CreateInstance(TowerEntity tower)
        {
            return new PoisonInstance(this, tower);
        }

        [Serializable]
        private class PoisonInstance : IUpgradeInstance
        {
            private readonly PoisonUpgradeSo config;
            private readonly TowerEntity tower;

            public PoisonInstance(PoisonUpgradeSo config, TowerEntity tower)
            {
                this.config = config;
                this.tower = tower;
            }

            public void Enable()
            {
                if (tower.events != null)
                    tower.events.onHit += TryApplyPoison;
            }

            public void Disable()
            {
                if (tower.events != null)
                    tower.events.onHit -= TryApplyPoison;
            }

            private void TryApplyPoison(UpgradeProvider.OnHitData data)
            {
                if (!data.target) return;

                if (data.damageType == UpgradeProvider.DamageType.DoT) return;

                if (!data.target.TryGetComponent<StatusHandler>(out var statusHandler)) return;

                var effect = new PoisonStatus(config, tower, data.target);
                statusHandler.ApplyStatus(effect);
            }
        }

        private class PoisonStatus : IStatusEffect
        {
            private readonly PoisonUpgradeSo config;
            private readonly TowerEntity sourceTower;
            private readonly GameObject target;
            private float expirationTime;


            public PoisonStatus(PoisonUpgradeSo config, TowerEntity sourceTower, GameObject target)
            {
                this.config = config;
                this.sourceTower = sourceTower;
                this.target = target;
            }

            public void OnApply(StatusHandler host)
            {
                expirationTime = Time.time + config.duration;

                // VFXManager.Instance.Spawn(config.impactEffect, target.transform.position, Quaternion.identity);
            }

            public void Reapply(IStatusEffect newInstance)
            {
                expirationTime = Time.time + config.duration;
            }

            public void OnEnd()
            {
            }

            public IEnumerator Process(StatusHandler host)
            {
                var waiter = new WaitForSeconds(config.tickRate);

                while (Time.time < expirationTime)
                {
                    yield return waiter;
                    
                    if (Time.time > expirationTime + 0.1f) yield break;

                    if (!target) yield break;

                    // if (!target.TryGetComponent<HealthComponent>(out var hp)) continue;

                    sourceTower.events.onHit?.Invoke(new UpgradeProvider.OnHitData
                    {
                        origin = sourceTower.gameObject,
                        target = target,
                        damage = config.damagePerTick,
                        damageType = UpgradeProvider.DamageType.DoT
                    });

                    // var isDead = hp.TakeDamage(config.damagePerTick);

                    // if (!isDead) continue;

                    sourceTower.events.onKill?.Invoke(new UpgradeProvider.OnKillData
                    {
                        origin = sourceTower.gameObject,
                        target = target,
                        damage = config.damagePerTick
                    });

                    yield break;
                }
            }

            public Sprite GetIcon()
            {
                return config.icon;
            }

            public float GetDurationRatio()
            {
                if (config.duration <= 0) return 0;
                var remaining = expirationTime - Time.time;
                return Mathf.Clamp01(remaining / config.duration);
            }

            public int GetStackCount()
            {
                return 1;
            }
        }
    }
}