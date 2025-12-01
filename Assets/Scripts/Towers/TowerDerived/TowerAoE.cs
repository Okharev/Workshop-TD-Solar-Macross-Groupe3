using System.Collections;
using UnityEngine;
using Economy; // Required for EnemyController

namespace Towers.TowerDerived
{
    public class TowerAoE : BaseTower
    {
        [Header("AoE Configuration")]
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private ParticleSystem pulseEffect;

        protected override void Start()
        {
            // Auto-configure layer if forgotten
            if (enemyLayer == 0) enemyLayer = LayerMask.GetMask("Enemy");

            if (Mathf.Approximately(fireRate.BaseValue, 1f)) 
            {
                fireRate.BaseValue = 1.25f; 
            }

            StartCoroutine(DamageLoop());
        }

        private IEnumerator DamageLoop()
        {
            while (true)
            {
                // Calculate delay based on FireRate Stat.
                // This allows the "Buff Tower" to actually make this tower tick faster!
                // Formula: Interval = 1 / FireRate
                float currentFireRate = fireRate.Value.CurrentValue;
                float delay = currentFireRate > 0 ? 1f / currentFireRate : 0.8f;

                yield return new WaitForSeconds(delay);

                if (powerSource && powerSource.IsPowered.CurrentValue)
                {
                    PulseDamage();
                }
            }
        }

        private void PulseDamage()
        {
            float currentRange = range.Value.CurrentValue;
            float currentDamage = damage.Value.CurrentValue;

            // 1. Play Visuals
            if (pulseEffect) pulseEffect.Play();

            // 2. Find Targets
            Collider[] hits = Physics.OverlapSphere(transform.position, currentRange, enemyLayer);

            foreach (var hit in hits)
            {
                EnemyController enemy = hit.GetComponentInParent<EnemyController>();

                if (enemy)
                {
                    // 3. Precise Distance Check
                    // Ensures we don't hit enemies whose collider just barely touched the edge
                    // unless their center point is actually in range.
                    if (Vector3.Distance(transform.position, enemy.transform.position) <= currentRange)
                    {
                        DealDamage(enemy, currentDamage);
                    }
                }
            }
        }

        private void DealDamage(EnemyController enemy, float amount)
        {
            // enemy.TakeDamage(amount);
            
            Debug.Log($"ennemy: {enemy.gameObject.name} took {amount} damage");
        }

        // Disable standard firing logic since we run our own Coroutine
        protected override void Fire() { }
        protected override void AcquireTarget() { }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f); // Red for damage
            Gizmos.DrawSphere(transform.position, range.Value.CurrentValue);
        }
    }
}