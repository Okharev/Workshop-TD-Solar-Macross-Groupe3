using Enemy.Combat;
using UnityEngine;

namespace Enemy
{
    public class MeleeAttacker : EnemyAttacker
    {
        protected override void PerformAttack(GameObject target)
        {
            var damage = 10;
            
            if (target.TryGetComponent<HealthComponent>(out var health))
            {
                health.TakeDamage(damage);
                Debug.Log($"{name} punched the target!");
            }
        }
    }
}