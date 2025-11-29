using System;
using UnityEngine;

namespace Towers.TargetingStrategies
{
    public interface ITargetingBehaviours
    {
        void Initialize(TowerEntity tower);

        void Dispose(TowerEntity tower);

        event Action<Transform> OnTargetAcquired;
        event Action OnTargetLost;
    }
    
    public interface IWeaponStrategy
    {
        void Initialize(TowerEntity tower);
        void Dispose(TowerEntity tower);
        void UpdateWeapon(TowerEntity tower, float deltaTime);

        event Action OnFired;
        event Action OnReloadStart;
        event Action OnReloadComplete;
    }

    public interface IRotationStrategy
    {
        void Initialize(TowerEntity tower);
        void Dispose(TowerEntity tower);
        void UpdateRotation(TowerEntity tower, float deltaTime);


        event Action<bool> OnAlignmentStatusChanged; 
    }
}