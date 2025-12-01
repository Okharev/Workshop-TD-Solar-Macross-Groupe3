using System.Collections;
using System.Collections.Generic;
using Economy;
using UnityEngine;
// Import R3

// Required to access EnemyController

namespace Towers.TowerDerived
{
    public class TowerSlow : BaseTower
    {
        [Header("Slow Configuration")] [Tooltip("Percentage to slow the enemy. 0.3 = 30% slow.")] [Range(0f, 0.9f)]
        public float slowPercent = 0.3f;

        [Header("Performance")] [SerializeField]
        private float checkInterval = 0.2f;

        [SerializeField] private LayerMask enemyLayer;

        // Optimization: Reusable lists/arrays to prevent Garbage Collection allocation in the loop
        private readonly List<EnemyController> _currentFrameEnemies = new();
        private readonly List<EnemyController> _enemiesToRemove = new();
        private readonly Collider[] _hitBuffer = new Collider[32]; // Cap max targets to 50 for performance

        // Track currently slowed enemies to remove the slow when they leave range
        private readonly HashSet<EnemyController> _slowedEnemies = new();

        protected override void Start()
        {
            if (enemyLayer == 0) enemyLayer = LayerMask.GetMask("Enemy");
            StartCoroutine(SlowLoop());
        }

        // Cleanup on Tower Sell/Destroy
        private void OnDestroy()
        {
            RemoveAllSlows();
            StopAllCoroutines();
        }

        private void OnDrawGizmosSelected()
        {
            // Safety check for range in editor mode
            var r = range.Value;
            Gizmos.color = new Color(0, 0, 1, 0.3f);
            Gizmos.DrawSphere(transform.position, r);

            Gizmos.color = Color.green;
            foreach (var nemy in _slowedEnemies) Gizmos.DrawLine(transform.position, nemy.transform.position);
        }

        private IEnumerator SlowLoop()
        {
            var wait = new WaitForSeconds(checkInterval);

            while (true) // Coroutine continues until GameObject is destroyed
            {
                // Check if the R3 View is destroyed to stop the loop safely (optional but good practice)
                if (!this) yield break;

                // Logic: Only apply slow if powered on
                if (powerSource && powerSource.IsPowered)
                    CheckForEnemies();
                else if (_slowedEnemies.Count > 0)
                    // If power is lost, immediately free everyone
                    RemoveAllSlows();

                yield return wait;
            }
        }

        private void CheckForEnemies()
        {
            Debug.Log("1");

            var currentRange = range.Value;

            // 1. Physics Check (NonAlloc for performance)
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, currentRange, _hitBuffer, enemyLayer);

            Debug.Log("2");

            _currentFrameEnemies.Clear();

            // 2. Identify Valid Enemies in Range
            for (var i = 0; i < hitCount; i++)
            {
                Debug.Log("3: " + hitCount);

                var enemy = _hitBuffer[i].GetComponentInParent<EnemyController>();

                if (enemy)
                {
                    Debug.Log("4");

                    // Optional: Distance check if collider is larger than range
                    var dist = Vector3.Distance(transform.position, enemy.transform.position);
                    if (dist <= currentRange)
                    {
                        _currentFrameEnemies.Add(enemy);

                        Debug.Log("5");


                        // If this enemy is new to the set, apply the slow
                        if (!_slowedEnemies.Contains(enemy))
                        {
                            ApplySlow(enemy);
                            _slowedEnemies.Add(enemy);
                        }
                    }
                }
            }

            // 3. Cleanup: Find enemies in the "Slowed" list that are NOT in the "Current" list
            _enemiesToRemove.Clear();

            foreach (var slowedEnemy in _slowedEnemies)
                // If enemy died (null) OR is no longer in range list
                if (!slowedEnemy || !_currentFrameEnemies.Contains(slowedEnemy))
                    _enemiesToRemove.Add(slowedEnemy);

            // Remove modifiers
            foreach (var oldEnemy in _enemiesToRemove)
            {
                if (oldEnemy) RemoveSlow(oldEnemy);
                _slowedEnemies.Remove(oldEnemy);
            }
        }

        private void ApplySlow(EnemyController target)
        {
            // R3 Adaptation:
            // Create a modifier with 'this' as the source.
            // Value is negative because Type is PercentAdd (Add -0.3 = 70% speed).
            var mod = new StatModifier(-slowPercent, StatModType.PercentAdd, this);

            // Assuming EnemyController has a field 'Speed' of type ReactiveStat
            target.speed.AddModifier(mod);
        }

        private void RemoveSlow(EnemyController target)
        {
            // R3 Adaptation:
            // Remove all modifiers created by THIS tower instance
            target.speed.RemoveAllModifiersFromSource(this);
        }

        private void RemoveAllSlows()
        {
            foreach (var enemy in _slowedEnemies)
                if (enemy)
                    RemoveSlow(enemy);
            _slowedEnemies.Clear();
        }

        protected override void Fire()
        {
        }

        protected override void AcquireTarget()
        {
        }
    }
}