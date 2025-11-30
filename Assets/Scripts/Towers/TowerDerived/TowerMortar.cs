using System;
using System.Collections.Generic;
using Towers.ProjectileDerived;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Towers.TowerDerived
{
    public class TowerMortar : BaseTower
    {
        [Header("Ballistics")]
        [Tooltip("Minimum flight time (close range).")]
        [SerializeField] private float minProjectileTravelTime = 0.5f;

        [Tooltip("Maximum flight time (max range).")] 
        [SerializeField] private float maxProjectileTravelTime = 2.0f;
        
        [Tooltip("Extra time (in seconds) to predict ahead. If shells land behind, increase this (e.g. 0.1 or 0.2).")]
        [SerializeField] private float leadTimeBonus = 0.1f;

        [SerializeField] private MortarBomb mortarProjectilePrefab;
        [SerializeField] private float radiusOfImpact = 6f;

        // --- Cache ---
        private readonly List<Collider> currentTargetCluster = new(64); 
        private readonly Collider[] initialScanCache = new Collider[64];
        private readonly Collider[] clusterOptimizationCache = new Collider[64]; 

        // --- State ---
        private float currentProjectileTravelTime;
        private bool hasValidTarget;
        private Vector3 predictedAimPoint;
        
        // --- Velocity Tracking ---
        private Vector3 lastFrameCentroid;
        private Vector3 calculatedClusterVelocity;
        private bool isTracking;

        protected override void Update()
        {
            if (!powerSource.IsPowered) return;
            
            // 1. Refresh Cluster & Calculate REAL velocity (Manual Delta)
            if (hasValidTarget && currentTargetCluster.Count > 0)
            {
                TrackClusterMovement();
            }
            else
            {
                if (hasValidTarget) ResetTracking();
            }

            // 2. Aim
            var isAligned = AimAtTarget(Vector3.zero); 

            fireCountdown -= Time.deltaTime;

            if (isAligned && fireCountdown <= 0f)
            {
                if(IsPathClear(firePoint.position, predictedAimPoint, currentProjectileTravelTime))
                {
                    Fire();
                    fireCountdown = 1f / fireRate.Value;
                }
            }
        }
        
        protected override void Fire()
        {
            if (!mortarProjectilePrefab) return;

            var shell = Instantiate(mortarProjectilePrefab, firePoint.position, firePoint.rotation);
            shell.Initialize(this, radiusOfImpact);

            // Physics Reset to ensure pure ballistics
            shell.rigidbody.linearDamping = 0f; 
            shell.rigidbody.angularDamping = 0.05f;
            shell.rigidbody.useGravity = true;

            // Calculate Velocity based on where the target WILL be
            shell.rigidbody.linearVelocity = CalculateLaunchVelocity(predictedAimPoint, currentProjectileTravelTime);
            
            // Visuals
            const float torqueStrength = 4f;
            var randomTorque = Random.insideUnitSphere * torqueStrength;
            shell.rigidbody.AddTorque(randomTorque, ForceMode.Impulse);
        }

        protected override void AcquireTarget()
        {
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, range.Value, initialScanCache, targetLayer);
            if (hitCount == 0)
            {
                ResetTracking();
                return;
            }

            var bestClusterCenter = FindBestCluster(hitCount);
            if (!bestClusterCenter)
            {
                ResetTracking();
                return;
            }

            FillClusterList(bestClusterCenter);
            
            // Reset velocity tracking on new target acquisition to prevent "teleport" jumps
            isTracking = false; 
            TrackClusterMovement(); 

            hasValidTarget = IsPathClear(firePoint.position, predictedAimPoint, currentProjectileTravelTime);
        }

        private void ResetTracking()
        {
            hasValidTarget = false;
            currentTargetCluster.Clear();
            isTracking = false;
            calculatedClusterVelocity = Vector3.zero;
        }

        private void TrackClusterMovement()
        {
            var totalPosition = Vector3.zero;
            var validCount = 0;

            // Clean list and calculate centroid
            for (int i = currentTargetCluster.Count - 1; i >= 0; i--)
            {
                var member = currentTargetCluster[i];
                if (member == null || !member.gameObject.activeInHierarchy)
                {
                    currentTargetCluster.RemoveAt(i);
                    continue;
                }
                totalPosition += member.transform.position;
                validCount++;
            }

            if (validCount == 0)
            {
                ResetTracking();
                return;
            }

            var currentCentroid = totalPosition / validCount;

            // --- MANUAL VELOCITY CALCULATION ---
            // We compare current position to last frame's position. 
            // This is more accurate than NavMeshAgent.velocity for ballistics.
            if (isTracking)
            {
                if (Time.deltaTime > 0)
                {
                    var instantaneousVelocity = (currentCentroid - lastFrameCentroid) / Time.deltaTime;
                    
                    // Simple Low-Pass Filter to smooth out jitter (lerp 50% current, 50% old)
                    calculatedClusterVelocity = Vector3.Lerp(calculatedClusterVelocity, instantaneousVelocity, 0.5f);
                }
            }
            else
            {
                // First frame of tracking, we can't calculate delta yet, try to guess or wait
                calculatedClusterVelocity = Vector3.zero; 
                isTracking = true;
            }

            lastFrameCentroid = currentCentroid;

            UpdatePredictionInternal(currentCentroid, calculatedClusterVelocity);
        }

        private void UpdatePredictionInternal(Vector3 currentPos, Vector3 velocity)
        {
            Vector3 potentialHitPos = currentPos;
            float timeToTarget = GetDynamicTravelTime(potentialHitPos);

            // 3-Pass Iteration
            for(int i = 0; i < 3; i++)
            {
                // We add 'leadTimeBonus' here to force the aim slightly ahead
                // We also add 'Time.fixedDeltaTime' to account for the 1-frame physics delay
                float adjustedTime = timeToTarget + leadTimeBonus + Time.fixedDeltaTime;
                
                potentialHitPos = currentPos + (velocity * adjustedTime);
                timeToTarget = GetDynamicTravelTime(potentialHitPos);
            }

            currentProjectileTravelTime = timeToTarget;
            predictedAimPoint = potentialHitPos;
        }

        // --- Standard Helper Methods (Unchanged Logic, just helper access) ---
        
        private Transform FindBestCluster(int hitCount)
        {
            Transform bestTarget = null;
            var maxDensity = 0;
            for (var i = 0; i < hitCount; i++)
            {
                if (initialScanCache[i] == null) continue;
                var currentDensity = Physics.OverlapSphereNonAlloc(initialScanCache[i].transform.position, radiusOfImpact, clusterOptimizationCache, targetLayer);
                if (currentDensity > maxDensity)
                {
                    maxDensity = currentDensity;
                    bestTarget = initialScanCache[i].transform;
                }
            }
            return bestTarget;
        }

        private void FillClusterList(Transform clusterCenter)
        {
            currentTargetCluster.Clear();
            var hitCount = Physics.OverlapSphereNonAlloc(clusterCenter.position, radiusOfImpact, clusterOptimizationCache, targetLayer);
            for (var i = 0; i < hitCount; i++) 
                if(clusterOptimizationCache[i] != null) currentTargetCluster.Add(clusterOptimizationCache[i]);
        }

        private float GetDynamicTravelTime(Vector3 targetPosition)
        {
            var distance = Vector3.Distance(transform.position, targetPosition);
            var travelTimeFactor = Mathf.Clamp01(distance / range.Value);
            return Mathf.Lerp(minProjectileTravelTime, maxProjectileTravelTime, travelTimeFactor);
        }

        private bool IsPathClear(Vector3 startPoint, Vector3 endPoint, float time)
        {
            var launchVelocity = CalculateLaunchVelocity(endPoint, time);
            if (launchVelocity == Vector3.zero) return false;
            
            var previousPoint = startPoint;
            const int trajectorySteps = 15; 
            for (var i = 1; i <= trajectorySteps; i++)
            {
                var t = (float)i / trajectorySteps * time;
                var currentPoint = startPoint + launchVelocity * t + Physics.gravity * (0.5f * t * t);
                if (Physics.Linecast(previousPoint, currentPoint, visionBlockerLayer)) return false;
                previousPoint = currentPoint;
            }
            return true;
        }

        private Vector3 CalculateLaunchVelocity(Vector3 targetPoint, float time)
        {
            if (time <= 0.001f) return Vector3.zero;
            var displacement = targetPoint - firePoint.position;
            var velocityY = (displacement.y - 0.5f * Physics.gravity.y * (time * time)) / time;
            var velocityX = displacement.x / time;
            var velocityZ = displacement.z / time;
            return new Vector3(velocityX, velocityY, velocityZ);
        }

        protected override bool AimAtTarget(Vector3 aimPoint)
        {
            if (!yPivot || !xPivot) return true;
            
            // Aim at the PREDICTED point, not the current cluster center
            var launchVelocity = CalculateLaunchVelocity(predictedAimPoint, currentProjectileTravelTime);
            if (launchVelocity == Vector3.zero) return false;

            var horizontalDirection = new Vector3(launchVelocity.x, 0, launchVelocity.z);
            if (horizontalDirection.sqrMagnitude < 0.001f) horizontalDirection = Vector3.forward;

            var yLookRotation = Quaternion.LookRotation(horizontalDirection);
            yPivot.rotation = Quaternion.RotateTowards(yPivot.rotation, yLookRotation, yPivotSpeed * Time.deltaTime);

            var localLaunchDirection = yPivot.InverseTransformDirection(launchVelocity);
            if (localLaunchDirection.sqrMagnitude < 0.001f) localLaunchDirection = Vector3.forward;
            
            var xLookRotation = Quaternion.LookRotation(localLaunchDirection);
            xPivot.localRotation = Quaternion.RotateTowards(xPivot.localRotation, xLookRotation, xPivotSpeed * Time.deltaTime);

            var yAligned = Quaternion.Angle(yPivot.rotation, yLookRotation) < rotationThreshold;
            var xAligned = Quaternion.Angle(xPivot.localRotation, xLookRotation) < rotationThreshold;
            return yAligned && xAligned;
        }
    }
}