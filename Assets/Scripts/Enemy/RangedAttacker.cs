using Enemy.Combat;
using UnityEngine;

namespace Enemy
{
    public class RangedAttacker : EnemyAttacker
    {
        [Header("Ranged Settings")]
        public GameObject projectilePrefab;
        public Transform firePoint;
        public float projectileSpeed = 20f;
        

        protected override void Start()
        {
            base.Start();
            if (movement) movement.SetStoppingDistance(attackRange - 1.0f);
        }

        protected override void PerformAttack(GameObject target)
        {
            var damage = 10;
            
            // Rotate towards target before firing
            transform.LookAt(target.transform);

            if (target.TryGetComponent<HealthComponent>(out var health))
            {
                health.TakeDamage(damage);
            }
            
            Debug.Log($"{name} fired a shot!");
        }
    }
}