using UnityEngine;
using System;
using Placement;

namespace Pathing.Gameplay
{
    public class EnemyObjectiveTracker : MonoBehaviour
    {
        public ReactiveProperty<Transform> CurrentTarget { get; private set; } = new ReactiveProperty<Transform>();

        private DestructibleObjective _primaryObjective;
        private DestructibleObjective _backupObjective;
        
        public void Initialize(DestructibleObjective primary, DestructibleObjective backup)
        {
            _primaryObjective = primary;
            _backupObjective = backup;

            if (_primaryObjective)
            {
                SetTarget(_primaryObjective);
            }
            else
            {
                SetTarget(_backupObjective);
            }
        }

        private void SetTarget(DestructibleObjective objective)
        {
            if (!objective) return;

            CurrentTarget.Value = objective.transform;
            
            objective.OnDestroyed.AddListener(OnTargetDestroyed);
        }

        private void OnTargetDestroyed()
        {
            if (CurrentTarget.Value == _primaryObjective.transform)
            {
                Debug.Log($"[Enemy] Objectif {_primaryObjective.name} détruit ! Redirection vers {_backupObjective.name}");
                
                _primaryObjective.OnDestroyed.RemoveListener(OnTargetDestroyed);
                
                if (_backupObjective)
                {
                    CurrentTarget.Value = _backupObjective.transform;
                }
            }
        }

        private void OnDestroy()
        {
            if (_primaryObjective != null) _primaryObjective.OnDestroyed.RemoveListener(OnTargetDestroyed);
        }
    }
}