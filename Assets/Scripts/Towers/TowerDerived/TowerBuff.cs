using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Towers.TowerDerived
{
    public class TowerBuff : BaseTower
    {
        [Header("Buff Configuration")]
        [Range(0, 2)] public float damagePercentBuff = 0.2f;
        [Range(0, 2)] public float rangePercentBuff = 0f;
        [Range(0, 2)] public float fireRatePercentBuff = 0.1f;

        [Header("Performance")]
        [SerializeField] private float checkInterval = 2f; // Check 4 times a second
        [SerializeField] private LayerMask towerLayer; // Assign your "Towers" layer here

        // We keep track of who currently has the buff so we can remove it if they move out/died
        private readonly HashSet<BaseTower> _currentBuffedTowers = new HashSet<BaseTower>();

        protected override void Start()
        {
            // Auto-configure layer if forgotten
            if (towerLayer == 0) towerLayer = LayerMask.GetMask("PlacementBlockers"); // Adjust to your layer name
            
            StartCoroutine(BuffLoop());
        }


        private IEnumerator BuffLoop()
        {
            var wait = new WaitForSeconds(checkInterval);

            while (true)
            {
                if (powerSource.IsPowered.CurrentValue) // Optional: Stop buffing if out of power
                {
                    CheckForTowers();
                }
                else
                {
                    RemoveAllBuffs();
                }
                yield return wait;
            }
        }

        private void CheckForTowers()
        {
            float currentRange = range.Value.CurrentValue;

            // 1. Find all colliders roughly in the area (efficient physics query)
            Collider[] hits = Physics.OverlapSphere(transform.position, currentRange, towerLayer);

            // Create a temporary set to track who is valid THIS frame
            HashSet<BaseTower> validNeighbors = new HashSet<BaseTower>();

            foreach (var hit in hits)
            {
                BaseTower neighbor = hit.GetComponent<BaseTower>();
                
                // Validate neighbor
                if (neighbor && neighbor != this)
                {
                    // 2. THE POINT CHECK
                    // Physics.OverlapSphere might catch the edge of a collider.
                    // This check ensures the CENTER POINT of the tower is actually inside the range.
                    float dist = Vector3.Distance(transform.position, neighbor.transform.position);
                    
                    if (dist <= currentRange)
                    {
                        validNeighbors.Add(neighbor);
                        
                        // If we haven't buffed them yet, do it now
                        if (!_currentBuffedTowers.Contains(neighbor))
                        {
                            ApplyBuffs(neighbor);
                            _currentBuffedTowers.Add(neighbor);
                        }
                    }
                }
            }

            // 3. Cleanup: Find towers that were buffed but are no longer in the valid list
            // (They were sold, destroyed, or we downgraded range)
            List<BaseTower> toRemove = new List<BaseTower>();
            foreach (var oldTower in _currentBuffedTowers)
            {
                if (!oldTower || !validNeighbors.Contains(oldTower))
                {
                    toRemove.Add(oldTower);
                }
            }

            // Remove buffs from the ones that are gone
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

            Debug.Log($"Buff added to {target.name}");
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
            {
                if(t) RemoveBuffs(t);
            }
            _currentBuffedTowers.Clear();
        }

        private void OnDestroy()
        {
            RemoveAllBuffs();
        }

        protected override void Fire() {}
        protected override void AcquireTarget() {}
        
        private void OnDrawGizmosSelected()
        {
            // Draw the Range Circle
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawSphere(transform.position, range.Value.CurrentValue);

            // Draw lines to valid targets (Just for debug visualization)
            Gizmos.color = Color.green;
            Collider[] hits = Physics.OverlapSphere(transform.position, range.Value.CurrentValue, towerLayer);
            foreach (var hit in hits)
            {
                BaseTower t = hit.GetComponent<BaseTower>();
                if (t != null && t != this)
                {
                    // Check the Point logic visually
                    if (Vector3.Distance(transform.position, t.transform.position) <=  range.Value.CurrentValue)
                    {
                        Gizmos.DrawLine(transform.position, t.transform.position);
                    }
                }
            }
        }
    }
}