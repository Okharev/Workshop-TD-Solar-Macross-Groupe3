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

        public abstract IUpgradeInstance CreateInstance(BaseTower tower);
    }

    [CreateAssetMenu(menuName = "Upgrades/Bleed")]
    public class BleedUpgradeSo : UpgradeSo
    {
        public float stackDuration = 3f;
        public float tickRate = 0.5f;
        public int damagePerStack = 5;

        public override IUpgradeInstance CreateInstance(BaseTower tower)
        {
            return new BleedInstance(this, tower);
        }

        [Serializable]
        private class BleedInstance : IUpgradeInstance
        {
            private readonly BleedUpgradeSo _config;
            private readonly BaseTower _tower;

            public BleedInstance(BleedUpgradeSo config, BaseTower tower)
            {
                this._config = config;
                this._tower = tower;
            }

            public void Enable()
            {
                if (_tower.Events != null)
                    _tower.Events.OnHit += TryApplyBleed;
            }

            public void Disable()
            {
                if (_tower.Events != null)
                    _tower.Events.OnHit -= TryApplyBleed;
            }

            private void TryApplyBleed(UpgradeProvider.OnHitData data)
            {
                if (!data.Target) return;

                if (data.DamageType == UpgradeProvider.DamageType.DoT) return;

                if (!data.Target.TryGetComponent<StatusHandler>(out var statusHandler)) return;

                var effect = new BleedStatus(_config, _tower, data.Target);
                statusHandler.ApplyStatus(effect);
            }
        }

        [Serializable]
        private class BleedStatus : IStatusEffect
        {
            private readonly BleedUpgradeSo _config;
            private readonly BaseTower _sourceTower;

            private readonly List<float> _stackExpirations = new(16);
            private readonly GameObject _target;

            public BleedStatus(BleedUpgradeSo config, BaseTower sourceTower, GameObject target)
            {
                this._config = config;
                this._sourceTower = sourceTower;
                this._target = target;
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
                _stackExpirations.Clear();
            }

            public IEnumerator Process(StatusHandler host)
            {
                var waiter = new WaitForSeconds(_config.tickRate);

                while (true)
                {
                    yield return waiter;

                    var currentTime = Time.time;

                    for (var i = _stackExpirations.Count - 1; i >= 0; i--)
                        if (currentTime >= _stackExpirations[i])
                            _stackExpirations.RemoveAt(i);

                    if (_stackExpirations.Count == 0)
                    {
                        yield return null;

                        if (_stackExpirations.Count == 0) yield break;

                        continue;
                    }

                    if (!_target) yield break;
                    //TODO if (!target.TryGetComponent<HealthComponent>(out var hp)) continue;

                    var totalDamage = _stackExpirations.Count * _config.damagePerStack;

                    _sourceTower.Events.OnHit?.Invoke(new UpgradeProvider.OnHitData
                    {
                        Origin = _sourceTower.gameObject,
                        Target = _target,
                        Damage = totalDamage,
                        DamageType = UpgradeProvider.DamageType.DoT
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
                return _config.icon;
            }

            public float GetDurationRatio()
            {
                if (_stackExpirations.Count == 0 || _config.stackDuration <= 0) return 0;

                var maxExpiration = 0f;
                foreach (var exp in _stackExpirations)
                    if (exp > maxExpiration)
                        maxExpiration = exp;

                var remaining = maxExpiration - Time.time;
                return Mathf.Clamp01(remaining / _config.stackDuration);
            }

            public int GetStackCount()
            {
                return _stackExpirations.Count;
            }

            private void AddStack()
            {
                _stackExpirations.Add(Time.time + _config.stackDuration);
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


        public override IUpgradeInstance CreateInstance(BaseTower tower)
        {
            return new ExplosionInstance(this, tower);
        }

        [Serializable]
        private class ExplosionInstance : IUpgradeInstance
        {
            private readonly ExplosionUpgradeSo _config;
            private readonly BaseTower _tower;

            // Pre-allocated buffer for physics hits
            private Collider[] _colliders = new Collider[16];

            public ExplosionInstance(ExplosionUpgradeSo config, BaseTower tower)
            {
                this._config = config;
                this._tower = tower;
            }

            public void Enable()
            {
                if (_tower.Events != null)
                    _tower.Events.OnKill += QueueExplosion;
            }

            public void Disable()
            {
                if (_tower.Events != null)
                    _tower.Events.OnKill -= QueueExplosion;
            }

            private void QueueExplosion(UpgradeProvider.OnKillData data)
            {
                var explosionCenter = data.Target.transform.position;

                _tower.StartCoroutine(ExplosionRoutine(explosionCenter, data.Target));
            }

            private IEnumerator ExplosionRoutine(Vector3 center, GameObject originalVictim)
            {
                if (_config.explosionDelay > 0)
                    yield return new WaitForSeconds(_config.explosionDelay);
                else
                    yield return null;

                // VFXManager.Instance.Spawn(config.impactEffect, center, Quaternion.identity);

                var hits = Physics.OverlapSphereNonAlloc(center, _config.radius, _colliders, _config.enemyLayer);

                for (var i = 0; i < hits; i++)
                {
                    var hit = _colliders[i];

                    if (hit.gameObject == originalVictim) continue;

                    // if (!hit.TryGetComponent<HealthComponent>(out var hp)) continue;

                    // if (hp.currentHealth <= 0) continue;

                    _tower.Events.OnHit?.Invoke(new UpgradeProvider.OnHitData
                    {
                        Origin = _tower.gameObject,
                        Target = hit.gameObject,
                        Damage = _config.explosionDamage,
                        DamageType = UpgradeProvider.DamageType.AreaOfEffect
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

        public override IUpgradeInstance CreateInstance(BaseTower tower)
        {
            return new ExecuteInstance(this, tower);
        }

        [Serializable]
        private class ExecuteInstance : IUpgradeInstance
        {
            private readonly ExecuteUpgradeSo _config;
            private readonly BaseTower _tower;

            public ExecuteInstance(ExecuteUpgradeSo config, BaseTower tower)
            {
                this._config = config;
                this._tower = tower;
            }

            public void Enable()
            {
                if (_tower.Events != null)
                    _tower.Events.OnHit += TryExecuteTarget;
            }

            public void Disable()
            {
                if (_tower.Events != null)
                    _tower.Events.OnHit -= TryExecuteTarget;
            }

            private void TryExecuteTarget(UpgradeProvider.OnHitData data)
            {
                if (!data.Target) return;

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

        public override IUpgradeInstance CreateInstance(BaseTower tower)
        {
            return new PoisonInstance(this, tower);
        }

        [Serializable]
        private class PoisonInstance : IUpgradeInstance
        {
            private readonly PoisonUpgradeSo _config;
            private readonly BaseTower _tower;

            public PoisonInstance(PoisonUpgradeSo config, BaseTower tower)
            {
                this._config = config;
                this._tower = tower;
            }

            public void Enable()
            {
                if (_tower.Events != null)
                    _tower.Events.OnHit += TryApplyPoison;
            }

            public void Disable()
            {
                if (_tower.Events != null)
                    _tower.Events.OnHit -= TryApplyPoison;
            }

            private void TryApplyPoison(UpgradeProvider.OnHitData data)
            {
                if (!data.Target) return;

                if (data.DamageType == UpgradeProvider.DamageType.DoT) return;

                if (!data.Target.TryGetComponent<StatusHandler>(out var statusHandler)) return;

                var effect = new PoisonStatus(_config, _tower, data.Target);
                statusHandler.ApplyStatus(effect);
            }
        }

        private class PoisonStatus : IStatusEffect
        {
            private readonly PoisonUpgradeSo _config;
            private readonly BaseTower _sourceTower;
            private readonly GameObject _target;
            private float _expirationTime;


            public PoisonStatus(PoisonUpgradeSo config, BaseTower sourceTower, GameObject target)
            {
                this._config = config;
                this._sourceTower = sourceTower;
                this._target = target;
            }

            public void OnApply(StatusHandler host)
            {
                _expirationTime = Time.time + _config.duration;

                // VFXManager.Instance.Spawn(config.impactEffect, target.transform.position, Quaternion.identity);
            }

            public void Reapply(IStatusEffect newInstance)
            {
                _expirationTime = Time.time + _config.duration;
            }

            public void OnEnd()
            {
            }

            public IEnumerator Process(StatusHandler host)
            {
                var waiter = new WaitForSeconds(_config.tickRate);

                while (Time.time < _expirationTime)
                {
                    yield return waiter;

                    if (Time.time > _expirationTime + 0.1f) yield break;

                    if (!_target) yield break;

                    // if (!target.TryGetComponent<HealthComponent>(out var hp)) continue;

                    _sourceTower.Events.OnHit?.Invoke(new UpgradeProvider.OnHitData
                    {
                        Origin = _sourceTower.gameObject,
                        Target = _target,
                        Damage = _config.damagePerTick,
                        DamageType = UpgradeProvider.DamageType.DoT
                    });

                    // var isDead = hp.TakeDamage(config.damagePerTick);

                    // if (!isDead) continue;

                    _sourceTower.Events.OnKill?.Invoke(new UpgradeProvider.OnKillData
                    {
                        Origin = _sourceTower.gameObject,
                        Target = _target,
                        Damage = _config.damagePerTick
                    });

                    yield break;
                }
            }

            public Sprite GetIcon()
            {
                return _config.icon;
            }

            public float GetDurationRatio()
            {
                if (_config.duration <= 0) return 0;
                var remaining = _expirationTime - Time.time;
                return Mathf.Clamp01(remaining / _config.duration);
            }

            public int GetStackCount()
            {
                return 1;
            }
        }
    }
}