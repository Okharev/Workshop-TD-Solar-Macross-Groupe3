using UnityEngine;

namespace Towers
{
    [RequireComponent(typeof(Collider))]
    public abstract class BaseProjectile : MonoBehaviour
    {
        public BaseTower source;
        private protected readonly Collider[] collidersCache = new Collider[16];

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!IsValidHit(other)) return;

            HandleImpact(other);
        }
        
        protected abstract void HandleImpact(Collider other);

        protected abstract bool IsValidHit(Collider hitObject);
    }
}