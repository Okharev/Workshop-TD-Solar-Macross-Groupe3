using System.Collections;
using Economy;
using UnityEngine;

namespace Towers.TowerDerived
{
    public sealed class TowerAoE : BaseTower
    {
        [Header("AoE Configuration")] [SerializeField]
        private LayerMask enemyLayer;

        [SerializeField] private ParticleSystem pulseEffect;

        protected override void Start()
        {
            // Auto-configure layer if forgotten
            if (enemyLayer == 0) enemyLayer = LayerMask.GetMask("Enemy");

            if (Mathf.Approximately(fireRate.BaseValue, 1f)) fireRate.BaseValue = 1.25f;

            StartCoroutine(DamageLoop());
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
//            Gizmos.DrawSphere(transform.position, range.Value.CurrentValue);
        }

        private IEnumerator DamageLoop()
        {
            while (true)
            {

                var currentFireRate = fireRate.Value;
                var delay = currentFireRate > 0 ? 1f / currentFireRate : 0.8f;

                yield return new WaitForSeconds(delay);

                if (powerSource && powerSource.IsPowered) PulseDamage();
            }
        }

        private void PulseDamage()
        {
            var currentRange = range.Value;
            var currentDamage = damage.Value;


            var hits = Physics.OverlapSphere(transform.position, currentRange, enemyLayer);

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<EnemyController>();

                if (enemy)

                    if (Vector3.Distance(transform.position, enemy.transform.position) <= currentRange)
                        DealDamage(enemy, currentDamage);
            }
        }

        private void DealDamage(EnemyController enemy, float amount)
        {
            // enemy.TakeDamage(amount);

            Debug.Log($"ennemy: {enemy.gameObject.name} took {amount} damage");
        }

        protected override void Fire()
        {
        }

        protected override void AcquireTarget()
        {
        }
    }
}