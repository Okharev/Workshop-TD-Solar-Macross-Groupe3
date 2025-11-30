using System;
using UnityEngine;

namespace Towers.TargetingStrategies
{
    [Serializable]
    public class RotationDualAxis : IRotationStrategy
    {
        [Header("Settings")] [SerializeField] private float yPivotSpeed = 10f;

        [SerializeField] private float xPivotSpeed = 5f;
        [SerializeField] private float lockThreshold = 5f;

        private bool _isLocked;

        public RotationDualAxis(
            float yPivotSpeed,
            float xPivotSpeed,
            float lockThreshold
        )
        {
            this.yPivotSpeed = yPivotSpeed;
            this.xPivotSpeed = xPivotSpeed;
            this.lockThreshold = lockThreshold;
        }

        // Event to tell the UI/Weapon we are locked on
        public event Action<bool> OnAlignmentStatusChanged;

        public void Initialize(TowerEntity tower)
        {
            // Optional: Reset rotations to identity on placement
        }

        public void Dispose(TowerEntity tower)
        {
        }

        public void UpdateRotation(TowerEntity tower, float deltaTime)
        {
            // 1. Determine Target Position
            Vector3 targetPos;
            if (tower.currentTarget != null)
                targetPos = tower.currentTarget.position;
            else if (tower.aimPoint != Vector3.zero)
                targetPos = tower.aimPoint;
            else
                return; // No target, do nothing (or rotate to idle)

            var yAligned = true;
            var xAligned = true;

            // 2. Handle Y Axis (Yaw / Base)
            if (tower.yPivot)
            {
                // Get direction ignoring height (Planar check)
                var directionToTarget = targetPos - tower.yPivot.position;
                var planarDirection = Vector3.ProjectOnPlane(directionToTarget, Vector3.up);

                if (planarDirection.sqrMagnitude > 0.001f)
                {
                    var targetRot = Quaternion.LookRotation(planarDirection);
                    tower.yPivot.rotation = Quaternion.RotateTowards(
                        tower.yPivot.rotation,
                        targetRot,
                        yPivotSpeed * 100f * deltaTime // Multiplier for responsiveness
                    );

                    var angle = Quaternion.Angle(tower.yPivot.rotation, targetRot);
                    yAligned = angle < lockThreshold;
                }
            }

            // 3. Handle X Axis (Pitch / Barrel)
            if (tower.xPivot)
            {
                // We rotate the barrel locally to look at the target
                // We use the firePoint or Pivot as reference
                var directionToTarget = targetPos - tower.xPivot.position;

                if (directionToTarget.sqrMagnitude > 0.001f)
                {
                    // Calculate Local Rotation needed
                    // Use the Y-Pivot's Up vector to ensure we don't roll
                    var upAxis = tower.yPivot ? tower.yPivot.up : Vector3.up;
                    var lookRot = Quaternion.LookRotation(directionToTarget, upAxis);

                    // We only want the X component of that rotation
                    // Standard technique: RotateTowards the world rotation
                    tower.xPivot.rotation = Quaternion.RotateTowards(
                        tower.xPivot.rotation,
                        lookRot,
                        xPivotSpeed * 100f * deltaTime
                    );

                    // Check alignment (Angle between forward and target dir)
                    var angle = Vector3.Angle(tower.xPivot.forward, directionToTarget);
                    xAligned = angle < lockThreshold;
                }
            }

            // 4. Update Status
            var currentLock = yAligned && xAligned;
            tower.isAligned = currentLock;

            if (_isLocked != currentLock)
            {
                _isLocked = currentLock;
                OnAlignmentStatusChanged?.Invoke(_isLocked);
            }
        }
    }
}