using Enemy.Combat;
using UnityEngine;

namespace Enemy
{
    public sealed class MeleeAttacker : EnemyAttacker
    {
        [SerializeField] public int damage = 2;

        protected override void PerformAttack(GameObject target)
        {
            if (target.TryGetComponent<HealthComponent>(out var health))
            {
                health.TakeDamage(damage);
                Debug.Log($"{name} punched the target!");
            }
        }
    }
}