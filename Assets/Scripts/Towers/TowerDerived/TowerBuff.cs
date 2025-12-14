using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Towers.TowerDerived
{
    public sealed class TowerBuff : BaseTower
    {
        [Header("Buff Configuration")] [Range(0, 2)]
        public float damagePercentBuff = 0.2f;

        [Range(0, 2)] public float rangePercentBuff;
        [Range(0, 2)] public float fireRatePercentBuff = 0.1f;

        [Header("Performance")] [SerializeField]
        private float checkInterval = 0.25f;

        [SerializeField] private LayerMask towerLayer;

        private readonly HashSet<BaseTower> _currentBuffedTowers = new();

        protected override void Start()
        {
            if (towerLayer == 0) towerLayer = LayerMask.GetMask("PlacementBlockers");

            StartCoroutine(BuffLoop());
        }

        private void OnDestroy()
        {
            RemoveAllBuffs();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawSphere(transform.position, range.Value);

            Gizmos.color = Color.green;
            var hits = Physics.OverlapSphere(transform.position, range.Value, towerLayer);
            foreach (var hit in hits)
            {
                var t = hit.GetComponent<BaseTower>();
                if (t && t != this)
                    // Check the Point logic visually
                    if (Vector3.Distance(transform.position, t.transform.position) <= range.Value)
                        Gizmos.DrawLine(transform.position, t.transform.position);
            }
        }


        private IEnumerator BuffLoop()
        {
            var wait = new WaitForSeconds(checkInterval);

            while (true)
            {
                if (powerSource.IsPowered)
                    CheckForTowers();
                else
                    RemoveAllBuffs();
                yield return wait;
            }
        }

        private void CheckForTowers()
        {
            var currentRange = range.Value;

            var hits = Physics.OverlapSphere(transform.position, currentRange, towerLayer);

            var validNeighbors = new HashSet<BaseTower>();

            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent<BaseTower>(out var neighbor)) continue;

                if (neighbor && neighbor != this)
                {
                    var dist = Vector3.Distance(transform.position, neighbor.transform.position);

                    if (dist <= currentRange)
                    {
                        validNeighbors.Add(neighbor);

                        if (!_currentBuffedTowers.Contains(neighbor))
                        {
                            ApplyBuffs(neighbor);
                            _currentBuffedTowers.Add(neighbor);
                        }
                    }
                }
            }


            var toRemove = new List<BaseTower>();
            foreach (var oldTower in _currentBuffedTowers)
                if (!oldTower || !validNeighbors.Contains(oldTower))
                    toRemove.Add(oldTower);

            foreach (var old in toRemove)
            {
                if (old) RemoveBuffs(old);
                _currentBuffedTowers.Remove(old);
            }
        }

        private void ApplyBuffs(BaseTower target)
        {
            if (damagePercentBuff > 0)
                target.damage.AddModifier(new StatModifier(damagePercentBuff, StatModType.PercentAdd, this));

            if (rangePercentBuff > 0)
                target.range.AddModifier(new StatModifier(rangePercentBuff, StatModType.PercentAdd, this));

            if (fireRatePercentBuff > 0)
                target.fireRate.AddModifier(new StatModifier(fireRatePercentBuff, StatModType.PercentAdd, this));

            Debug.Log(
                $"Buff added to {target.name}, old stat: {target.damage}dmg, {target.fireRate}rate,  {target.range}range || new stat: {target.damage.Value} dmg, fire{target.fireRate.Value}, range {target.range.Value}");
        }

        private void RemoveBuffs(BaseTower target)
        {
            target.damage.RemoveAllModifiersFromSource(this);
            target.range.RemoveAllModifiersFromSource(this);
            target.fireRate.RemoveAllModifiersFromSource(this);

            Debug.Log($"Buff removed from {target.name}");
        }

        private void RemoveAllBuffs()
        {
            foreach (var t in _currentBuffedTowers)
                if (t)
                    RemoveBuffs(t);
            _currentBuffedTowers.Clear();
        }

        protected override void Fire()
        {
        }

        protected override void AcquireTarget()
        {
        }
    }
}